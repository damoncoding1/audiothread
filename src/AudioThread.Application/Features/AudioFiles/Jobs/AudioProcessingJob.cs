using AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;

namespace AudioThread.Application.Features.AudioFiles.Jobs;

public enum AudioProcessingJobStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}

public enum AudioProcessingStepState
{
    Pending,
    Running,
    Completed,
    Failed
}

public static class AudioProcessingSteps
{
    public const string Compression = "compression";
    public const string Transcription = "transcription";
    public const string Summarization = "summarization";
    public const string NoiseReduction = "noise-reduction";
    public const string Storage = "storage";
    public const string Database = "database";

    public static IReadOnlyList<string> All { get; } =
    [
        Compression,
        Transcription,
        Summarization,
        NoiseReduction,
        Storage,
        Database
    ];
}

public sealed record AudioProcessingStepUpdate(
    string Step,
    AudioProcessingStepState State,
    TimeSpan? Duration = null);

public sealed record AudioProcessingStepSnapshot(
    string Step,
    AudioProcessingStepState State,
    long? DurationMilliseconds);

public sealed record AudioProcessingJobSnapshot(
    Guid Id,
    string OriginalFileName,
    AudioProcessingJobStatus Status,
    IReadOnlyList<AudioProcessingStepSnapshot> Steps,
    UploadAudioFileResponse? Result,
    string? ErrorMessage,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc)
{
    public bool IsFinished => Status is AudioProcessingJobStatus.Completed or AudioProcessingJobStatus.Failed;
}
