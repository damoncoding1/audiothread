using System.Text.Json;
using System.Text.Json.Serialization;
using AudioThread.Application.Common.Options;
using AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;
using AudioThread.Application.Features.AudioFiles.Jobs;

namespace AudioThread.Api.Endpoints;

public static class AudioProcessingJobsEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static IEndpointRouteBuilder MapAudioProcessingJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audio-files/jobs")
            .WithTags("Audio processing jobs");

        group.MapPost("/", CreateJobAsync)
            .WithName("CreateAudioProcessingJob")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<AudioProcessingJobSnapshot>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem();

        group.MapGet("/{jobId:guid}", GetJob)
            .WithName("GetAudioProcessingJob")
            .Produces<AudioProcessingJobSnapshot>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{jobId:guid}/events", StreamJobEventsAsync)
            .WithName("StreamAudioProcessingJobEvents")
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> CreateJobAsync(
        HttpRequest request,
        IAudioProcessingJobQueue queue,
        IAudioProcessingJobStore store,
        UploadAudioFileValidator validator,
        AudioUploadOptions uploadOptions,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["The request must be multipart/form-data."]
            });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");

        if (file is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["A file named 'file' is required."]
            });
        }

        await using var uploadStream = file.OpenReadStream();
        var validation = validator.Validate(new UploadAudioFileCommand(
            uploadStream,
            file.FileName,
            file.ContentType,
            file.Length));

        if (!validation.IsValid)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = [.. validation.Errors]
            });
        }

        var content = await ReadFileAsync(file, cancellationToken);
        var jobId = Guid.NewGuid();
        var snapshot = store.Create(jobId, file.FileName);

        await queue.EnqueueAsync(
            new AudioProcessingWorkItem(jobId, content, file.FileName, file.ContentType, file.Length),
            cancellationToken);

        return Results.Accepted($"/api/audio-files/jobs/{jobId}", snapshot);
    }

    private static IResult GetJob(Guid jobId, IAudioProcessingJobStore store)
    {
        var snapshot = store.Get(jobId);

        return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
    }

    private static async Task StreamJobEventsAsync(
        Guid jobId,
        HttpContext context,
        IAudioProcessingJobStore store,
        CancellationToken cancellationToken)
    {
        if (store.Get(jobId) is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var snapshot in store.WatchAsync(jobId, cancellationToken))
        {
            var payload = JsonSerializer.Serialize(snapshot, SerializerOptions);

            await context.Response.WriteAsync($"id: {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}\n", cancellationToken);
            await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }

    private static async Task<byte[]> ReadFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(file.Length > 0 && file.Length <= int.MaxValue ? (int)file.Length : 0);
        await using var stream = file.OpenReadStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }
}
