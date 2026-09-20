using AudioThread.Domain.Interfaces;
using AudioThread.Domain.ValueObjects;

namespace AudioThread.Application.Features.AudioFiles.Queries.GetAudioFileById;

public sealed class GetAudioFileByIdHandler
{
    private readonly IAudioFileRepository _repository;

    public GetAudioFileByIdHandler(IAudioFileRepository repository)
    {
        _repository = repository;
    }

    public async Task<AudioFileDto?> HandleAsync(GetAudioFileByIdQuery query, CancellationToken cancellationToken)
    {
        var audioFile = await _repository.GetByIdAsync(AudioFileId.From(query.Id), cancellationToken);
        if (audioFile is null)
        {
            return null;
        }

        return new AudioFileDto(
            audioFile.Id.Value,
            audioFile.OriginalFileName,
            audioFile.StoredFileName,
            audioFile.ContentType,
            audioFile.SizeInBytes,
            audioFile.StorageBucket,
            audioFile.StoragePath,
            audioFile.PublicUrl,
            audioFile.OriginalPublicUrl ?? audioFile.PublicUrl,
            audioFile.FilteredPublicUrl,
            audioFile.Status.ToString(),
            audioFile.Summary,
            audioFile.SummarizedAtUtc,
            audioFile.CreatedAtUtc);
    }
}
