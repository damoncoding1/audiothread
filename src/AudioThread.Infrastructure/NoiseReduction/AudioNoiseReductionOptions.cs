namespace AudioThread.Infrastructure.NoiseReduction;

public sealed class AudioNoiseReductionOptions
{
    public const string SectionName = "AudioNoiseReduction";

    public string PythonExecutablePath { get; set; } = "python3";

    public string ScriptPath { get; set; } = "python/enhance_audio.py";

    public string Model { get; set; } = "DeepFilterNet3";

    public string? ModelCacheDirectory { get; set; }

    public int ModelThreads { get; set; } = 2;

    public int TimeoutSeconds { get; set; } = 600;

    public bool EnablePostFilter { get; set; } = true;
}
