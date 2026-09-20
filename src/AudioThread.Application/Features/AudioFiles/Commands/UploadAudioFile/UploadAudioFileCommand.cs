using AudioThread.Application.Features.AudioFiles.Jobs;

namespace AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;

public sealed record UploadAudioFileCommand(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeInBytes)
{
    public IProgress<AudioProcessingStepUpdate>? Progress { get; init; }
}
