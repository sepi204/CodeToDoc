using System.Text.Json;
using System.Text.Json.Serialization;
using CodeToDoc.Api;
using CodeToDoc.Infrastructure;
using CodeToDoc.Service;
using CodeToDoc.Service.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Allow large multipart uploads; the actual archive limits are enforced by the service.
const long maxRequestBodyBytes = 300L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxRequestBodyBytes;
    options.ValueLengthLimit = int.MaxValue;
});

var app = builder.Build();

// Validate strongly typed options at startup so invalid configuration fails clearly.
{
    var phaseOneOptions = app.Services.GetRequiredService<IOptions<PhaseOneProcessingOptions>>().Value;
    var validator = app.Services.GetRequiredService<IValidateOptions<PhaseOneProcessingOptions>>();
    var validationResult = validator.Validate(null, phaseOneOptions);
    if (validationResult.Failed)
    {
        throw new InvalidOperationException(
            $"Invalid {PhaseOneProcessingOptions.SectionName} configuration: {string.Join("; ", validationResult.Failures)}");
    }

    var phaseThreeOptions = app.Services.GetRequiredService<IOptions<PhaseThreeProcessingOptions>>().Value;
    var phaseThreeValidator = app.Services.GetRequiredService<IValidateOptions<PhaseThreeProcessingOptions>>();
    var phaseThreeResult = phaseThreeValidator.Validate(null, phaseThreeOptions);
    if (phaseThreeResult.Failed)
    {
        throw new InvalidOperationException(
            $"Invalid {PhaseThreeProcessingOptions.SectionName} configuration: {string.Join("; ", phaseThreeResult.Failures)}");
    }

    var llmOptions = app.Services.GetRequiredService<IOptions<LlmOptions>>().Value;
    var llmValidator = app.Services.GetRequiredService<IValidateOptions<LlmOptions>>();
    var llmResult = llmValidator.Validate(null, llmOptions);
    if (llmResult.Failed)
    {
        throw new InvalidOperationException(
            $"Invalid {LlmOptions.SectionName} configuration: {string.Join("; ", llmResult.Failures)}");
    }

    var artifactStorageOptions = app.Services.GetRequiredService<IOptions<ArtifactStorageOptions>>().Value;
    if (artifactStorageOptions.MaxDownloadBytes <= 0 || artifactStorageOptions.MaxArtifactBytes <= 0)
    {
        throw new InvalidOperationException(
            $"Invalid {ArtifactStorageOptions.SectionName} configuration: size limits must be greater than zero.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseExceptionHandler();
app.MapControllers();

app.Run();

public partial class Program
{
}
