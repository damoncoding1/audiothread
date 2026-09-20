using AudioThread.Domain.ValueObjects;

namespace AudioThread.Application.Common.Options;

public sealed record AudioSummaryOptions
{
    public const string SectionName = "AudioSummarization";

    public int MaxLength { get; init; } = AudioSummary.DefaultMaxLength;
}
