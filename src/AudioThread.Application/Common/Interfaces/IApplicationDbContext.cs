using AudioThread.Domain.Entities;

namespace AudioThread.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<AudioFile> AudioFiles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
