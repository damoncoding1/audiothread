using System.Diagnostics;
using AudioThread.Application.Common.Interfaces;
using AudioThread.Application.Common.Processing;
using AudioThread.Application.Features.AudioFiles.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AudioThread.Infrastructure.Processing;

public sealed class TplAudioProcessingCoordinator : IAudioProcessingCoordinator
{
    private readonly IAudioCompressor _compressor;
    private readonly IAudioSummarizer _summarizer;
    private readonly IAudioNoiseReducer _noiseReducer;
    private readonly ILogger<TplAudioProcessingCoordinator> _logger;
    private readonly AudioProcessingOptions _options;

    public TplAudioProcessingCoordinator(
        IAudioCompressor compressor,
        IAudioSummarizer summarizer,
        IAudioNoiseReducer noiseReducer,
        IOptions<AudioProcessingOptions> options,
        ILogger<TplAudioProcessingCoordinator> logger)
    {
        _compressor = compressor;
        _summarizer = summarizer;
        _noiseReducer = noiseReducer;
        _options = options.Value;
        _logger = logger;
    }

    public AudioProcessingSession Start(
        AudioProcessingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var compression = CompressAsync(request, cancellationToken);

        if (_options.ExecutionMode == AudioProcessingExecutionMode.Sequential)
        {
            _logger.LogInformation(
                "Starting the sequential audio pipeline for {FileName}",
                request.FileName);

            var summarization = SummarizeAfterCompressionAsync(
                compression,
                request,
                cancellationToken);

            return new AudioProcessingSession(
                compression,
                summarization,
                ReduceNoiseAfterSummarizationAsync(summarization, request, cancellationToken),
                allowsOverlappedStorage: false,
                onCompleted: timings => LogPipelineCompleted(
                    AudioProcessingExecutionMode.Sequential,
                    request.FileName,
                    timings));
        }

        _logger.LogInformation(
            "Starting the optimized audio pipeline for {FileName}",
            request.FileName);

        return new AudioProcessingSession(
            compression,
            SummarizeAsync(request, cancellationToken),
            ReduceNoiseAsync(request, cancellationToken),
            allowsOverlappedStorage: true,
            onCompleted: timings => LogPipelineCompleted(
                AudioProcessingExecutionMode.Optimized,
                request.FileName,
                timings));
    }

    private void LogPipelineCompleted(
        AudioProcessingExecutionMode executionMode,
        string fileName,
        AudioProcessingTimings timings)
    {
        _logger.LogInformation(
            "{ExecutionMode} audio pipeline completed for {FileName} in {WallClockMilliseconds} ms. Stage sum: {SequentialMilliseconds} ms. Overlap saved: {SavedMilliseconds} ms",
            executionMode,
            fileName,
            (long)timings.WallClockDuration.TotalMilliseconds,
            (long)timings.SequentialDuration.TotalMilliseconds,
            (long)timings.TimeSaved.TotalMilliseconds);
    }

    private async Task<AudioProcessingStage<AudioSummaryResult>> SummarizeAfterCompressionAsync(
        Task<AudioProcessingStage<CompressedAudioFile>> compression,
        AudioProcessingRequest request,
        CancellationToken cancellationToken)
    {
        await compression;
        return await SummarizeAsync(request, cancellationToken);
    }

    private async Task<AudioProcessingStage<FilteredAudioFile>> ReduceNoiseAfterSummarizationAsync(
        Task<AudioProcessingStage<AudioSummaryResult>> summarization,
        AudioProcessingRequest request,
        CancellationToken cancellationToken)
    {
        await summarization;
        return await ReduceNoiseAsync(request, cancellationToken);
    }

    private async Task<AudioProcessingStage<CompressedAudioFile>> CompressAsync(
        AudioProcessingRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var progress = request.Progress;

        progress?.Report(new AudioProcessingStepUpdate(
            AudioProcessingSteps.Compression,
            AudioProcessingStepState.Running));

        try
        {
            using var content = new MemoryStream(request.Content, writable: false);
            var compressed = await _compressor.CompressToAacAsync(
                content,
                request.FileName,
                request.SizeInBytes,
                cancellationToken);

            stopwatch.Stop();

            progress?.Report(new AudioProcessingStepUpdate(
                AudioProcessingSteps.Compression,
                AudioProcessingStepState.Completed,
                stopwatch.Elapsed));

            _logger.LogInformation(
                "Compression finished for {FileName} in {ElapsedMilliseconds} ms",
                request.FileName,
                (long)stopwatch.Elapsed.TotalMilliseconds);

            return new AudioProcessingStage<CompressedAudioFile>(compressed, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            progress?.Report(new AudioProcessingStepUpdate(
                AudioProcessingSteps.Compression,
                AudioProcessingStepState.Failed));

            _logger.LogError(exception, "Compression failed for {FileName}", request.FileName);
            throw;
        }
    }

    private async Task<AudioProcessingStage<AudioSummaryResult>> SummarizeAsync(
        AudioProcessingRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var content = new MemoryStream(request.Content, writable: false);
            var summary = await _summarizer.SummarizeAsync(
                content,
                request.FileName,
                cancellationToken,
                request.Progress);

            stopwatch.Stop();

            _logger.LogInformation(
                "Summarization finished for {FileName} in {ElapsedMilliseconds} ms. Transcription: {TranscriptionMilliseconds} ms. Summary generation: {SummaryMilliseconds} ms",
                request.FileName,
                (long)stopwatch.Elapsed.TotalMilliseconds,
                (long)summary.TranscriptionDuration.TotalMilliseconds,
                (long)summary.SummarizationDuration.TotalMilliseconds);

            return new AudioProcessingStage<AudioSummaryResult>(summary, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            _logger.LogError(exception, "Summarization failed for {FileName}", request.FileName);
            throw;
        }
    }

    private async Task<AudioProcessingStage<FilteredAudioFile>> ReduceNoiseAsync(
        AudioProcessingRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        request.Progress?.Report(new AudioProcessingStepUpdate(
            AudioProcessingSteps.NoiseReduction,
            AudioProcessingStepState.Running));

        try
        {
            using var content = new MemoryStream(request.Content, writable: false);
            var filtered = await _noiseReducer.ReduceNoiseAsync(
                content,
                request.FileName,
                cancellationToken);

            stopwatch.Stop();

            request.Progress?.Report(new AudioProcessingStepUpdate(
                AudioProcessingSteps.NoiseReduction,
                AudioProcessingStepState.Completed,
                stopwatch.Elapsed));

            _logger.LogInformation(
                "DeepFilterNet noise reduction finished for {FileName} in {ElapsedMilliseconds} ms",
                request.FileName,
                (long)stopwatch.Elapsed.TotalMilliseconds);

            return new AudioProcessingStage<FilteredAudioFile>(filtered, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            request.Progress?.Report(new AudioProcessingStepUpdate(
                AudioProcessingSteps.NoiseReduction,
                AudioProcessingStepState.Failed));

            _logger.LogError(exception, "Noise reduction failed for {FileName}", request.FileName);
            throw;
        }
    }
}
