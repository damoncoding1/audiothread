namespace AudioThread.Application.Features.AudioFiles.Queries.ListAudioFiles;

public sealed record AudioFileListItemDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeInBytes,
    string? Url,
    string? OriginalUrl,
    string? FilteredUrl,
    string? Summary,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record ListAudioFilesResponse(
    IReadOnlyList<AudioFileListItemDto> Items,
    int Total,
    int Skip,
    int Take);
