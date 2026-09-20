using System.ComponentModel;
using System.Diagnostics;
using AudioThread.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AudioThread.Infrastructure.Compression;

public sealed class FfmpegAudioCompressor : IAudioCompressor
{
    private const string AacContentType = "audio/aac";

    private readonly FfmpegCompressionOptions _options;
    private readonly ILogger<FfmpegAudioCompressor> _logger;

    public FfmpegAudioCompressor(
        IOptions<FfmpegCompressionOptions> options,
        ILogger<FfmpegAudioCompressor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CompressedAudioFile> CompressToAacAsync(
        Stream content,
        string originalFileName,
        long originalSizeInBytes,
        CancellationToken cancellationToken)
    {
        ValidateOptions();

        var operationId = Guid.NewGuid().ToString("N");
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "AudioThread", "compression");
        Directory.CreateDirectory(temporaryDirectory);

        var inputExtension = Path.GetExtension(originalFileName);
        var inputPath = Path.Combine(temporaryDirectory, $"{operationId}{inputExtension}");
        var outputPath = Path.Combine(temporaryDirectory, $"{operationId}.aac");
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting AAC compression for {OriginalFileName}. Original size: {OriginalSizeInBytes} bytes",
            originalFileName,
            originalSizeInBytes);

        try
        {
            await WriteInputFileAsync(content, inputPath, cancellationToken);
            await ExecuteFfmpegAsync(inputPath, outputPath, cancellationToken);

            var compressedBytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            if (compressedBytes.Length == 0)
            {
                throw new AudioCompressionException("FFmpeg created an empty AAC file.");
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "AAC compression completed for {OriginalFileName} in {ElapsedMilliseconds} ms. Original size: {OriginalSizeInBytes} bytes. Compressed size: {CompressedSizeInBytes} bytes",
                originalFileName,
                stopwatch.ElapsedMilliseconds,
                originalSizeInBytes,
                compressedBytes.LongLength);

            return new CompressedAudioFile(
                new MemoryStream(compressedBytes, writable: false),
                CreateCompressedFileName(originalFileName),
                AacContentType,
                compressedBytes.LongLength,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "AAC compression was cancelled for {OriginalFileName} after {ElapsedMilliseconds} ms",
                originalFileName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Win32Exception exception)
        {
            throw new AudioCompressionException(
                $"FFmpeg could not be started from '{_options.ExecutablePath}'. Install FFmpeg or configure AudioCompression:ExecutablePath.",
                exception);
        }
        finally
        {
            DeleteTemporaryFile(inputPath);
            DeleteTemporaryFile(outputPath);
        }
    }

    private async Task ExecuteFfmpegAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        AddArguments(startInfo, inputPath, outputPath);

        using var process = Process.Start(startInfo)
            ?? throw new AudioCompressionException("FFmpeg process could not be started.");
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TerminateProcess(process);
            throw new AudioCompressionException(
                $"FFmpeg compression exceeded the {_options.TimeoutSeconds}-second timeout.");
        }
        catch (OperationCanceledException)
        {
            TerminateProcess(process);
            throw;
        }

        var standardError = await standardErrorTask;
        _ = await standardOutputTask;

        if (process.ExitCode != 0)
        {
            throw new AudioCompressionException(
                $"FFmpeg compression failed with exit code {process.ExitCode}: {standardError.Trim()}");
        }
    }

    private void AddArguments(ProcessStartInfo startInfo, string inputPath, string outputPath)
    {
        string[] arguments =
        [
            "-y",
            "-hide_banner",
            "-loglevel", "error",
            "-i", inputPath,
            "-vn",
            "-c:a", "aac",
            "-b:a", _options.AudioBitrate,
            outputPath
        ];

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static async Task WriteInputFileAsync(
        Stream content,
        string inputPath,
        CancellationToken cancellationToken)
    {
        await using var inputFile = new FileStream(
            inputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await content.CopyToAsync(inputFile, cancellationToken);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ExecutablePath))
        {
            throw new AudioCompressionException("FFmpeg executable path is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.AudioBitrate))
        {
            throw new AudioCompressionException("AAC audio bitrate is not configured.");
        }

        if (_options.TimeoutSeconds <= 0)
        {
            throw new AudioCompressionException("FFmpeg timeout must be greater than zero seconds.");
        }
    }

    private static string CreateCompressedFileName(string originalFileName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
        return $"{(string.IsNullOrWhiteSpace(nameWithoutExtension) ? "audio" : nameWithoutExtension)}.aac";
    }

    private static void TerminateProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and the termination request.
        }
    }

    private void DeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Temporary audio file {TemporaryFilePath} could not be deleted", path);
        }
    }
}
