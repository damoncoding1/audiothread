using AudioThread.Application.Common.Interfaces;
using AudioThread.Application.Features.AudioFiles.Jobs;
using AudioThread.Domain.Interfaces;
using AudioThread.Infrastructure.Compression;
using AudioThread.Infrastructure.Persistence;
using AudioThread.Infrastructure.Persistence.Repositories;
using AudioThread.Infrastructure.NoiseReduction;
using AudioThread.Infrastructure.Processing;
using AudioThread.Infrastructure.Storage;
using AudioThread.Infrastructure.Summarization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AudioThread.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["SUPABASE_DATABASE_CONNECTION_STRING"]
            ?? configuration.GetConnectionString("AudioThreadDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Supabase Postgres connection string is not configured.");
        }

        services.AddDbContext<AudioThreadDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<AudioThreadDbContext>());
        services.AddScoped<IAudioFileRepository, AudioFileRepository>();

        services.Configure<FfmpegCompressionOptions>(
            configuration.GetSection(FfmpegCompressionOptions.SectionName));
        services.AddScoped<IAudioCompressor, FfmpegAudioCompressor>();

        services.Configure<AudioSummarizationOptions>(
            configuration.GetSection(AudioSummarizationOptions.SectionName));
        services.AddScoped<IAudioSummarizer, PythonAudioSummarizer>();
        services.AddOptions<AudioNoiseReductionOptions>()
            .Bind(configuration.GetSection(AudioNoiseReductionOptions.SectionName))
            .Validate(options => options.ModelThreads > 0, "AudioNoiseReduction:ModelThreads must be greater than zero.")
            .Validate(options => options.TimeoutSeconds > 0, "AudioNoiseReduction:TimeoutSeconds must be greater than zero.")
            .ValidateOnStart();
        services.AddScoped<IAudioNoiseReducer, DeepFilterNetAudioNoiseReducer>();
        services.AddScoped<IAudioProcessingCoordinator, TplAudioProcessingCoordinator>();

        services.AddOptions<AudioProcessingOptions>()
            .Bind(configuration.GetSection(AudioProcessingOptions.SectionName))
            .Validate(
                options => Enum.IsDefined(options.ExecutionMode),
                "AudioProcessing:ExecutionMode must be Sequential or Optimized.")
            .Validate(
                options => options.MaxConcurrentJobs > 0,
                "AudioProcessing:MaxConcurrentJobs must be greater than zero.")
            .Validate(
                options => options.QueueCapacity >= options.MaxConcurrentJobs,
                "AudioProcessing:QueueCapacity must be greater than or equal to MaxConcurrentJobs.")
            .ValidateOnStart();

        services.AddSingleton<IAudioProcessingJobStore, InMemoryAudioProcessingJobStore>();
        services.AddSingleton<IAudioProcessingJobQueue, AudioProcessingJobQueue>();
        services.AddSingleton<ParallelAudioProcessingExecutor>();
        services.AddHostedService<AudioProcessingBackgroundService>();

        services.Configure<SupabaseStorageOptions>(options =>
        {
            options.Url = configuration["SUPABASE_URL"]
                ?? configuration[$"{SupabaseStorageOptions.SectionName}:Url"]
                ?? string.Empty;
            options.ServiceRoleKey = configuration["SUPABASE_SERVICE_ROLE_KEY"]
                ?? configuration[$"{SupabaseStorageOptions.SectionName}:ServiceRoleKey"]
                ?? string.Empty;
            options.StorageBucket = configuration["SUPABASE_STORAGE_BUCKET"]
                ?? configuration[$"{SupabaseStorageOptions.SectionName}:StorageBucket"]
                ?? "audio-files";
            options.UsePublicUrls = bool.TryParse(
                configuration["SUPABASE_STORAGE_USE_PUBLIC_URLS"]
                    ?? configuration[$"{SupabaseStorageOptions.SectionName}:UsePublicUrls"],
                out var usePublicUrls) && usePublicUrls;
        });

        services.AddHttpClient<IAudioFileStorage, SupabaseAudioFileStorage>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupabaseStorageOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.Url))
            {
                client.BaseAddress = new Uri(options.Url.TrimEnd('/') + "/");
            }
        });

        return services;
    }
}
