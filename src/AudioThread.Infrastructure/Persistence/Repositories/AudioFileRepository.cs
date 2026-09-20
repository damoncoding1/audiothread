using AudioThread.Domain.Entities;
using AudioThread.Domain.Interfaces;
using AudioThread.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AudioThread.Infrastructure.Persistence.Repositories;

public sealed class AudioFileRepository : IAudioFileRepository
{
    private readonly AudioThreadDbContext _dbContext;

    public AudioFileRepository(AudioThreadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AudioFile audioFile, CancellationToken cancellationToken)
    {
        await _dbContext.AudioFiles.AddAsync(audioFile, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<AudioFile?> GetByIdAsync(AudioFileId id, CancellationToken cancellationToken)
    {
        return _dbContext.AudioFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(audioFile => audioFile.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AudioFile>> GetPagedAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await _dbContext.AudioFiles
            .AsNoTracking()
            .OrderByDescending(audioFile => audioFile.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return _dbContext.AudioFiles.CountAsync(cancellationToken);
    }
}
