using System.Diagnostics;
using AudioThread.Application.Common.Interfaces;

namespace AudioThread.Application.Common.Processing;

public sealed class AudioProcessingSession : IAsyncDisposable
{
    private readonly Task<AudioProcessingStage<CompressedAudioFile>> _compression;
    private readonly Task<AudioProcessingStage<AudioSummaryResult>> _summarization;
    private readonly Task<AudioProcessingStage<FilteredAudioFile>> _noiseReduction;
    private readonly Action<AudioProcessingTimings>? _onCompleted;
    private readonly Stopwatch _wallClock = Stopwatch.StartNew();

    public AudioProcessingSession(
        Task<AudioProcessingStage<CompressedAudioFile>> compression,
        Task<AudioProcessingStage<AudioSummaryResult>> summarization,
        Task<AudioProcessingStage<FilteredAudioFile>> noiseReduction,
        bool allowsOverlappedStorage = true,
        Action<AudioProcessingTimings>? onCompleted = null)
    {
        _compression = compression;
        _summarization = summarization;
        _noiseReduction = noiseReduction;
        AllowsOverlappedStorage = allowsOverlappedStorage;
        _onCompleted = onCompleted;
    }

    public Task<AudioProcessingStage<CompressedAudioFile>> Compression => _compression;

    public Task<AudioProcessingStage<AudioSummaryResult>> Summarization => _summarization;

    public Task<AudioProcessingStage<FilteredAudioFile>> NoiseReduction => _noiseReduction;

    public bool AllowsOverlappedStorage { get; }

    public async Task<AudioProcessingResult> CompleteAsync()
    {
        var stages = Task.WhenAll(_compression, _summarization, _noiseReduction);

        try
        {
            await stages;
        }
        catch
        {
            _wallClock.Stop();
            throw DescribeFailure(stages);
        }

        _wallClock.Stop();

        var compression = await _compression;
        var summarization = await _summarization;
        var noiseReduction = await _noiseReduction;

        var timings = new AudioProcessingTimings(
            compression.Duration,
            summarization.Duration,
            noiseReduction.Duration,
            _wallClock.Elapsed);

        _onCompleted?.Invoke(timings);

        return new AudioProcessingResult(
            compression.Value,
            summarization.Value,
            noiseReduction.Value,
            timings);
    }

    public async ValueTask DisposeAsync()
    {
        await ObserveAsync(_compression);
        await ObserveAsync(_summarization);
        await ObserveAsync(_noiseReduction);

        if (_compression.IsCompletedSuccessfully)
        {
            await _compression.Result.Value.Content.DisposeAsync();
        }

        if (_noiseReduction.IsCompletedSuccessfully)
        {
            await _noiseReduction.Result.Value.Content.DisposeAsync();
        }
    }

    private static Exception DescribeFailure(Task stages)
    {
        var failures = stages.Exception?.InnerExceptions;

        if (failures is null || failures.Count == 0)
        {
            return new TaskCanceledException("The audio pipeline was cancelled before producing a result.");
        }

        if (failures.Count == 1)
        {
            return failures[0];
        }

        return new AggregateException(
            "The audio pipeline failed on more than one stage.",
            failures);
    }

    private static async Task ObserveAsync(Task stage)
    {
        try
        {
            await stage;
        }
        catch (Exception)
        {
        }
    }
}
