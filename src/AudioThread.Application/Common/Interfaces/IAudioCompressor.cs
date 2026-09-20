namespace AudioThread.Application.Common.Interfaces;

public interface IAudioCompressor
{
    Task<CompressedAudioFile> CompressToAacAsync(
        Stream content,
        string originalFileName,
        long originalSizeInBytes,
        CancellationToken cancellationToken);
}

public sealed record CompressedAudioFile(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeInBytes,
    TimeSpan CompressionDuration);
