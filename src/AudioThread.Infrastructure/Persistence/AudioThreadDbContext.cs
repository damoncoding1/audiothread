using AudioThread.Application.Common.Interfaces;
using AudioThread.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AudioThread.Infrastructure.Persistence;

public sealed class AudioThreadDbContext : DbContext, IApplicationDbContext
{
    public AudioThreadDbContext(DbContextOptions<AudioThreadDbContext> options)
        : base(options)
    {
    }

    public DbSet<AudioFile> AudioFiles => Set<AudioFile>();

    IQueryable<AudioFile> IApplicationDbContext.AudioFiles => AudioFiles;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AudioThreadDbContext).Assembly);
    }
}
