namespace AudioThread.Application.Common.Interfaces;

public interface IAudioNoiseReducer
{
    Task<FilteredAudioFile> ReduceNoiseAsync(
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken);
}

public sealed record FilteredAudioFile(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeInBytes,
    TimeSpan ProcessingDuration);
