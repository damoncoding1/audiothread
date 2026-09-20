namespace AudioThread.Application.Features.AudioFiles.Jobs;

public sealed record AudioProcessingWorkItem(
    Guid JobId,
    byte[] Content,
    string FileName,
    string ContentType,
    long SizeInBytes);

public interface IAudioProcessingJobQueue
{
    ValueTask EnqueueAsync(AudioProcessingWorkItem workItem, CancellationToken cancellationToken);

    IAsyncEnumerable<AudioProcessingWorkItem> DequeueAllAsync(CancellationToken cancellationToken);
}
