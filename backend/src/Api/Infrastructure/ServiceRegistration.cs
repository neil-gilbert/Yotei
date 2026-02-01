using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Yotei.Api.Data;
using Yotei.Api.Features.Flow.Inference;
using Yotei.Api.Features.Ingestion;
using Yotei.Api.Features.ReviewSessions;
using Yotei.Api.Features.Flow;
using Yotei.Api.Storage;

namespace Yotei.Api.Infrastructure;

public static class ServiceRegistration
{
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
                var connectionString = configuration.GetConnectionString("Postgres");
                options.UseNpgsql(connectionString);
            });
        }

        services.Configure<StorageSettings>(configuration.GetSection("Storage"));
        services.Configure<GitHubSettings>(configuration.GetSection("GitHub"));
        services.Configure<OpenAiSettings>(configuration.GetSection("OpenAI"));
        services.Configure<FlowInferenceOptions>(configuration.GetSection("FlowInference"));
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
        services.AddHttpClient<IGithubIngestionService, GithubIngestionService>();
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
}
