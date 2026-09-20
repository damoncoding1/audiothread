using AudioThread.Domain.Entities;
using AudioThread.Domain.Interfaces;

namespace AudioThread.Application.Features.AudioFiles.Queries.ListAudioFiles;

public sealed class ListAudioFilesHandler
{
    private readonly IAudioFileRepository _repository;

    public ListAudioFilesHandler(IAudioFileRepository repository)
    {
        _repository = repository;
    }

    public async Task<ListAudioFilesResponse> HandleAsync(
        ListAudioFilesQuery query,
        CancellationToken cancellationToken)
    {
        var total = await _repository.CountAsync(cancellationToken);
        var audioFiles = await _repository.GetPagedAsync(query.Skip, query.Take, cancellationToken);

        return new ListAudioFilesResponse(
            [.. audioFiles.Select(ToListItem)],
            total,
            query.Skip,
            query.Take);
    }

    private static AudioFileListItemDto ToListItem(AudioFile audioFile)
    {
        return new AudioFileListItemDto(
            audioFile.Id.Value,
            audioFile.OriginalFileName,
            audioFile.ContentType,
            audioFile.SizeInBytes,
            audioFile.PublicUrl,
            audioFile.OriginalPublicUrl ?? audioFile.PublicUrl,
            audioFile.FilteredPublicUrl,
            audioFile.Summary,
            audioFile.Status.ToString(),
            audioFile.CreatedAtUtc);
    }
}
