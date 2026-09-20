namespace AudioThread.Api.Contracts.AudioFiles;

public sealed record AudioFileResponse(
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
