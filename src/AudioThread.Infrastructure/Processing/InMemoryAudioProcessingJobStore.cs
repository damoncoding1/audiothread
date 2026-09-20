using System.Collections.Concurrent;
using System.Threading.Channels;
using AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;
using AudioThread.Application.Features.AudioFiles.Jobs;

namespace AudioThread.Infrastructure.Processing;

public sealed class InMemoryAudioProcessingJobStore : IAudioProcessingJobStore
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<Guid, JobEntry> _jobs = new();

    public AudioProcessingJobSnapshot Create(Guid jobId, string originalFileName)
    {
        RemoveExpiredJobs();

        var steps = AudioProcessingSteps.All
            .Select(step => new AudioProcessingStepSnapshot(step, AudioProcessingStepState.Pending, null))
            .ToList();

        var snapshot = new AudioProcessingJobSnapshot(
            jobId,
            originalFileName,
            AudioProcessingJobStatus.Queued,
            steps,
            Result: null,
            ErrorMessage: null,
            DateTimeOffset.UtcNow,
            CompletedAtUtc: null);

        _jobs[jobId] = new JobEntry(snapshot);

        return snapshot;
    }

    public AudioProcessingJobSnapshot? Get(Guid jobId) =>
        _jobs.TryGetValue(jobId, out var entry) ? entry.Snapshot : null;

    public void MarkProcessing(Guid jobId) =>
        Mutate(jobId, snapshot => snapshot with { Status = AudioProcessingJobStatus.Processing });

    public void UpdateStep(Guid jobId, AudioProcessingStepUpdate update)
    {
        Mutate(jobId, snapshot =>
        {
            var steps = snapshot.Steps
                .Select(step => step.Step == update.Step
                    ? step with
                    {
                        State = update.State,
                        DurationMilliseconds = update.Duration is null
                            ? step.DurationMilliseconds
                            : (long)update.Duration.Value.TotalMilliseconds
                    }
                    : step)
                .ToList();

            return snapshot with { Steps = steps };
        });
    }

    public void Complete(Guid jobId, UploadAudioFileResponse result) =>
        Mutate(jobId, snapshot => snapshot with
        {
            Status = AudioProcessingJobStatus.Completed,
            Result = result,
            CompletedAtUtc = DateTimeOffset.UtcNow
        });

    public void Fail(Guid jobId, string errorMessage) =>
        Mutate(jobId, snapshot => snapshot with
        {
            Status = AudioProcessingJobStatus.Failed,
            ErrorMessage = errorMessage,
            Steps = [.. snapshot.Steps.Select(step => step.State == AudioProcessingStepState.Running
                ? step with { State = AudioProcessingStepState.Failed }
                : step)],
            CompletedAtUtc = DateTimeOffset.UtcNow
        });

    public async IAsyncEnumerable<AudioProcessingJobSnapshot> WatchAsync(
        Guid jobId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var entry))
        {
            yield break;
        }

        var channel = Channel.CreateUnbounded<AudioProcessingJobSnapshot>();
        var current = entry.Subscribe(channel.Writer);

        yield return current;

        if (current.IsFinished)
        {
            entry.Unsubscribe(channel.Writer);
            yield break;
        }

        try
        {
            await foreach (var snapshot in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return snapshot;

                if (snapshot.IsFinished)
                {
                    yield break;
                }
            }
        }
        finally
        {
            entry.Unsubscribe(channel.Writer);
        }
    }

    private void Mutate(Guid jobId, Func<AudioProcessingJobSnapshot, AudioProcessingJobSnapshot> mutation)
    {
        if (_jobs.TryGetValue(jobId, out var entry))
        {
            entry.Update(mutation);
        }
    }

    private void RemoveExpiredJobs()
    {
        var threshold = DateTimeOffset.UtcNow - RetentionPeriod;

        foreach (var (jobId, entry) in _jobs)
        {
            if (entry.Snapshot.IsFinished && entry.Snapshot.CompletedAtUtc < threshold)
            {
                _jobs.TryRemove(jobId, out _);
            }
        }
    }

    private sealed class JobEntry
    {
        private readonly Lock _gate = new();
        private readonly List<ChannelWriter<AudioProcessingJobSnapshot>> _subscribers = [];

        public JobEntry(AudioProcessingJobSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public AudioProcessingJobSnapshot Snapshot { get; private set; }

        public AudioProcessingJobSnapshot Subscribe(ChannelWriter<AudioProcessingJobSnapshot> writer)
        {
            lock (_gate)
            {
                _subscribers.Add(writer);
                return Snapshot;
            }
        }

        public void Unsubscribe(ChannelWriter<AudioProcessingJobSnapshot> writer)
        {
            lock (_gate)
            {
                _subscribers.Remove(writer);
            }

            writer.TryComplete();
        }

        public void Update(Func<AudioProcessingJobSnapshot, AudioProcessingJobSnapshot> mutation)
        {
            ChannelWriter<AudioProcessingJobSnapshot>[] subscribers;
            AudioProcessingJobSnapshot updated;

            lock (_gate)
            {
                updated = mutation(Snapshot);
                Snapshot = updated;
                subscribers = [.. _subscribers];
            }

            foreach (var subscriber in subscribers)
            {
                subscriber.TryWrite(updated);
            }
        }
    }
}
