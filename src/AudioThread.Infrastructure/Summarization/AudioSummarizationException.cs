namespace AudioThread.Infrastructure.Summarization;

public sealed class AudioSummarizationException : Exception
{
    public AudioSummarizationException(string message)
        : base(message)
    {
    }

    public AudioSummarizationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
