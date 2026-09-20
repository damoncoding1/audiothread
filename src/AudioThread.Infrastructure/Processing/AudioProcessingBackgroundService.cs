using AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;
using AudioThread.Application.Features.AudioFiles.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AudioThread.Infrastructure.Processing;

public sealed class AudioProcessingBackgroundService : BackgroundService
{
    private readonly IAudioProcessingJobQueue _queue;
    private readonly IAudioProcessingJobStore _store;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ParallelAudioProcessingExecutor _executor;
    private readonly ILogger<AudioProcessingBackgroundService> _logger;

    public AudioProcessingBackgroundService(
        IAudioProcessingJobQueue queue,
        IAudioProcessingJobStore store,
        IServiceScopeFactory scopeFactory,
        ParallelAudioProcessingExecutor executor,
        ILogger<AudioProcessingBackgroundService> logger)
    {
        _queue = queue;
        _store = store;
        _scopeFactory = scopeFactory;
        _executor = executor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _executor.ExecuteAsync(
            _queue.DequeueAllAsync(stoppingToken),
            ProcessAsync,
            stoppingToken);
    }

    private async Task ProcessAsync(AudioProcessingWorkItem workItem, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting background job {JobId} for {FileName}",
            workItem.JobId,
            workItem.FileName);

        _store.MarkProcessing(workItem.JobId);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<UploadAudioFileHandler>();
            var progress = new Progress<AudioProcessingStepUpdate>(
                update => _store.UpdateStep(workItem.JobId, update));

            await using var content = new MemoryStream(workItem.Content, writable: false);
            var result = await handler.HandleAsync(
                new UploadAudioFileCommand(
                    content,
                    workItem.FileName,
                    workItem.ContentType,
                    workItem.SizeInBytes)
                {
                    Progress = progress
                },
                cancellationToken);

            _store.Complete(workItem.JobId, result);

            _logger.LogInformation("Background job {JobId} completed", workItem.JobId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Background job {JobId} failed", workItem.JobId);
            _store.Fail(workItem.JobId, exception.Message);
        }
    }
}
