namespace AudioThread.Application.Features.AudioFiles.Queries.GetAudioFileById;

public sealed record AudioFileDto(
    Guid Id,
    string OriginalFileName,
    string StoredFileName,
    string ContentType,
    long SizeInBytes,
    string StorageBucket,
    string StoragePath,
    string? Url,
    string? OriginalUrl,
    string? FilteredUrl,
    string Status,
    string? Summary,
    DateTimeOffset? SummarizedAtUtc,
    DateTimeOffset CreatedAtUtc);
