using AudioThread.Application.Common.Options;

namespace AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;

public sealed class UploadAudioFileValidator
{
    private readonly AudioUploadOptions _options;

    public UploadAudioFileValidator(AudioUploadOptions options)
    {
        _options = options;
    }

    public ValidationResult Validate(UploadAudioFileCommand command)
    {
        List<string> errors = [];

        if (command.Content == Stream.Null)
        {
            errors.Add("Audio file content is required.");
        }

        if (string.IsNullOrWhiteSpace(command.FileName))
        {
            errors.Add("Audio file name is required.");
        }

        if (string.IsNullOrWhiteSpace(command.ContentType))
        {
            errors.Add("Audio file content type is required.");
        }

        if (command.SizeInBytes <= 0)
        {
            errors.Add("Audio file cannot be empty.");
        }

        if (command.SizeInBytes > _options.MaxFileSizeInBytes)
        {
            errors.Add($"Audio file exceeds the maximum allowed size of {_options.MaxFileSizeInBytes} bytes.");
        }

        var extension = Path.GetExtension(command.FileName);
        if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Audio extension '{extension}' is not allowed.");
        }

        if (!_options.AllowedContentTypes.Contains(command.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Audio content type '{command.ContentType}' is not allowed.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success() => new(true, []);

    public static ValidationResult Failure(IReadOnlyList<string> errors) => new(false, errors);
}
