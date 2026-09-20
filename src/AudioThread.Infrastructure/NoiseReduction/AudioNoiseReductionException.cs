namespace AudioThread.Infrastructure.NoiseReduction;

public sealed class AudioNoiseReductionException : InvalidOperationException
{
    public AudioNoiseReductionException(string message)
        : base(message)
    {
    }

    public AudioNoiseReductionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
