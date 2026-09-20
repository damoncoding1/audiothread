namespace AudioThread.Application.Common.Options;

public sealed record AudioUploadOptions
{
    public const string SectionName = "AudioUpload";

    public long MaxFileSizeInBytes { get; init; } = 50 * 1024 * 1024;

    public string[] AllowedExtensions { get; init; } = [".mp3", ".wav"];

    public string[] AllowedContentTypes { get; init; } =
    [
        "audio/mpeg",
        "audio/mp3",
        "audio/wav",
        "audio/wave",
        "audio/x-wav"
    ];
}
