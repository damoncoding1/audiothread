using System.ComponentModel;
using System.Diagnostics;
using AudioThread.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AudioThread.Infrastructure.NoiseReduction;

public sealed class DeepFilterNetAudioNoiseReducer : IAudioNoiseReducer
{
    private readonly AudioNoiseReductionOptions _options;
    private readonly ILogger<DeepFilterNetAudioNoiseReducer> _logger;

    public DeepFilterNetAudioNoiseReducer(
        IOptions<AudioNoiseReductionOptions> options,
        ILogger<DeepFilterNetAudioNoiseReducer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FilteredAudioFile> ReduceNoiseAsync(
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "AudioThread", "noise-reduction");
        Directory.CreateDirectory(temporaryDirectory);

        var inputPath = Path.Combine(
            temporaryDirectory,
            $"{operationId}{Path.GetExtension(originalFileName)}");
        var outputPath = Path.Combine(temporaryDirectory, $"{operationId}-filtered.wav");
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting local noise reduction for {OriginalFileName} using model {Model}",
            originalFileName,
            _options.Model);

        try
        {
            await WriteInputFileAsync(content, inputPath, cancellationToken);
            await ExecuteModelAsync(inputPath, outputPath, cancellationToken);

            var filteredContent = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            if (filteredContent.Length == 0)
            {
                throw new AudioNoiseReductionException("DeepFilterNet produced an empty audio file.");
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Local noise reduction completed for {OriginalFileName} in {ElapsedMilliseconds} ms. Filtered size: {FilteredSize} bytes",
                originalFileName,
                stopwatch.ElapsedMilliseconds,
                filteredContent.LongLength);

            return new FilteredAudioFile(
                new MemoryStream(filteredContent, writable: false),
                $"{Path.GetFileNameWithoutExtension(originalFileName)}-filtered.wav",
                "audio/wav",
                filteredContent.LongLength,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Noise reduction was cancelled for {OriginalFileName} after {ElapsedMilliseconds} ms",
                originalFileName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Win32Exception exception)
        {
            throw new AudioNoiseReductionException(
                $"Python could not be started from '{_options.PythonExecutablePath}'.",
                exception);
        }
        finally
        {
            DeleteTemporaryFile(inputPath);
            DeleteTemporaryFile(outputPath);
        }
    }

    private async Task ExecuteModelAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var scriptPath = ResolveRepositoryFile(_options.ScriptPath)
            ?? throw new AudioNoiseReductionException(
                $"The noise reduction script '{_options.ScriptPath}' was not found.");
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveExecutablePath(_options.PythonExecutablePath),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)
                ?? Environment.CurrentDirectory
        };

        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add(_options.Model);
        startInfo.ArgumentList.Add("--threads");
        startInfo.ArgumentList.Add(_options.ModelThreads.ToString());

        if (_options.EnablePostFilter)
        {
            startInfo.ArgumentList.Add("--post-filter");
        }

        if (!string.IsNullOrWhiteSpace(_options.ModelCacheDirectory))
        {
            startInfo.ArgumentList.Add("--cache-directory");
            startInfo.ArgumentList.Add(ResolveDirectory(
                _options.ModelCacheDirectory,
                Path.GetDirectoryName(scriptPath)!));
        }

        using var process = Process.Start(startInfo)
            ?? throw new AudioNoiseReductionException("The DeepFilterNet process could not be started.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TerminateProcess(process);
            throw new AudioNoiseReductionException(
                $"Noise reduction exceeded the {_options.TimeoutSeconds}-second timeout.");
        }
        catch (OperationCanceledException)
        {
            TerminateProcess(process);
            throw;
        }

        var standardOutput = await outputTask;
        var standardError = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new AudioNoiseReductionException(
                $"DeepFilterNet failed with exit code {process.ExitCode}: {standardError.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(standardOutput) || !string.IsNullOrWhiteSpace(standardError))
        {
            _logger.LogDebug(
                "DeepFilterNet diagnostics: {Diagnostics}",
                $"{standardOutput}\n{standardError}".Trim());
        }
    }

    private static async Task WriteInputFileAsync(
        Stream content,
        string inputPath,
        CancellationToken cancellationToken)
    {
        await using var output = new FileStream(
            inputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await content.CopyToAsync(output, cancellationToken);
    }

    private static string ResolveExecutablePath(string configuredPath)
    {
        var looksLikePath = configuredPath.Contains(Path.DirectorySeparatorChar)
            || configuredPath.Contains(Path.AltDirectorySeparatorChar);

        return !looksLikePath
            ? configuredPath
            : ResolveRepositoryFile(configuredPath)
                ?? throw new AudioNoiseReductionException(
                    $"The Python executable '{configuredPath}' was not found.");
    }

    private static string ResolveDirectory(string configuredPath, string scriptDirectory)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            Directory.CreateDirectory(configuredPath);
            return configuredPath;
        }

        var resolved = Path.GetFullPath(Path.Combine(scriptDirectory, configuredPath));
        Directory.CreateDirectory(resolved);
        return resolved;
    }

    private static string? ResolveRepositoryFile(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return File.Exists(configuredPath) ? configuredPath : null;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, configuredPath);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static void TerminateProcess(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
