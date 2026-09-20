using AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;
using AudioThread.Application.Features.AudioFiles.Jobs;
using AudioThread.Infrastructure.Processing;
using Xunit;

namespace AudioThread.Infrastructure.Tests.Processing;

public sealed class InMemoryAudioProcessingJobStoreTests
{
    private static readonly UploadAudioFileResponse SampleResult = new(
        Guid.NewGuid(),
        "voice.wav",
        "audio/aac",
        1729851,
        "audio-files",
        "uploads/voice.aac",
        null,
        null,
        null,
        "Resumen de prueba",
        DateTimeOffset.UtcNow);

    [Fact]
    public void Create_StartsQueuedWithEveryStepPending()
    {
        var store = new InMemoryAudioProcessingJobStore();
        var jobId = Guid.NewGuid();

        var snapshot = store.Create(jobId, "voice.wav");

        Assert.Equal(AudioProcessingJobStatus.Queued, snapshot.Status);
        Assert.Equal(AudioProcessingSteps.All.Count, snapshot.Steps.Count);
        Assert.All(snapshot.Steps, step => Assert.Equal(AudioProcessingStepState.Pending, step.State));
        Assert.False(snapshot.IsFinished);
    }

    [Fact]
    public void UpdateStep_ChangesOnlyTheTargetStepAndKeepsTheDuration()
    {
        var store = new InMemoryAudioProcessingJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, "voice.wav");

        store.UpdateStep(jobId, new AudioProcessingStepUpdate(
            AudioProcessingSteps.Compression,
            AudioProcessingStepState.Completed,
            TimeSpan.FromMilliseconds(574)));

        var snapshot = store.Get(jobId);
        Assert.NotNull(snapshot);

        var compression = snapshot.Steps.Single(step => step.Step == AudioProcessingSteps.Compression);
        var transcription = snapshot.Steps.Single(step => step.Step == AudioProcessingSteps.Transcription);

        Assert.Equal(AudioProcessingStepState.Completed, compression.State);
        Assert.Equal(574, compression.DurationMilliseconds);
        Assert.Equal(AudioProcessingStepState.Pending, transcription.State);
    }

    [Fact]
    public void Fail_MarksRunningStepsAsFailedAndKeepsTheMessage()
    {
        var store = new InMemoryAudioProcessingJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, "voice.wav");
        store.UpdateStep(jobId, new AudioProcessingStepUpdate(
            AudioProcessingSteps.Transcription,
            AudioProcessingStepState.Running));

        store.Fail(jobId, "El modelo no esta disponible.");

        var snapshot = store.Get(jobId);
        Assert.NotNull(snapshot);
        Assert.Equal(AudioProcessingJobStatus.Failed, snapshot.Status);
        Assert.Equal("El modelo no esta disponible.", snapshot.ErrorMessage);
        Assert.True(snapshot.IsFinished);

        var transcription = snapshot.Steps.Single(step => step.Step == AudioProcessingSteps.Transcription);
        Assert.Equal(AudioProcessingStepState.Failed, transcription.State);
    }

    [Fact]
    public async Task WatchAsync_EmitsTheCurrentSnapshotAndEveryChange()
    {
        var store = new InMemoryAudioProcessingJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, "voice.wav");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<AudioProcessingJobSnapshot>();

        var watching = Task.Run(async () =>
        {
            await foreach (var snapshot in store.WatchAsync(jobId, timeout.Token))
            {
                received.Add(snapshot);
            }
        }, timeout.Token);

        await Task.Delay(50, timeout.Token);
        store.MarkProcessing(jobId);
        store.UpdateStep(jobId, new AudioProcessingStepUpdate(
            AudioProcessingSteps.Compression,
            AudioProcessingStepState.Completed,
            TimeSpan.FromMilliseconds(574)));
        store.Complete(jobId, SampleResult);

        await watching;

        Assert.Equal(AudioProcessingJobStatus.Queued, received[0].Status);
        Assert.Contains(received, snapshot => snapshot.Status == AudioProcessingJobStatus.Processing);
        Assert.Equal(AudioProcessingJobStatus.Completed, received[^1].Status);
        Assert.Equal(SampleResult.Summary, received[^1].Result?.Summary);
    }

    [Fact]
    public async Task WatchAsync_StopsImmediatelyWhenTheJobAlreadyFinished()
    {
        var store = new InMemoryAudioProcessingJobStore();
        var jobId = Guid.NewGuid();
        store.Create(jobId, "voice.wav");
        store.Complete(jobId, SampleResult);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<AudioProcessingJobSnapshot>();

        await foreach (var snapshot in store.WatchAsync(jobId, timeout.Token))
        {
            received.Add(snapshot);
        }

        var single = Assert.Single(received);
        Assert.Equal(AudioProcessingJobStatus.Completed, single.Status);
    }

    [Fact]
    public async Task WatchAsync_YieldsNothingForAnUnknownJob()
    {
        var store = new InMemoryAudioProcessingJobStore();
        var received = new List<AudioProcessingJobSnapshot>();

        await foreach (var snapshot in store.WatchAsync(Guid.NewGuid(), CancellationToken.None))
        {
            received.Add(snapshot);
        }

        Assert.Empty(received);
    }
}
