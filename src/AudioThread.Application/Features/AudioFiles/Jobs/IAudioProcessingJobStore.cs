using AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;

namespace AudioThread.Application.Features.AudioFiles.Jobs;

public interface IAudioProcessingJobStore
{
    AudioProcessingJobSnapshot Create(Guid jobId, string originalFileName);

    AudioProcessingJobSnapshot? Get(Guid jobId);

    void MarkProcessing(Guid jobId);

    void UpdateStep(Guid jobId, AudioProcessingStepUpdate update);

    void Complete(Guid jobId, UploadAudioFileResponse result);

    void Fail(Guid jobId, string errorMessage);

    IAsyncEnumerable<AudioProcessingJobSnapshot> WatchAsync(Guid jobId, CancellationToken cancellationToken);
}
