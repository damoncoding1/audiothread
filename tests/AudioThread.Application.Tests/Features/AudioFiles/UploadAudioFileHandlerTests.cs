using System.Diagnostics;
using AudioThread.Application.Common.Interfaces;
using AudioThread.Application.Common.Options;
using AudioThread.Application.Common.Processing;
using AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;
using AudioThread.Domain.Entities;
using AudioThread.Domain.Enums;
using AudioThread.Domain.Interfaces;
using AudioThread.Domain.ValueObjects;
using Xunit;

namespace AudioThread.Application.Tests.Features.AudioFiles;

public sealed class UploadAudioFileHandlerTests
{
    private const string LongModelSummary =
        "The team discussed the quarterly roadmap, the hiring plan, the migration to the new storage provider, the pending security review and the budget approved for the next release cycle.";

    private static readonly byte[] OriginalAudio = [10, 20, 30, 40, 50];
    private static readonly byte[] CompressedAudio = [255, 241, 80];
    private static readonly byte[] FilteredAudio = [82, 73, 70, 70];

    [Fact]
    public async Task HandleAsync_UploadsCompressedAacInsteadOfOriginalAudio()
    {
        var storage = new CapturingAudioFileStorage();
        var repository = new CapturingAudioFileRepository();
        var handler = CreateHandler(new StubAudioProcessingCoordinator(), storage, repository);

        var response = await HandleUploadAsync(handler);

        var compressedUpload = Assert.Single(storage.Uploads, upload => upload.FileName == "voice.aac");
        Assert.Equal("audio/aac", compressedUpload.ContentType);
        Assert.Equal(CompressedAudio, compressedUpload.Content);
        Assert.Equal("audio/aac", response.ContentType);
        Assert.Equal(CompressedAudio.LongLength, response.SizeInBytes);
        Assert.EndsWith(".aac", response.StoragePath, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(repository.AddedAudioFile);
        Assert.Equal("voice.wav", repository.AddedAudioFile.OriginalFileName);
        Assert.Equal("audio/aac", repository.AddedAudioFile.ContentType);
        Assert.NotNull(repository.AddedAudioFile.OriginalStoragePath);
        Assert.NotNull(repository.AddedAudioFile.FilteredStoragePath);
    }

    [Fact]
    public async Task HandleAsync_PersistsTheExtractedSummary()
    {
        var storage = new CapturingAudioFileStorage();
        var repository = new CapturingAudioFileRepository();
        var coordinator = new StubAudioProcessingCoordinator(summary: "Weekly team standup");
        var handler = CreateHandler(coordinator, storage, repository);

        var response = await HandleUploadAsync(handler);

        Assert.Equal("Weekly team standup", response.Summary);
        Assert.NotNull(repository.AddedAudioFile);
        Assert.Equal("Weekly team standup", repository.AddedAudioFile.Summary);
        Assert.Equal(AudioFileStatus.Summarized, repository.AddedAudioFile.Status);
        Assert.NotNull(repository.AddedAudioFile.SummarizedAtUtc);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(150)]
    public async Task HandleAsync_TruncatesSummariesLongerThanTheConfiguredLimit(int maxLength)
    {
        var storage = new CapturingAudioFileStorage();
        var repository = new CapturingAudioFileRepository();
        var coordinator = new StubAudioProcessingCoordinator(summary: LongModelSummary);
        var handler = CreateHandler(
            coordinator,
            storage,
            repository,
            new AudioSummaryOptions { MaxLength = maxLength });

        var response = await HandleUploadAsync(handler);

        Assert.NotNull(response.Summary);
        Assert.True(
            response.Summary.Length <= maxLength,
            $"Expected at most {maxLength} characters but got {response.Summary.Length}.");
    }

    [Fact]
    public async Task HandleAsync_UsesTheDefaultLimitWhenNoneIsConfigured()
    {
        var storage = new CapturingAudioFileStorage();
        var repository = new CapturingAudioFileRepository();
        var coordinator = new StubAudioProcessingCoordinator(summary: LongModelSummary);
        var handler = CreateHandler(coordinator, storage, repository);

        var response = await HandleUploadAsync(handler);

        Assert.NotNull(response.Summary);
        Assert.True(
            response.Summary.Length <= AudioSummary.DefaultMaxLength,
            $"Expected at most {AudioSummary.DefaultMaxLength} characters but got {response.Summary.Length}.");
    }

    [Fact]
    public async Task HandleAsync_ForwardsTheWholeOriginalAudioToThePipeline()
    {
        var coordinator = new StubAudioProcessingCoordinator();
        var handler = CreateHandler(coordinator, new CapturingAudioFileStorage(), new CapturingAudioFileRepository());

        await HandleUploadAsync(handler);

        Assert.NotNull(coordinator.ReceivedRequest);
        Assert.Equal(OriginalAudio, coordinator.ReceivedRequest.Content);
        Assert.Equal("voice.wav", coordinator.ReceivedRequest.FileName);
    }

    [Fact]
    public async Task HandleAsync_StartsTheStorageUploadWhileSummarizationIsStillRunning()
    {
        var storage = new CapturingAudioFileStorage();
        var coordinator = new StubAudioProcessingCoordinator(
            summarizationDelay: TimeSpan.FromMilliseconds(300));
        var handler = CreateHandler(coordinator, storage, new CapturingAudioFileRepository());

        await HandleUploadAsync(handler);

        Assert.NotNull(storage.UploadStartedAt);
        Assert.True(
            storage.UploadStartedAt < coordinator.SummarizationFinishedAt,
            "Expected the storage upload to start before the summarization stage finished.");
    }

    [Fact]
    public async Task HandleAsync_SequentialSessionUploadsAfterSummarizationFinishes()
    {
        var storage = new CapturingAudioFileStorage();
        var coordinator = new StubAudioProcessingCoordinator(
            summarizationDelay: TimeSpan.FromMilliseconds(100),
            allowsOverlappedStorage: false);
        var handler = CreateHandler(coordinator, storage, new CapturingAudioFileRepository());

        await HandleUploadAsync(handler);

        Assert.NotNull(storage.UploadStartedAt);
        Assert.True(
            storage.UploadStartedAt >= coordinator.SummarizationFinishedAt,
            "Expected the sequential session to upload only after summarization finished.");
    }

    private static UploadAudioFileHandler CreateHandler(
        IAudioProcessingCoordinator coordinator,
        IAudioFileStorage storage,
        IAudioFileRepository repository,
        AudioSummaryOptions? summaryOptions = null)
    {
        return new UploadAudioFileHandler(
            coordinator,
            storage,
            repository,
            new UploadAudioFileValidator(new AudioUploadOptions()),
            summaryOptions ?? new AudioSummaryOptions());
    }

    private static async Task<UploadAudioFileResponse> HandleUploadAsync(UploadAudioFileHandler handler)
    {
        await using var originalStream = new MemoryStream(OriginalAudio);

        return await handler.HandleAsync(
            new UploadAudioFileCommand(originalStream, "voice.wav", "audio/wav", OriginalAudio.LongLength),
            CancellationToken.None);
    }

    private sealed class StubAudioProcessingCoordinator(
        string summary = "Team standup",
        TimeSpan? summarizationDelay = null,
        bool allowsOverlappedStorage = true) : IAudioProcessingCoordinator
    {
        public AudioProcessingRequest? ReceivedRequest { get; private set; }

        public long SummarizationFinishedAt { get; private set; }

        public AudioProcessingSession Start(
            AudioProcessingRequest request,
            CancellationToken cancellationToken)
        {
            ReceivedRequest = request;

            return new AudioProcessingSession(
                CompressAsync(),
                SummarizeAsync(cancellationToken),
                FilterAsync(),
                allowsOverlappedStorage);
        }

        private static Task<AudioProcessingStage<FilteredAudioFile>> FilterAsync()
        {
            var filtered = new FilteredAudioFile(
                new MemoryStream(FilteredAudio, writable: false),
                "voice-filtered.wav",
                "audio/wav",
                FilteredAudio.LongLength,
                TimeSpan.FromMilliseconds(50));

            return Task.FromResult(
                new AudioProcessingStage<FilteredAudioFile>(filtered, TimeSpan.FromMilliseconds(50)));
        }

        private static Task<AudioProcessingStage<CompressedAudioFile>> CompressAsync()
        {
            var compressed = new CompressedAudioFile(
                new MemoryStream(CompressedAudio, writable: false),
                "voice.aac",
                "audio/aac",
                CompressedAudio.LongLength,
                TimeSpan.FromMilliseconds(25));

            return Task.FromResult(
                new AudioProcessingStage<CompressedAudioFile>(compressed, TimeSpan.FromMilliseconds(25)));
        }

        private async Task<AudioProcessingStage<AudioSummaryResult>> SummarizeAsync(
            CancellationToken cancellationToken)
        {
            if (summarizationDelay is { } delay)
            {
                await Task.Delay(delay, cancellationToken);
            }

            var summaryResult = new AudioSummaryResult(
                summary,
                "This is the full transcript of the recording.",
                TimeSpan.FromMilliseconds(120),
                TimeSpan.FromMilliseconds(80));

            SummarizationFinishedAt = Stopwatch.GetTimestamp();

            return new AudioProcessingStage<AudioSummaryResult>(
                summaryResult,
                TimeSpan.FromMilliseconds(200));
        }
    }

    private sealed class CapturingAudioFileStorage : IAudioFileStorage
    {
        public List<CapturedUpload> Uploads { get; } = [];

        public long? UploadStartedAt { get; private set; }

        public async Task<StoredAudioFile> UploadAsync(
            AudioFileId audioFileId,
            Stream content,
            string originalFileName,
            string contentType,
            CancellationToken cancellationToken)
        {
            UploadStartedAt = Stopwatch.GetTimestamp();

            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Uploads.Add(new CapturedUpload(buffer.ToArray(), originalFileName, contentType));

            return new StoredAudioFile(
                "audio-files",
                $"uploads/{audioFileId.Value:N}/{originalFileName}",
                originalFileName,
                $"https://example.test/{originalFileName}");
        }

        public sealed record CapturedUpload(byte[] Content, string FileName, string ContentType);
    }

    private sealed class CapturingAudioFileRepository : IAudioFileRepository
    {
        public AudioFile? AddedAudioFile { get; private set; }

        public Task AddAsync(AudioFile audioFile, CancellationToken cancellationToken)
        {
            AddedAudioFile = audioFile;
            return Task.CompletedTask;
        }

        public Task<AudioFile?> GetByIdAsync(AudioFileId id, CancellationToken cancellationToken)
        {
            return Task.FromResult<AudioFile?>(null);
        }

        public Task<IReadOnlyList<AudioFile>> GetPagedAsync(
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AudioFile> audioFiles = AddedAudioFile is null ? [] : [AddedAudioFile];

            return Task.FromResult(audioFiles);
        }

        public Task<int> CountAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(AddedAudioFile is null ? 0 : 1);
        }
    }
}
