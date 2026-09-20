using System.Text.RegularExpressions;

namespace AudioThread.Domain.ValueObjects;

public sealed partial record AudioSummary
{
    public const int DefaultMaxLength = 150;

    public const int AbsoluteMaxLength = 512;

    private AudioSummary(string value, int maxLength)
    {
        Value = value;
        MaxLength = maxLength;
    }

    public string Value { get; }

    public int MaxLength { get; }

    public static AudioSummary NoSpeechDetected { get; } =
        new("No speech detected in the audio", DefaultMaxLength);

    public static AudioSummary Create(string value, int maxLength = DefaultMaxLength)
    {
        EnsureSupportedLimit(maxLength);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Audio summary is required.", nameof(value));
        }

        var normalized = Normalize(value);
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Audio summary cannot exceed {maxLength} characters.");
        }

        return new AudioSummary(normalized, maxLength);
    }

    public static AudioSummary FromModelOutput(string? value, int maxLength = DefaultMaxLength)
    {
        EnsureSupportedLimit(maxLength);

        if (string.IsNullOrWhiteSpace(value))
        {
            return NoSpeechDetected;
        }

        var normalized = Normalize(value);
        return normalized.Length == 0
            ? NoSpeechDetected
            : new AudioSummary(Shorten(normalized, maxLength), maxLength);
    }

    public override string ToString() => Value;

    private static void EnsureSupportedLimit(int maxLength)
    {
        if (maxLength is <= 0 or > AbsoluteMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxLength),
                $"Audio summary length must be between 1 and {AbsoluteMaxLength} characters.");
        }
    }

    private static string Normalize(string value)
    {
        var collapsed = WhitespaceRegex().Replace(value, " ").Trim();
        return collapsed.Trim('"', '\'', '«', '»', '“', '”').Trim();
    }

    private static string Shorten(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var truncated = value[..maxLength];
        var lastSeparator = truncated.LastIndexOf(' ');

        if (lastSeparator > maxLength / 2)
        {
            truncated = truncated[..lastSeparator];
        }

        return truncated.TrimEnd(' ', ',', ';', ':', '.', '-');
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
