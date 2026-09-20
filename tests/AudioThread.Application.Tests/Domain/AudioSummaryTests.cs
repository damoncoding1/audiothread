using AudioThread.Domain.ValueObjects;
using Xunit;

namespace AudioThread.Application.Tests.Domain;

public sealed class AudioSummaryTests
{
    private const string LongModelOutput =
        "The speaker explains in great detail how the parallel audio pipeline compresses the recording while a second thread extracts the transcript and produces the summary, and then describes how both threads synchronize before the result is stored in the database.";

    [Fact]
    public void FromModelOutput_KeepsSummaryWithinTheDefaultLimit()
    {
        var summary = AudioSummary.FromModelOutput(LongModelOutput);

        Assert.True(
            summary.Value.Length <= AudioSummary.DefaultMaxLength,
            $"Expected at most {AudioSummary.DefaultMaxLength} characters but got {summary.Value.Length}.");
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(120)]
    [InlineData(150)]
    [InlineData(250)]
    public void FromModelOutput_KeepsSummaryWithinTheConfiguredLimit(int maxLength)
    {
        var summary = AudioSummary.FromModelOutput(LongModelOutput, maxLength);

        Assert.True(
            summary.Value.Length <= maxLength,
            $"Expected at most {maxLength} characters but got {summary.Value.Length}.");
        Assert.Equal(maxLength, summary.MaxLength);
    }

    [Fact]
    public void FromModelOutput_DoesNotCutInTheMiddleOfAWord()
    {
        var summary = AudioSummary.FromModelOutput(LongModelOutput);

        Assert.DoesNotContain("  ", summary.Value, StringComparison.Ordinal);
        Assert.Equal(summary.Value.Trim(), summary.Value);
    }

    [Fact]
    public void FromModelOutput_RemovesSurroundingQuotesAndCollapsesWhitespace()
    {
        var summary = AudioSummary.FromModelOutput("  \"Weekly   team\nstandup\"  ");

        Assert.Equal("Weekly team standup", summary.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromModelOutput_FallsBackWhenTheModelReturnsNothing(string? modelOutput)
    {
        var summary = AudioSummary.FromModelOutput(modelOutput);

        Assert.Equal(AudioSummary.NoSpeechDetected, summary);
        Assert.True(summary.Value.Length <= AudioSummary.DefaultMaxLength);
    }

    [Fact]
    public void Create_RejectsSummariesLongerThanTheLimit()
    {
        var tooLong = new string('a', AudioSummary.DefaultMaxLength + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => AudioSummary.Create(tooLong));
    }

    [Fact]
    public void Create_AcceptsSummariesExactlyAtTheLimit()
    {
        var exactLength = new string('a', AudioSummary.DefaultMaxLength);

        var summary = AudioSummary.Create(exactLength);

        Assert.Equal(AudioSummary.DefaultMaxLength, summary.Value.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(AudioSummary.AbsoluteMaxLength + 1)]
    public void FromModelOutput_RejectsUnsupportedLimits(int maxLength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AudioSummary.FromModelOutput(LongModelOutput, maxLength));
    }
}
