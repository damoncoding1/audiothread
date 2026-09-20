using AudioThread.Application.Common.Processing;
using AudioThread.Application.Features.AudioFiles.Jobs;

namespace AudioThread.Application.Common.Interfaces;

public interface IAudioProcessingCoordinator
{
    AudioProcessingSession Start(
        AudioProcessingRequest request,
        CancellationToken cancellationToken);
}

public sealed record AudioProcessingRequest(
    byte[] Content,
    string FileName,
    long SizeInBytes)
{
    public IProgress<AudioProcessingStepUpdate>? Progress { get; init; }
}

public sealed record AudioProcessingStage<T>(T Value, TimeSpan Duration);

public sealed record AudioProcessingResult(
    CompressedAudioFile CompressedAudio,
    AudioSummaryResult Summary,
    FilteredAudioFile FilteredAudio,
    AudioProcessingTimings Timings);

public sealed record AudioProcessingTimings(
    TimeSpan CompressionDuration,
    TimeSpan SummarizationDuration,
    TimeSpan NoiseReductionDuration,
    TimeSpan WallClockDuration)
{
    public TimeSpan SequentialDuration =>
        CompressionDuration + SummarizationDuration + NoiseReductionDuration;

    public TimeSpan TimeSaved
    {
        get
        {
            var saved = SequentialDuration - WallClockDuration;
            return saved > TimeSpan.Zero ? saved : TimeSpan.Zero;
        }
    }
}
