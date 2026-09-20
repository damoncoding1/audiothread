using AudioThread.Application.Features.AudioFiles.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AudioThread.Infrastructure.Processing;

public sealed class ParallelAudioProcessingExecutor
{
    private readonly AudioProcessingOptions _options;
    private readonly ILogger<ParallelAudioProcessingExecutor> _logger;

    public ParallelAudioProcessingExecutor(
        IOptions<AudioProcessingOptions> options,
        ILogger<ParallelAudioProcessingExecutor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task ExecuteAsync(
        IAsyncEnumerable<AudioProcessingWorkItem> workItems,
        Func<AudioProcessingWorkItem, CancellationToken, Task> processor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workItems);
        ArgumentNullException.ThrowIfNull(processor);

        if (_options.ExecutionMode == AudioProcessingExecutionMode.Sequential)
        {
            _logger.LogInformation("Starting the sequential background audio executor");
            return ExecuteSequentiallyAsync(workItems, processor, cancellationToken);
        }

        _logger.LogInformation(
            "Starting the background audio executor with up to {MaxConcurrentJobs} concurrent jobs",
            _options.MaxConcurrentJobs);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _options.MaxConcurrentJobs,
            CancellationToken = cancellationToken
        };

        return Parallel.ForEachAsync(
            workItems,
            parallelOptions,
            async (workItem, itemCancellationToken) =>
                await processor(workItem, itemCancellationToken));
    }

    private static async Task ExecuteSequentiallyAsync(
        IAsyncEnumerable<AudioProcessingWorkItem> workItems,
        Func<AudioProcessingWorkItem, CancellationToken, Task> processor,
        CancellationToken cancellationToken)
    {
        await foreach (var workItem in workItems.WithCancellation(cancellationToken))
        {
            await processor(workItem, cancellationToken);
        }
    }
}
