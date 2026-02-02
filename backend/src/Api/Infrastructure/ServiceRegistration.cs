using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Yotei.Api.Data;
using Yotei.Api.Features.Flow.Inference;
using Yotei.Api.Features.Ingestion;
using Yotei.Api.Features.ReviewSessions;
using Yotei.Api.Features.Flow;
using Yotei.Api.Storage;
using Yotei.Api.Features.Tenancy;

namespace Yotei.Api.Infrastructure;

public static class ServiceRegistration
{
    /// <summary>
    /// Registers infrastructure services, storage, and integrations for the API.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <param name="configuration">The app configuration values.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddYoteiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks();

        var provider = configuration.GetValue<string>("Database:Provider") ?? "Postgres";
        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            var databaseName = configuration.GetValue<string>("Database:Name") ?? $"yotei-{Guid.NewGuid()}";
            services.AddDbContext<YoteiDbContext>(options => options.UseInMemoryDatabase(databaseName));
        }
        else
        {
            services.AddDbContext<YoteiDbContext>(options =>
            {
                var connectionString = NormalizePostgresConnectionString(
                    configuration.GetConnectionString("Postgres"));
                options.UseNpgsql(connectionString);
            });
        }

        services.Configure<StorageSettings>(configuration.GetSection("Storage"));
        services.Configure<GitHubSettings>(configuration.GetSection("GitHub"));
        services.Configure<FrontendSettings>(configuration.GetSection("Frontend"));
        services.Configure<TenancySettings>(configuration.GetSection("Tenancy"));
        services.Configure<OpenAiSettings>(configuration.GetSection("OpenAI"));
        services.Configure<FlowInferenceOptions>(configuration.GetSection("FlowInference"));
        services.AddScoped<TenantContext>();
        services.AddScoped<TenantResolverMiddleware>();
        services.AddScoped<TenantProvisioningService>();
        services.AddSingleton<IGitHubAppJwtFactory, GitHubAppJwtFactory>();
        services.AddSingleton<IGitHubAccessTokenProvider, GitHubAccessTokenProvider>();
        services.AddHttpClient(GitHubHttpClientConfigurator.ClientName, (provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<GitHubSettings>>().Value;
            GitHubHttpClientConfigurator.Configure(client, settings);
        });
        services.AddHttpClient<IGitHubInstallationClient, GitHubInstallationClient>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<GitHubSettings>>().Value;
            GitHubHttpClientConfigurator.Configure(client, settings);
        });
        var storageProvider = configuration.GetValue<string>("Storage:Provider") ?? "S3";
        if (string.Equals(storageProvider, "Database", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IRawDiffStorage, DatabaseRawDiffStorage>();
        }
        else
        {
            services.AddScoped<IRawDiffStorage, S3RawDiffStorage>();
        }
        services.AddSingleton<IExplanationGenerator, StubExplanationGenerator>();
        services.AddSingleton<IReviewExplanationGenerator, StubReviewExplanationGenerator>();
        services.AddScoped<ReviewTreeBuilder>();
        services.AddScoped<ReviewSummaryOverviewGenerator>();
        services.AddSingleton<ReviewNodeInsightsGenerator>();
        services.AddScoped<ReviewBehaviourSummaryGenerator>();
        services.AddScoped<ReviewNodeQuestionsGenerator>();
        services.AddScoped<ReviewVoiceQueryGenerator>();
        services.AddScoped<FlowGraphBuilder>();
        services.AddSingleton<IFlowInferenceAdapter, CSharpFlowInferenceAdapter>();
        services.AddSingleton<IFlowInferenceAdapter, JavaScriptFlowInferenceAdapter>();
        services.AddScoped<FlowInferenceAdapterRegistry>();
        services.AddHttpClient<IGithubIngestionService, GithubIngestionService>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<GitHubSettings>>().Value;
            GitHubHttpClientConfigurator.Configure(client, settings);
        });
        services.AddHttpClient<IOpenAiClient, OpenAiClient>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<OpenAiSettings>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
                ? "https://api.openai.com/v1/"
                : settings.BaseUrl;
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }

    /// <summary>
    /// Normalizes Render-style Postgres URLs into Npgsql key/value connection strings.
    /// </summary>
    private static string? NormalizePostgresConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/').Trim();
        var port = uri.IsDefaultPort ? 5432 : uri.Port;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }
}
