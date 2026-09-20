using System.Diagnostics;
using AudioThread.Application.Common.Interfaces;
using AudioThread.Application.Features.AudioFiles.Jobs;
using AudioThread.Infrastructure.Processing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AudioThread.Infrastructure.Tests.Processing;

public sealed class TplAudioProcessingCoordinatorTests
{
    private static readonly TimeSpan StageDuration = TimeSpan.FromMilliseconds(400);
    private static readonly byte[] AudioContent = [1, 2, 3, 4, 5];

    [Fact]
    public async Task CompleteAsync_RunsCompressionAndSummarizationAtTheSameTime()
    {
        var compressor = new DelayedAudioCompressor(StageDuration);
        var summarizer = new DelayedAudioSummarizer(StageDuration);
        var noiseReducer = new DelayedNoiseReducer(StageDuration);
        var coordinator = CreateCoordinator(compressor, summarizer, noiseReducer);

        await using var session = coordinator.Start(CreateRequest(), CancellationToken.None);
        await session.CompleteAsync();

        Assert.True(
            compressor.StartedAt < summarizer.FinishedAt && summarizer.StartedAt < compressor.FinishedAt,
            "Expected both stages to overlap in time but they ran one after the other.");
        Assert.True(
            noiseReducer.StartedAt < compressor.FinishedAt,
            "Expected noise reduction to overlap with the other stages.");
    }

    [Fact]
    public async Task CompleteAsync_SequentialModeRunsSummarizationAfterCompression()
    {
        var compressor = new DelayedAudioCompressor(StageDuration);
        var summarizer = new DelayedAudioSummarizer(StageDuration);
        var coordinator = CreateCoordinator(
            compressor,
            summarizer,
            new DelayedNoiseReducer(StageDuration),
            AudioProcessingExecutionMode.Sequential);

        await using var session = coordinator.Start(CreateRequest(), CancellationToken.None);
        await session.CompleteAsync();

        Assert.True(
            compressor.FinishedAt <= summarizer.StartedAt,
            "Expected summarization to start only after compression completed.");
        Assert.False(session.AllowsOverlappedStorage);
    }

    [Fact]
    public async Task CompleteAsync_CompletesFasterThanRunningBothStagesSequentially()
    {
        var coordinator = CreateCoordinator(
            new DelayedAudioCompressor(StageDuration),
            new DelayedAudioSummarizer(StageDuration),
            new DelayedNoiseReducer(StageDuration));

        var stopwatch = Stopwatch.StartNew();
        await using var session = coordinator.Start(CreateRequest(), CancellationToken.None);
        var result = await session.CompleteAsync();
        stopwatch.Stop();

        var sequentialDuration = StageDuration + StageDuration + StageDuration;
        Assert.True(
            stopwatch.Elapsed < sequentialDuration,
            $"Expected the parallel pipeline to finish before {sequentialDuration.TotalMilliseconds} ms but it took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
        Assert.True(result.Timings.TimeSaved > TimeSpan.Zero);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsBothResultsAfterSynchronizing()
    {
        var coordinator = CreateCoordinator(
            new DelayedAudioCompressor(TimeSpan.FromMilliseconds(50)),
            new DelayedAudioSummarizer(TimeSpan.FromMilliseconds(50)),
            new DelayedNoiseReducer(TimeSpan.FromMilliseconds(50)));

        await using var session = coordinator.Start(CreateRequest(), CancellationToken.None);
        var result = await session.CompleteAsync();

        Assert.Equal("audio/aac", result.CompressedAudio.ContentType);
        Assert.Equal("Team standup", result.Summary.Summary);
        Assert.Equal("voice-filtered.wav", result.FilteredAudio.FileName);
        Assert.True(result.Timings.WallClockDuration > TimeSpan.Zero);
    }

    [Fact]
    public async Task Compression_CompletesBeforeTheSummarizationStageFinishes()
    {
        var coordinator = CreateCoordinator(
            new DelayedAudioCompressor(TimeSpan.FromMilliseconds(50)),
            new DelayedAudioSummarizer(TimeSpan.FromMilliseconds(400)),
            new DelayedNoiseReducer(TimeSpan.FromMilliseconds(50)));

        await using var session = coordinator.Start(CreateRequest(), CancellationToken.None);
        await session.Compression;

        Assert.False(
            session.Summarization.IsCompleted,
            "Expected the compressed audio to be available while summarization was still running.");

        await session.CompleteAsync();
    }

    [Fact]
    public async Task CompleteAsync_PropagatesTheFailureRaisedInsideAStage()
    {
        var coordinator = CreateCoordinator(
            new FailingAudioCompressor(),
            new DelayedAudioSummarizer(TimeSpan.FromMilliseconds(50)),
            new DelayedNoiseReducer(TimeSpan.FromMilliseconds(50)));

        await using var session = coordinator.Start(CreateRequest(), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(session.CompleteAsync);

        Assert.Equal("FFmpeg is not available.", exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_AggregatesFailuresRaisedOnBothStages()
    {
        var coordinator = CreateCoordinator(
            new FailingAudioCompressor(),
            new FailingAudioSummarizer(),
            new DelayedNoiseReducer(TimeSpan.FromMilliseconds(10)));

        await using var session = coordinator.Start(CreateRequest(), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AggregateException>(session.CompleteAsync);

        Assert.Equal(2, exception.InnerExceptions.Count);
    }

    private static TplAudioProcessingCoordinator CreateCoordinator(
        IAudioCompressor compressor,
        IAudioSummarizer summarizer,
        IAudioNoiseReducer noiseReducer,
        AudioProcessingExecutionMode executionMode = AudioProcessingExecutionMode.Optimized)
    {
        return new TplAudioProcessingCoordinator(
            compressor,
            summarizer,
            noiseReducer,
            Options.Create(new AudioProcessingOptions { ExecutionMode = executionMode }),
            NullLogger<TplAudioProcessingCoordinator>.Instance);
    }

    private static AudioProcessingRequest CreateRequest()
    {
        return new AudioProcessingRequest(AudioContent, "voice.wav", AudioContent.LongLength);
    }

    private sealed class DelayedAudioCompressor(TimeSpan delay) : IAudioCompressor
    {
        public long StartedAt { get; private set; }

        public long FinishedAt { get; private set; }

        public async Task<CompressedAudioFile> CompressToAacAsync(
            Stream content,
            string originalFileName,
            long originalSizeInBytes,
            CancellationToken cancellationToken)
        {
            StartedAt = Stopwatch.GetTimestamp();
            await Task.Delay(delay, cancellationToken);
            FinishedAt = Stopwatch.GetTimestamp();

            return new CompressedAudioFile(
                new MemoryStream([255, 241, 80], writable: false),
                "voice.aac",
                "audio/aac",
                3,
                delay);
        }
    }

    private sealed class DelayedAudioSummarizer(TimeSpan delay) : IAudioSummarizer
    {
        public long StartedAt { get; private set; }

        public long FinishedAt { get; private set; }

        public async Task<AudioSummaryResult> SummarizeAsync(
            Stream content,
            string originalFileName,
            CancellationToken cancellationToken,
            IProgress<AudioProcessingStepUpdate>? progress = null)
        {
            StartedAt = Stopwatch.GetTimestamp();
            await Task.Delay(delay, cancellationToken);
            FinishedAt = Stopwatch.GetTimestamp();

            return new AudioSummaryResult(
                "Team standup",
                "This is the transcript.",
                delay,
                TimeSpan.Zero);
        }
    }

    private sealed class DelayedNoiseReducer(TimeSpan delay) : IAudioNoiseReducer
    {
        public long StartedAt { get; private set; }

        public async Task<FilteredAudioFile> ReduceNoiseAsync(
            Stream content,
            string originalFileName,
            CancellationToken cancellationToken)
        {
            StartedAt = Stopwatch.GetTimestamp();
            await Task.Delay(delay, cancellationToken);

            return new FilteredAudioFile(
                new MemoryStream([82, 73, 70, 70], writable: false),
                "voice-filtered.wav",
                "audio/wav",
                4,
                delay);
        }
    }

    private sealed class FailingAudioCompressor : IAudioCompressor
    {
        public Task<CompressedAudioFile> CompressToAacAsync(
            Stream content,
            string originalFileName,
            long originalSizeInBytes,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("FFmpeg is not available.");
        }
    }

    private sealed class FailingAudioSummarizer : IAudioSummarizer
    {
        public Task<AudioSummaryResult> SummarizeAsync(
            Stream content,
            string originalFileName,
            CancellationToken cancellationToken,
            IProgress<AudioProcessingStepUpdate>? progress = null)
        {
            throw new InvalidOperationException("The summarization model is not available.");
        }
    }
}
