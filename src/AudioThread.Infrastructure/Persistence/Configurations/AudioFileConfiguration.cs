using AudioThread.Domain.Entities;
using AudioThread.Domain.Enums;
using AudioThread.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AudioThread.Infrastructure.Persistence.Configurations;

public sealed class AudioFileConfiguration : IEntityTypeConfiguration<AudioFile>
{
    public void Configure(EntityTypeBuilder<AudioFile> builder)
    {
        builder.ToTable("audio_files");

        builder.HasKey(audioFile => audioFile.Id);

        builder.Property(audioFile => audioFile.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => AudioFileId.From(value))
            .ValueGeneratedNever();

        builder.Property(audioFile => audioFile.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(audioFile => audioFile.StoredFileName)
            .HasColumnName("stored_file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(audioFile => audioFile.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(audioFile => audioFile.SizeInBytes)
            .HasColumnName("size_in_bytes")
            .IsRequired();

        builder.Property(audioFile => audioFile.StorageBucket)
            .HasColumnName("storage_bucket")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(audioFile => audioFile.StoragePath)
            .HasColumnName("storage_path")
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(audioFile => audioFile.PublicUrl)
            .HasColumnName("public_url")
            .HasMaxLength(2048);

        builder.Property(audioFile => audioFile.OriginalStoredFileName)
            .HasColumnName("original_stored_file_name")
            .HasMaxLength(255);

        builder.Property(audioFile => audioFile.OriginalContentType)
            .HasColumnName("original_content_type")
            .HasMaxLength(100);

        builder.Property(audioFile => audioFile.OriginalSizeInBytes)
            .HasColumnName("original_size_in_bytes");

        builder.Property(audioFile => audioFile.OriginalStoragePath)
            .HasColumnName("original_storage_path")
            .HasMaxLength(1024);

        builder.Property(audioFile => audioFile.OriginalPublicUrl)
            .HasColumnName("original_public_url")
            .HasMaxLength(2048);

        builder.Property(audioFile => audioFile.FilteredStoredFileName)
            .HasColumnName("filtered_stored_file_name")
            .HasMaxLength(255);

        builder.Property(audioFile => audioFile.FilteredContentType)
            .HasColumnName("filtered_content_type")
            .HasMaxLength(100);

        builder.Property(audioFile => audioFile.FilteredSizeInBytes)
            .HasColumnName("filtered_size_in_bytes");

        builder.Property(audioFile => audioFile.FilteredStoragePath)
            .HasColumnName("filtered_storage_path")
            .HasMaxLength(1024);

        builder.Property(audioFile => audioFile.FilteredPublicUrl)
            .HasColumnName("filtered_public_url")
            .HasMaxLength(2048);

        builder.Property(audioFile => audioFile.Status)
            .HasColumnName("status")
            .HasConversion(status => status.ToString(), value => Enum.Parse<AudioFileStatus>(value))
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(audioFile => audioFile.Summary)
            .HasColumnName("summary")
            .HasMaxLength(AudioSummary.AbsoluteMaxLength);

        builder.Property(audioFile => audioFile.SummarizedAtUtc)
            .HasColumnName("summarized_at_utc");

        builder.Property(audioFile => audioFile.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(audioFile => audioFile.StoragePath)
            .IsUnique();

        builder.HasIndex(audioFile => audioFile.OriginalStoragePath)
            .IsUnique();

        builder.HasIndex(audioFile => audioFile.FilteredStoragePath)
            .IsUnique();
    }
}
