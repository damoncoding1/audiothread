namespace AudioThread.Infrastructure.Storage;

public sealed class SupabaseStorageOptions
{
    public const string SectionName = "Supabase";

    public string Url { get; set; } = string.Empty;

    public string ServiceRoleKey { get; set; } = string.Empty;

    public string StorageBucket { get; set; } = "audio-files";

    public bool UsePublicUrls { get; set; }
}
