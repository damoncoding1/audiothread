using AudioThread.Domain.Enums;
using AudioThread.Domain.ValueObjects;

namespace AudioThread.Domain.Entities;

public sealed class AudioFile
{
    private AudioFile()
    {
        OriginalFileName = string.Empty;
        StoredFileName = string.Empty;
        ContentType = string.Empty;
        StorageBucket = string.Empty;
        StoragePath = string.Empty;
    }

    public AudioFile(
        AudioFileId id,
        string originalFileName,
        string storedFileName,
        string contentType,
        long sizeInBytes,
        string storageBucket,
        string storagePath,
        string? publicUrl,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException("Original file name is required.", nameof(originalFileName));
        }

        if (string.IsNullOrWhiteSpace(storedFileName))
        {
            throw new ArgumentException("Stored file name is required.", nameof(storedFileName));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type is required.", nameof(contentType));
        }

        if (sizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Audio file size must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(storageBucket))
        {
            throw new ArgumentException("Storage bucket is required.", nameof(storageBucket));
        }

        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException("Storage path is required.", nameof(storagePath));
        }

        Id = id;
        OriginalFileName = originalFileName;
        StoredFileName = storedFileName;
        ContentType = contentType;
        SizeInBytes = sizeInBytes;
        StorageBucket = storageBucket;
        StoragePath = storagePath;
        PublicUrl = publicUrl;
        Status = AudioFileStatus.Uploaded;
        CreatedAtUtc = createdAtUtc;
    }

    public AudioFileId Id { get; private set; }

    public string OriginalFileName { get; private set; }

    public string StoredFileName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeInBytes { get; private set; }

    public string StorageBucket { get; private set; }

    public string StoragePath { get; private set; }

    public string? PublicUrl { get; private set; }

    public string? OriginalStoredFileName { get; private set; }

    public string? OriginalContentType { get; private set; }

    public long? OriginalSizeInBytes { get; private set; }

    public string? OriginalStoragePath { get; private set; }

    public string? OriginalPublicUrl { get; private set; }

    public string? FilteredStoredFileName { get; private set; }

    public string? FilteredContentType { get; private set; }

    public long? FilteredSizeInBytes { get; private set; }

    public string? FilteredStoragePath { get; private set; }

    public string? FilteredPublicUrl { get; private set; }

    public AudioFileStatus Status { get; private set; }

    public string? Summary { get; private set; }

    public DateTimeOffset? SummarizedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void AttachSummary(AudioSummary summary, DateTimeOffset summarizedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(summary);

        Summary = summary.Value;
        SummarizedAtUtc = summarizedAtUtc;
        Status = AudioFileStatus.Summarized;
    }

    public void AttachPlaybackVersions(
        string originalStoredFileName,
        string originalContentType,
        long originalSizeInBytes,
        string originalStoragePath,
        string? originalPublicUrl,
        string filteredStoredFileName,
        string filteredContentType,
        long filteredSizeInBytes,
        string filteredStoragePath,
        string? filteredPublicUrl)
    {
        OriginalStoredFileName = Require(originalStoredFileName, nameof(originalStoredFileName));
        OriginalContentType = Require(originalContentType, nameof(originalContentType));
        OriginalSizeInBytes = RequirePositive(originalSizeInBytes, nameof(originalSizeInBytes));
        OriginalStoragePath = Require(originalStoragePath, nameof(originalStoragePath));
        OriginalPublicUrl = originalPublicUrl;

        FilteredStoredFileName = Require(filteredStoredFileName, nameof(filteredStoredFileName));
        FilteredContentType = Require(filteredContentType, nameof(filteredContentType));
        FilteredSizeInBytes = RequirePositive(filteredSizeInBytes, nameof(filteredSizeInBytes));
        FilteredStoragePath = Require(filteredStoragePath, nameof(filteredStoragePath));
        FilteredPublicUrl = filteredPublicUrl;
    }

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("The audio asset value is required.", parameterName)
            : value;

    private static long RequirePositive(long value, string parameterName) =>
        value <= 0
            ? throw new ArgumentOutOfRangeException(parameterName, "The audio asset size must be greater than zero.")
            : value;
}
