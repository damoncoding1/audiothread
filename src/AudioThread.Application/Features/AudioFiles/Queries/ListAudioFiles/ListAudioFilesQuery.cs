namespace AudioThread.Application.Features.AudioFiles.Queries.ListAudioFiles;

public sealed record ListAudioFilesQuery(int Skip, int Take)
{
    public const int MinTake = 1;
    public const int MaxTake = 100;
    public const int DefaultTake = 20;

    public static ListAudioFilesQuery Create(int? skip, int? take)
    {
        var normalizedSkip = skip is > 0 ? skip.Value : 0;
        var normalizedTake = take is null
            ? DefaultTake
            : Math.Clamp(take.Value, MinTake, MaxTake);

        return new ListAudioFilesQuery(normalizedSkip, normalizedTake);
    }
}
