namespace AudioThread.Infrastructure.Compression;

public sealed class AudioCompressionException : InvalidOperationException
{
    public AudioCompressionException(string message)
        : base(message)
    {
    }

    public AudioCompressionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
