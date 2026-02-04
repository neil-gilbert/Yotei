using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Yotei.Api.Features.FileChanges;
using Yotei.Api.Features.Health;
using Yotei.Api.Features.Ingestion;
using Yotei.Api.Features.Snapshots;
using Yotei.Api.Features.ReviewSessions;
using Yotei.Api.Features.Storage;
using Yotei.Api.Features.ChangeTree;
using Yotei.Api.Features.Explanations;
using Yotei.Api.Features.Flow;
using Yotei.Api.Features.Insights;
using Yotei.Api.Features.Tenancy;
using Yotei.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

DisableConfigFileReload(builder.Configuration);

builder.Services.AddYoteiInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    var configuredOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>();
    var allowedOrigins = (configuredOrigins ?? Array.Empty<string>())
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (allowedOrigins.Length == 0)
    {
        allowedOrigins = new[]
        {
            "http://localhost:5173",
            "http://127.0.0.1:5173"
        };
    }

    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length == 1 && allowedOrigins[0] == "*")
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
    });
});

var app = builder.Build();

await app.InitializeAsync();

app.UseCors("Default");
app.UseMiddleware<TenantResolverMiddleware>();

app.MapHealthEndpoints();
app.MapIngestionEndpoints();
app.MapTenantOnboardingEndpoints();
app.MapSnapshotEndpoints();
app.MapReviewSessionEndpoints();
app.MapFileChangeEndpoints();
app.MapRawDiffEndpoints();
app.MapChangeTreeEndpoints();
app.MapExplanationEndpoints();
app.MapFlowEndpoints();
app.MapInsightsEndpoints();

await app.RunAsync();

// Disables file watcher reloads to avoid inotify/FD limits in constrained environments.
static void DisableConfigFileReload(ConfigurationManager configuration)
{
    foreach (var source in configuration.Sources.OfType<JsonConfigurationSource>())
    {
        source.ReloadOnChange = false;
    }
}

public partial class Program
{
}
