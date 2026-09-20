namespace AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;

public sealed record UploadAudioFileResponse(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeInBytes,
    string StorageBucket,
    string StoragePath,
    string? Url,
    string? OriginalUrl,
    string? FilteredUrl,
    string? Summary,
    DateTimeOffset CreatedAtUtc);
