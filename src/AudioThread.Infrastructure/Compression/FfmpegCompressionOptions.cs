namespace AudioThread.Infrastructure.Compression;

public sealed class FfmpegCompressionOptions
{
    public const string SectionName = "AudioCompression";

    public string ExecutablePath { get; set; } = "ffmpeg";

    public string AudioBitrate { get; set; } = "128k";

    public int TimeoutSeconds { get; set; } = 120;
}
