using AudioThread.Api.Configuration;
using AudioThread.Api.Endpoints;
using AudioThread.Application;
using AudioThread.Infrastructure;
using AudioThread.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

const string FrontendCorsPolicy = "FrontendCorsPolicy";
string[] defaultAllowedOrigins = ["http://localhost:5173", "http://localhost:4173"];

var builder = WebApplication.CreateBuilder(args);

var envFilePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env"));
builder.Configuration.AddEnvFile(envFilePath);
builder.Configuration.AddEnvironmentVariables();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOpenApi();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = builder.Configuration
        .GetValue<long?>("AudioUpload:MaxFileSizeInBytes") ?? 50 * 1024 * 1024;
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();

if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    allowedOrigins = defaultAllowedOrigins;
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:RunMigrationsOnStartup"))
{
    await ApplyMigrationsAsync(app);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health")
    .ExcludeFromDescription();
app.MapAudioFilesEndpoints();
app.MapAudioProcessingJobsEndpoints();

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("AudioThread.Api.Startup");

    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AudioThreadDbContext>();
        logger.LogInformation("Applying pending database migrations");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied");
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "The database migrations could not be applied");
        throw;
    }
}
