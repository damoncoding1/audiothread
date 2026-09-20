using AudioThread.Api.Contracts.AudioFiles;
using AudioThread.Application.Features.AudioFiles.Commands.UploadAudioFile;
using AudioThread.Application.Features.AudioFiles.Queries.GetAudioFileById;
using AudioThread.Application.Features.AudioFiles.Queries.ListAudioFiles;

namespace AudioThread.Api.Endpoints;

public static class AudioFilesEndpoints
{
    public static IEndpointRouteBuilder MapAudioFilesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audio-files")
            .WithTags("Audio files");

        group.MapPost("/", UploadAudioFileAsync)
            .WithName("UploadAudioFile")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<AudioFileResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/{id:guid}", GetAudioFileByIdAsync)
            .WithName("GetAudioFileById")
            .Produces<AudioFileDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", ListAudioFilesAsync)
            .WithName("ListAudioFiles")
            .Produces<ListAudioFilesResponse>();

        return app;
    }

    private static async Task<IResult> ListAudioFilesAsync(
        ListAudioFilesHandler handler,
        CancellationToken cancellationToken,
        int? skip = null,
        int? take = null)
    {
        var query = ListAudioFilesQuery.Create(skip, take);
        var response = await handler.HandleAsync(query, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> UploadAudioFileAsync(
        HttpRequest request,
        UploadAudioFileHandler handler,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Request must be sent as multipart/form-data."]
            });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Audio file is required. Use the form field name 'file'."]
            });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var response = await handler.HandleAsync(
                new UploadAudioFileCommand(stream, file.FileName, file.ContentType, file.Length),
                cancellationToken);

            var apiResponse = new AudioFileResponse(
                response.Id,
                response.OriginalFileName,
                response.ContentType,
                response.SizeInBytes,
                response.StorageBucket,
                response.StoragePath,
                response.Url,
                response.OriginalUrl,
                response.FilteredUrl,
                response.Summary,
                response.CreatedAtUtc);

            return Results.Created($"/api/audio-files/{apiResponse.Id}", apiResponse);
        }
        catch (AudioUploadValidationException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = exception.Errors.ToArray()
            });
        }
    }

    private static async Task<IResult> GetAudioFileByIdAsync(
        Guid id,
        GetAudioFileByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var audioFile = await handler.HandleAsync(new GetAudioFileByIdQuery(id), cancellationToken);
        return audioFile is null ? Results.NotFound() : Results.Ok(audioFile);
    }
}
