using AudioThread.Application.Common.Interfaces;
using AudioThread.Application.Common.Options;
using AudioThread.Application.Common.Processing;
using AudioThread.Domain.Entities;
using AudioThread.Domain.Interfaces;
using AudioThread.Application.Features.AudioFiles.Jobs;
using AudioThread.Domain.ValueObjects;

namespace AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;

public sealed class UploadAudioFileHandler
{
    private readonly IAudioProcessingCoordinator _coordinator;
    private readonly IAudioFileStorage _storage;
    private readonly IAudioFileRepository _repository;
    private readonly UploadAudioFileValidator _validator;
    private readonly AudioSummaryOptions _summaryOptions;

    public UploadAudioFileHandler(
        IAudioProcessingCoordinator coordinator,
        IAudioFileStorage storage,
        IAudioFileRepository repository,
        UploadAudioFileValidator validator,
        AudioSummaryOptions summaryOptions)
    {
        _coordinator = coordinator;
        _storage = storage;
        _repository = repository;
        _validator = validator;
        _summaryOptions = summaryOptions;
    }

    public async Task<UploadAudioFileResponse> HandleAsync(
        UploadAudioFileCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = _validator.Validate(command);
        if (!validationResult.IsValid)
        {
            throw new AudioUploadValidationException(validationResult.Errors);
        }

        var audioFileId = AudioFileId.New();
        var originalContent = await BufferOriginalContentAsync(command, cancellationToken);

        await using var session = _coordinator.Start(
            new AudioProcessingRequest(originalContent, command.FileName, command.SizeInBytes)
            {
                Progress = command.Progress
            },
            cancellationToken);

        AudioProcessingResult processing;
        StoredPlaybackVersions storedFiles;

        if (session.AllowsOverlappedStorage)
        {
            var storageTask = UploadPlaybackVersionsAsync(
                session,
                audioFileId,
                originalContent,
                command,
                command.Progress,
                cancellationToken);
            var processingTask = session.CompleteAsync();

            await WhenBothStagesSettleAsync(processingTask, storageTask);

            processing = await processingTask;
            storedFiles = await storageTask;
        }
        else
        {
            processing = await session.CompleteAsync();
            storedFiles = await UploadPlaybackVersionsSequentiallyAsync(
                processing,
                audioFileId,
                originalContent,
                command,
                command.Progress,
                cancellationToken);
        }

        command.Progress?.Report(new AudioProcessingStepUpdate(
            AudioProcessingSteps.Database,
            AudioProcessingStepState.Running));

        var createdAtUtc = DateTimeOffset.UtcNow;
        var audioFile = new AudioFile(
            audioFileId,
            command.FileName,
            storedFiles.Compressed.StoredFileName,
            processing.CompressedAudio.ContentType,
            processing.CompressedAudio.SizeInBytes,
            storedFiles.Compressed.Bucket,
            storedFiles.Compressed.Path,
            storedFiles.Compressed.PublicUrl,
            createdAtUtc);

        audioFile.AttachPlaybackVersions(
            storedFiles.Original.StoredFileName,
            command.ContentType,
            command.SizeInBytes,
            storedFiles.Original.Path,
            storedFiles.Original.PublicUrl,
            storedFiles.Filtered.StoredFileName,
            processing.FilteredAudio.ContentType,
            processing.FilteredAudio.SizeInBytes,
            storedFiles.Filtered.Path,
            storedFiles.Filtered.PublicUrl);

        var summary = AudioSummary.FromModelOutput(processing.Summary.Summary, _summaryOptions.MaxLength);
        audioFile.AttachSummary(summary, createdAtUtc);

        await _repository.AddAsync(audioFile, cancellationToken);

        command.Progress?.Report(new AudioProcessingStepUpdate(
            AudioProcessingSteps.Database,
            AudioProcessingStepState.Completed));

        return new UploadAudioFileResponse(
            audioFile.Id.Value,
            audioFile.OriginalFileName,
            audioFile.ContentType,
            audioFile.SizeInBytes,
            audioFile.StorageBucket,
            audioFile.StoragePath,
            audioFile.PublicUrl,
            audioFile.OriginalPublicUrl,
            audioFile.FilteredPublicUrl,
            audioFile.Summary,
            audioFile.CreatedAtUtc);
    }

    private async Task<StoredPlaybackVersions> UploadPlaybackVersionsAsync(
        AudioProcessingSession session,
        AudioFileId audioFileId,
        byte[] originalContent,
        UploadAudioFileCommand command,
        IProgress<AudioProcessingStepUpdate>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new AudioProcessingStepUpdate(
            AudioProcessingSteps.Storage,
            AudioProcessingStepState.Running));

        var originalTask = UploadOriginalAsync(
            originalContent,
            audioFileId,
            command.FileName,
            command.ContentType,
            cancellationToken);
        var compressedTask = UploadWhenCompressedAsync(session, audioFileId, cancellationToken);
        var filteredTask = UploadWhenFilteredAsync(session, audioFileId, cancellationToken);

        await Task.WhenAll(originalTask, compressedTask, filteredTask);

        progress?.Report(new AudioProcessingStepUpdate(
            AudioProcessingSteps.Storage,
            AudioProcessingStepState.Completed));

        return new StoredPlaybackVersions(
            await originalTask,
            await compressedTask,
            await filteredTask);
    }

    private async Task<StoredPlaybackVersions> UploadPlaybackVersionsSequentiallyAsync(
        AudioProcessingResult processing,
        AudioFileId audioFileId,
        byte[] originalContent,
        UploadAudioFileCommand command,
        IProgress<AudioProcessingStepUpdate>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new AudioProcessingStepUpdate(
            AudioProcessingSteps.Storage,
            AudioProcessingStepState.Running));

        var original = await UploadOriginalAsync(
            originalContent,
            audioFileId,
            command.FileName,
            command.ContentType,
            cancellationToken);
        var compressed = await UploadAsync(
            processing.CompressedAudio.Content,
            audioFileId,
            processing.CompressedAudio.FileName,
            processing.CompressedAudio.ContentType,
            cancellationToken);
        var filtered = await UploadAsync(
            processing.FilteredAudio.Content,
            audioFileId,
            processing.FilteredAudio.FileName,
            processing.FilteredAudio.ContentType,
            cancellationToken);

        progress?.Report(new AudioProcessingStepUpdate(
            AudioProcessingSteps.Storage,
            AudioProcessingStepState.Completed));

        return new StoredPlaybackVersions(original, compressed, filtered);
    }

    private async Task<StoredAudioFile> UploadWhenCompressedAsync(
        AudioProcessingSession session,
        AudioFileId audioFileId,
        CancellationToken cancellationToken)
    {
        var compression = await session.Compression;

        return await UploadAsync(
            compression.Value.Content,
            audioFileId,
            compression.Value.FileName,
            compression.Value.ContentType,
            cancellationToken);
    }

    private async Task<StoredAudioFile> UploadWhenFilteredAsync(
        AudioProcessingSession session,
        AudioFileId audioFileId,
        CancellationToken cancellationToken)
    {
        var filtered = await session.NoiseReduction;

        return await UploadAsync(
            filtered.Value.Content,
            audioFileId,
            filtered.Value.FileName,
            filtered.Value.ContentType,
            cancellationToken);
    }

    private async Task<StoredAudioFile> UploadOriginalAsync(
        byte[] originalContent,
        AudioFileId audioFileId,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        await using var content = new MemoryStream(originalContent, writable: false);
        return await UploadAsync(content, audioFileId, fileName, contentType, cancellationToken);
    }

    private Task<StoredAudioFile> UploadAsync(
        Stream content,
        AudioFileId audioFileId,
        string fileName,
        string contentType,
        CancellationToken cancellationToken) =>
        _storage.UploadAsync(audioFileId, content, fileName, contentType, cancellationToken);

    private static async Task WhenBothStagesSettleAsync(Task processing, Task storage)
    {
        try
        {
            await Task.WhenAll(processing, storage);
        }
        catch (Exception)
        {
            await processing;
            throw;
        }
    }

    private static async Task<byte[]> BufferOriginalContentAsync(
        UploadAudioFileCommand command,
        CancellationToken cancellationToken)
    {
        var capacity = command.SizeInBytes > 0 && command.SizeInBytes <= int.MaxValue
            ? (int)command.SizeInBytes
            : 0;

        using var buffer = new MemoryStream(capacity);
        await command.Content.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }

    private sealed record StoredPlaybackVersions(
        StoredAudioFile Original,
        StoredAudioFile Compressed,
        StoredAudioFile Filtered);
}

public sealed class AudioUploadValidationException : InvalidOperationException
{
    public AudioUploadValidationException(IReadOnlyList<string> errors)
        : base("Audio upload validation failed.")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
