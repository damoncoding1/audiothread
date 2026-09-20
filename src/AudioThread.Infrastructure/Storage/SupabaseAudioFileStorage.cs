using System.Net.Http.Headers;
using AudioThread.Application.Common.Interfaces;
using AudioThread.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace AudioThread.Infrastructure.Storage;

public sealed class SupabaseAudioFileStorage : IAudioFileStorage
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseStorageOptions _options;

    public SupabaseAudioFileStorage(HttpClient httpClient, IOptions<SupabaseStorageOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<StoredAudioFile> UploadAsync(
        AudioFileId audioFileId,
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var storedFileName = CreateSafeStoredFileName(originalFileName);
        var storagePath = $"uploads/{audioFileId.Value:N}/{storedFileName}";
        var requestUri = $"storage/v1/object/{Uri.EscapeDataString(_options.StorageBucket)}/{storagePath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
        request.Headers.TryAddWithoutValidation("apikey", _options.ServiceRoleKey);
        request.Headers.TryAddWithoutValidation("x-upsert", "false");
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new SupabaseStorageException(
                $"Supabase Storage upload failed with status {(int)response.StatusCode}: {responseBody}");
        }

        var publicUrl = _options.UsePublicUrls
            ? BuildPublicUrl(_options.Url, _options.StorageBucket, storagePath)
            : null;

        return new StoredAudioFile(_options.StorageBucket, storagePath, storedFileName, publicUrl);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
        {
            throw new SupabaseStorageException("Supabase URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.ServiceRoleKey))
        {
            throw new SupabaseStorageException("Supabase service role key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.StorageBucket))
        {
            throw new SupabaseStorageException("Supabase storage bucket is not configured.");
        }
    }

    private static string CreateSafeStoredFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        return $"{Guid.NewGuid():N}{extension}";
    }

    private static string BuildPublicUrl(string supabaseUrl, string bucket, string storagePath)
    {
        var baseUrl = supabaseUrl.TrimEnd('/');
        return $"{baseUrl}/storage/v1/object/public/{Uri.EscapeDataString(bucket)}/{storagePath}";
    }
}

public sealed class SupabaseStorageException(string message) : InvalidOperationException(message);
