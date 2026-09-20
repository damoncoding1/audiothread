using AudioThread.Application.Features.AudioFiles.Jobs;

namespace AudioThread.Application.Common.Interfaces;

public interface IAudioSummarizer
{
    Task<AudioSummaryResult> SummarizeAsync(
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken,
        IProgress<AudioProcessingStepUpdate>? progress = null);
}

public sealed record AudioSummaryResult(
    string Summary,
    string Transcript,
    TimeSpan TranscriptionDuration,
    TimeSpan SummarizationDuration);
