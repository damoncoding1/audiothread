using System.Runtime.CompilerServices;
using AudioThread.Application.Features.AudioFiles.Jobs;
using AudioThread.Infrastructure.Processing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AudioThread.Infrastructure.Tests.Processing;

public sealed class ParallelAudioProcessingExecutorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ExecuteAsync_RespectsTheConfiguredMaximumConcurrency(int maxConcurrentJobs)
    {
        var executor = new ParallelAudioProcessingExecutor(
            Options.Create(new AudioProcessingOptions
            {
                ExecutionMode = AudioProcessingExecutionMode.Optimized,
                MaxConcurrentJobs = maxConcurrentJobs,
                QueueCapacity = 4
            }),
            NullLogger<ParallelAudioProcessingExecutor>.Instance);

        var expectedWorkersStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWorkers = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var activeJobs = 0;
        var maximumObserved = 0;

        var execution = executor.ExecuteAsync(
            CreateWorkItems(4),
            async (_, cancellationToken) =>
            {
                var current = Interlocked.Increment(ref activeJobs);
                UpdateMaximum(ref maximumObserved, current);

                if (current == maxConcurrentJobs)
                {
                    expectedWorkersStarted.TrySetResult();
                }

                await releaseWorkers.Task.WaitAsync(cancellationToken);
                Interlocked.Decrement(ref activeJobs);
            },
            CancellationToken.None);

        await expectedWorkersStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);

        Assert.Equal(maxConcurrentJobs, Volatile.Read(ref maximumObserved));

        releaseWorkers.TrySetResult();
        await execution;
    }

    [Fact]
    public async Task ExecuteAsync_SequentialModeProcessesOnlyOneWorkItemAtATime()
    {
        var executor = new ParallelAudioProcessingExecutor(
            Options.Create(new AudioProcessingOptions
            {
                ExecutionMode = AudioProcessingExecutionMode.Sequential,
                MaxConcurrentJobs = 2,
                QueueCapacity = 4
            }),
            NullLogger<ParallelAudioProcessingExecutor>.Instance);
        var activeJobs = 0;
        var maximumObserved = 0;

        await executor.ExecuteAsync(
            CreateWorkItems(4),
            async (_, cancellationToken) =>
            {
                var current = Interlocked.Increment(ref activeJobs);
                UpdateMaximum(ref maximumObserved, current);
                await Task.Delay(20, cancellationToken);
                Interlocked.Decrement(ref activeJobs);
            },
            CancellationToken.None);

        Assert.Equal(1, maximumObserved);
    }

    private static async IAsyncEnumerable<AudioProcessingWorkItem> CreateWorkItems(
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new AudioProcessingWorkItem(
                Guid.NewGuid(),
                [1, 2, 3],
                $"voice-{index}.wav",
                "audio/wav",
                3);

            await Task.Yield();
        }
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref maximum);
            if (candidate <= current || Interlocked.CompareExchange(ref maximum, candidate, current) == current)
            {
                return;
            }
        }
    }
}
