namespace AudioThread.Infrastructure.Processing;

public sealed class AudioProcessingOptions
{
    public const string SectionName = "AudioProcessing";

    public AudioProcessingExecutionMode ExecutionMode { get; set; } =
        AudioProcessingExecutionMode.Optimized;

    public int MaxConcurrentJobs { get; set; } = 2;

    public int QueueCapacity { get; set; } = 8;
}

public enum AudioProcessingExecutionMode
{
    Sequential,
    Optimized
}
