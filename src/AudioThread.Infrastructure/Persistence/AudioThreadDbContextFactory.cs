using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AudioThread.Infrastructure.Persistence;

public sealed class AudioThreadDbContextFactory : IDesignTimeDbContextFactory<AudioThreadDbContext>
{
    public AudioThreadDbContext CreateDbContext(string[] args)
    {
        LoadEnvFile();

        var connectionString = Environment.GetEnvironmentVariable("SUPABASE_DATABASE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=audio_thread;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AudioThreadDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AudioThreadDbContext(optionsBuilder.Options);
    }

    private static void LoadEnvFile()
    {
        var envPath = FindEnvFile();

        if (!File.Exists(envPath))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(envPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');

            if (!string.IsNullOrWhiteSpace(key))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string? FindEnvFile()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var envPath = Path.Combine(directory.FullName, ".env");
            if (File.Exists(envPath))
            {
                return envPath;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
