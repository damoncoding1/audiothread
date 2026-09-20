using AudioThread.Application.Features.AudioFiles.Queries.ListAudioFiles;
using AudioThread.Domain.Entities;
using AudioThread.Domain.Interfaces;
using AudioThread.Domain.ValueObjects;
using Xunit;

namespace AudioThread.Application.Tests.Features.AudioFiles;

public sealed class ListAudioFilesHandlerTests
{
    [Theory]
    [InlineData(null, null, 0, ListAudioFilesQuery.DefaultTake)]
    [InlineData(-5, 10, 0, 10)]
    [InlineData(40, 500, 40, ListAudioFilesQuery.MaxTake)]
    [InlineData(0, 0, 0, ListAudioFilesQuery.MinTake)]
    public void Create_NormalizesPagingValues(int? skip, int? take, int expectedSkip, int expectedTake)
    {
        var query = ListAudioFilesQuery.Create(skip, take);

        Assert.Equal(expectedSkip, query.Skip);
        Assert.Equal(expectedTake, query.Take);
    }

    [Fact]
    public async Task HandleAsync_ReturnsItemsWithTotalAndPaging()
    {
        var repository = new StubAudioFileRepository(total: 7);
        var handler = new ListAudioFilesHandler(repository);

        var response = await handler.HandleAsync(
            ListAudioFilesQuery.Create(skip: 0, take: 2),
            CancellationToken.None);

        Assert.Equal(7, response.Total);
        Assert.Equal(0, response.Skip);
        Assert.Equal(2, response.Take);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(0, repository.ReceivedSkip);
        Assert.Equal(2, repository.ReceivedTake);
    }

    [Fact]
    public async Task HandleAsync_MapsTheSummaryAndStatusOfEachItem()
    {
        var repository = new StubAudioFileRepository(total: 1);
        var handler = new ListAudioFilesHandler(repository);

        var response = await handler.HandleAsync(
            ListAudioFilesQuery.Create(skip: 0, take: 1),
            CancellationToken.None);

        var item = Assert.Single(response.Items);
        Assert.Equal("voice.wav", item.OriginalFileName);
        Assert.Equal("audio/aac", item.ContentType);
        Assert.Equal("Resumen de prueba", item.Summary);
        Assert.Equal("Summarized", item.Status);
    }

    private sealed class StubAudioFileRepository(int total) : IAudioFileRepository
    {
        public int ReceivedSkip { get; private set; }

        public int ReceivedTake { get; private set; }

        public Task AddAsync(AudioFile audioFile, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<AudioFile?> GetByIdAsync(AudioFileId id, CancellationToken cancellationToken) =>
            Task.FromResult<AudioFile?>(null);

        public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(total);

        public Task<IReadOnlyList<AudioFile>> GetPagedAsync(
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            ReceivedSkip = skip;
            ReceivedTake = take;

            IReadOnlyList<AudioFile> audioFiles =
            [
                .. Enumerable.Range(0, Math.Min(take, total)).Select(_ => CreateAudioFile())
            ];

            return Task.FromResult(audioFiles);
        }

        private static AudioFile CreateAudioFile()
        {
            var audioFile = new AudioFile(
                AudioFileId.New(),
                "voice.wav",
                "voice.aac",
                "audio/aac",
                1024,
                "audio-files",
                $"uploads/{Guid.NewGuid():N}/voice.aac",
                null,
                DateTimeOffset.UtcNow);

            audioFile.AttachSummary(AudioSummary.Create("Resumen de prueba"), DateTimeOffset.UtcNow);

            return audioFile;
        }
    }
}
