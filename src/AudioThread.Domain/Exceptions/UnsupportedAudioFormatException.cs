namespace AudioThread.Domain.Exceptions;

public sealed class UnsupportedAudioFormatException(string extension)
    : InvalidOperationException($"Audio format '{extension}' is not supported.")
{
}
