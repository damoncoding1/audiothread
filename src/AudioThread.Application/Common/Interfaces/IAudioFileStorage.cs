using AudioThread.Domain.ValueObjects;

namespace AudioThread.Application.Common.Interfaces;

public interface IAudioFileStorage
{
    Task<StoredAudioFile> UploadAsync(
        AudioFileId audioFileId,
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken);
}

public sealed record StoredAudioFile(
    string Bucket,
    string Path,
    string StoredFileName,
    string? PublicUrl);
