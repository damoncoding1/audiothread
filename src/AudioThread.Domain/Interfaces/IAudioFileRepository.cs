using AudioThread.Domain.Entities;
using AudioThread.Domain.ValueObjects;

namespace AudioThread.Domain.Interfaces;

public interface IAudioFileRepository
{
    Task AddAsync(AudioFile audioFile, CancellationToken cancellationToken);

    Task<AudioFile?> GetByIdAsync(AudioFileId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AudioFile>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);
}
