using AudioThread.Application.Common.Options;
using AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;
using AudioThread.Application.Features.AudioFiles.Queries.GetAudioFileById;
using AudioThread.Application.Features.AudioFiles.Queries.ListAudioFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AudioThread.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var audioUploadOptions = new AudioUploadOptions
        {
            MaxFileSizeInBytes = long.TryParse(
                configuration[$"{AudioUploadOptions.SectionName}:MaxFileSizeInBytes"],
                out var maxFileSize)
                ? maxFileSize
                : new AudioUploadOptions().MaxFileSizeInBytes,
            AllowedExtensions = configuration
                .GetSection($"{AudioUploadOptions.SectionName}:AllowedExtensions")
                .GetChildren()
                .Select(section => section.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToArray(),
            AllowedContentTypes = configuration
                .GetSection($"{AudioUploadOptions.SectionName}:AllowedContentTypes")
                .GetChildren()
                .Select(section => section.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToArray()
        };

        if (audioUploadOptions.AllowedExtensions.Length == 0)
        {
            audioUploadOptions = audioUploadOptions with { AllowedExtensions = new AudioUploadOptions().AllowedExtensions };
        }

        if (audioUploadOptions.AllowedContentTypes.Length == 0)
        {
            audioUploadOptions = audioUploadOptions with { AllowedContentTypes = new AudioUploadOptions().AllowedContentTypes };
        }

        var audioSummaryOptions = new AudioSummaryOptions
        {
            MaxLength = int.TryParse(
                configuration[$"{AudioSummaryOptions.SectionName}:MaxSummaryLength"],
                out var maxSummaryLength)
                ? maxSummaryLength
                : new AudioSummaryOptions().MaxLength
        };

        services.AddSingleton(audioUploadOptions);
        services.AddSingleton(audioSummaryOptions);
        services.AddScoped<UploadAudioFileValidator>();
        services.AddScoped<UploadAudioFileHandler>();
        services.AddScoped<GetAudioFileByIdHandler>();
        services.AddScoped<ListAudioFilesHandler>();

        return services;
    }
}
