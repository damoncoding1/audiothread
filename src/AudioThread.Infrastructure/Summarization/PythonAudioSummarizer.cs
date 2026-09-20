using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using AudioThread.Application.Common.Interfaces;
using AudioThread.Application.Features.AudioFiles.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AudioThread.Infrastructure.Summarization;

public sealed partial class PythonAudioSummarizer : IAudioSummarizer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AudioSummarizationOptions _options;
    private readonly ILogger<PythonAudioSummarizer> _logger;

    public PythonAudioSummarizer(
        IOptions<AudioSummarizationOptions> options,
        ILogger<PythonAudioSummarizer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AudioSummaryResult> SummarizeAsync(
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken,
        IProgress<AudioProcessingStepUpdate>? progress = null)
    {
        var execution = new ScriptExecutionPaths(
            ResolvePythonExecutablePath(),
            ResolveScriptPath(),
            ResolveModelCacheDirectory());
        var operationId = Guid.NewGuid().ToString("N");
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "AudioThread", "summarization");
        Directory.CreateDirectory(temporaryDirectory);

        var inputPath = Path.Combine(
            temporaryDirectory,
            $"{operationId}{Path.GetExtension(originalFileName)}");
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting summary extraction for {OriginalFileName} using speech-to-text model '{SpeechToTextModel}' and summarization model '{SummarizationModel}'",
            originalFileName,
            _options.SpeechToTextModel,
            _options.SummarizationModel);

        try
        {
            await WriteInputFileAsync(content, inputPath, cancellationToken);
            var payload = await ExecuteScriptAsync(execution, inputPath, cancellationToken, progress);

            stopwatch.Stop();
            _logger.LogInformation(
                "Summary extraction completed for {OriginalFileName} in {ElapsedMilliseconds} ms. Transcript length: {TranscriptLength} characters. Summary length: {SummaryLength} characters. Summary: {Summary}",
                originalFileName,
                stopwatch.ElapsedMilliseconds,
                payload.Transcript.Length,
                payload.Summary.Length,
                payload.Summary);

            return new AudioSummaryResult(
                payload.Summary,
                payload.Transcript,
                TimeSpan.FromMilliseconds(payload.TranscriptionMilliseconds),
                TimeSpan.FromMilliseconds(payload.SummarizationMilliseconds));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Summary extraction was cancelled for {OriginalFileName} after {ElapsedMilliseconds} ms",
                originalFileName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Win32Exception exception)
        {
            throw new AudioSummarizationException(
                $"Python could not be started from '{_options.PythonExecutablePath}'. Create the virtual environment and configure AudioSummarization:PythonExecutablePath.",
                exception);
        }
        finally
        {
            DeleteTemporaryFile(inputPath);
        }
    }

    private async Task<SummarizationPayload> ExecuteScriptAsync(
        ScriptExecutionPaths execution,
        string inputPath,
        CancellationToken cancellationToken,
        IProgress<AudioProcessingStepUpdate>? progress = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = execution.PythonExecutablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(execution.ScriptPath) ?? Environment.CurrentDirectory
        };

        AddArguments(startInfo, execution, inputPath);

        using var process = Process.Start(startInfo)
            ?? throw new AudioSummarizationException("The summarization process could not be started.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = ReadDiagnosticsAsync(process.StandardError, progress, cancellationToken);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TerminateProcess(process);
            throw new AudioSummarizationException(
                $"Summary extraction exceeded the {_options.TimeoutSeconds}-second timeout.");
        }
        catch (OperationCanceledException)
        {
            TerminateProcess(process);
            throw;
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new AudioSummarizationException(
                $"Summary extraction failed with exit code {process.ExitCode}: {standardError.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            _logger.LogDebug("Summarization script diagnostics: {Diagnostics}", standardError.Trim());
        }

        return ParsePayload(standardOutput);
    }

    private static async Task<string> ReadDiagnosticsAsync(
        StreamReader standardError,
        IProgress<AudioProcessingStepUpdate>? progress,
        CancellationToken cancellationToken)
    {
        var diagnostics = new StringBuilder();

        while (await standardError.ReadLineAsync(cancellationToken) is { } line)
        {
            diagnostics.AppendLine(line);
            ReportStageFromDiagnostics(line, progress);
        }

        return diagnostics.ToString();
    }

    private static void ReportStageFromDiagnostics(
        string line,
        IProgress<AudioProcessingStepUpdate>? progress)
    {
        if (progress is null)
        {
            return;
        }

        if (line.StartsWith("Loading speech-to-text model", StringComparison.Ordinal))
        {
            progress.Report(new AudioProcessingStepUpdate(
                AudioProcessingSteps.Transcription,
                AudioProcessingStepState.Running));
            return;
        }

        if (line.StartsWith("Transcription finished", StringComparison.Ordinal))
        {
            progress.Report(new AudioProcessingStepUpdate(
                AudioProcessingSteps.Transcription,
                AudioProcessingStepState.Completed,
                ExtractDuration(line)));
            return;
        }

        if (line.StartsWith("Loading summarization model", StringComparison.Ordinal))
        {
            progress.Report(new AudioProcessingStepUpdate(
                AudioProcessingSteps.Summarization,
                AudioProcessingStepState.Running));
            return;
        }

        if (line.StartsWith("Summary generation finished", StringComparison.Ordinal))
        {
            progress.Report(new AudioProcessingStepUpdate(
                AudioProcessingSteps.Summarization,
                AudioProcessingStepState.Completed,
                ExtractDuration(line)));
        }
    }

    private static TimeSpan? ExtractDuration(string line)
    {
        var match = DurationRegex().Match(line);

        return match.Success && double.TryParse(
            match.Groups[1].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var milliseconds)
            ? TimeSpan.FromMilliseconds(milliseconds)
            : null;
    }

    [GeneratedRegex(@"in (\d+(?:\.\d+)?) ms")]
    private static partial Regex DurationRegex();

    private static SummarizationPayload ParsePayload(string standardOutput)
    {
        if (string.IsNullOrWhiteSpace(standardOutput))
        {
            throw new AudioSummarizationException("The summarization script did not return any output.");
        }

        try
        {
            return JsonSerializer.Deserialize<SummarizationPayload>(standardOutput, SerializerOptions)
                ?? throw new AudioSummarizationException("The summarization script returned an empty payload.");
        }
        catch (JsonException exception)
        {
            throw new AudioSummarizationException(
                $"The summarization script returned a malformed payload: {standardOutput.Trim()}",
                exception);
        }
    }

    private void AddArguments(ProcessStartInfo startInfo, ScriptExecutionPaths execution, string inputPath)
    {
        List<string> arguments =
        [
            execution.ScriptPath,
            "--audio", inputPath,
            "--speech-to-text-model", _options.SpeechToTextModel,
            "--summarization-model", _options.SummarizationModel,
            "--compute-type", _options.ComputeType,
            "--torch-dtype", _options.TorchDataType,
            "--max-summary-length", _options.MaxSummaryLength.ToString(),
            "--threads", _options.ModelThreads.ToString()
        ];

        if (!string.IsNullOrWhiteSpace(_options.Language))
        {
            arguments.Add("--language");
            arguments.Add(_options.Language);
        }

        if (!string.IsNullOrWhiteSpace(execution.ModelCacheDirectory))
        {
            arguments.Add("--cache-directory");
            arguments.Add(execution.ModelCacheDirectory);
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private string ResolveScriptPath()
    {
        if (string.IsNullOrWhiteSpace(_options.ScriptPath))
        {
            throw new AudioSummarizationException("The summarization script path is not configured.");
        }

        return ResolveRepositoryFile(_options.ScriptPath)
            ?? throw new AudioSummarizationException(
                $"The summarization script '{_options.ScriptPath}' was not found from '{AppContext.BaseDirectory}' upwards.");
    }

    private string ResolvePythonExecutablePath()
    {
        var configuredPath = _options.PythonExecutablePath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new AudioSummarizationException("The Python executable path is not configured.");
        }

        var looksLikePath = configuredPath.Contains(Path.DirectorySeparatorChar)
            || configuredPath.Contains(Path.AltDirectorySeparatorChar);

        if (!looksLikePath)
        {
            return configuredPath;
        }

        return ResolveRepositoryFile(configuredPath)
            ?? throw new AudioSummarizationException(
                $"The Python executable '{configuredPath}' was not found. Create the virtual environment described in python/README.md.");
    }

    private string? ResolveModelCacheDirectory()
    {
        var configuredPath = _options.ModelCacheDirectory;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        if (Path.IsPathRooted(configuredPath))
        {
            Directory.CreateDirectory(configuredPath);
            return configuredPath;
        }

        var repositoryRoot = Path.GetDirectoryName(ResolveScriptPath());
        var absolutePath = Path.GetFullPath(Path.Combine(
            repositoryRoot ?? AppContext.BaseDirectory,
            configuredPath));

        Directory.CreateDirectory(absolutePath);
        return absolutePath;
    }

    private static string? ResolveRepositoryFile(string relativeOrAbsolutePath)
    {
        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return File.Exists(relativeOrAbsolutePath) ? relativeOrAbsolutePath : null;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativeOrAbsolutePath);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            directory = directory.Parent;
        }

        return null;
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

    private void TerminateProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogDebug(exception, "The summarization process had already exited before termination");
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

    private sealed record ScriptExecutionPaths(
        string PythonExecutablePath,
        string ScriptPath,
        string? ModelCacheDirectory);

    private sealed record SummarizationPayload
    {
        [JsonPropertyName("transcript")]
        public string Transcript { get; init; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; init; } = string.Empty;

        [JsonPropertyName("transcriptionMilliseconds")]
        public double TranscriptionMilliseconds { get; init; }

        [JsonPropertyName("summarizationMilliseconds")]
        public double SummarizationMilliseconds { get; init; }
    }
}
