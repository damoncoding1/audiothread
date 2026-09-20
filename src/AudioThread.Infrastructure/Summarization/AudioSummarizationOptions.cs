using AudioThread.Domain.ValueObjects;

namespace AudioThread.Infrastructure.Summarization;

public sealed class AudioSummarizationOptions
{
    public const string SectionName = "AudioSummarization";

    public string PythonExecutablePath { get; set; } = "python3";

    public string ScriptPath { get; set; } = "python/summarize_audio.py";

    public string SpeechToTextModel { get; set; } = "tiny";

    public string SummarizationModel { get; set; } = "Qwen/Qwen2.5-1.5B-Instruct";

    public string ComputeType { get; set; } = "int8";

    public string TorchDataType { get; set; } = "bfloat16";

    public string? Language { get; set; }

    public int MaxSummaryLength { get; set; } = AudioSummary.DefaultMaxLength;

    public int ModelThreads { get; set; } = 2;

    public int TimeoutSeconds { get; set; } = 600;

    public string? ModelCacheDirectory { get; set; }
}
