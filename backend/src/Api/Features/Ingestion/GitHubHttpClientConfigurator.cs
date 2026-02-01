using System.Net.Http.Headers;

namespace Yotei.Api.Features.Ingestion;

/// <summary>
/// Configures HttpClient instances for GitHub REST API requests.
/// </summary>
public static class GitHubHttpClientConfigurator
{
    /// <summary>
    /// The shared HttpClient name used for GitHub API calls.
    /// </summary>
    public const string ClientName = "GitHub";

    /// <summary>
    /// Applies base address and default headers required by GitHub.
    /// </summary>
    /// <param name="client">The client instance to configure.</param>
    /// <param name="settings">GitHub configuration settings.</param>
    public static void Configure(HttpClient client, GitHubSettings settings)
    {
        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("GitHub base URL is invalid.");
        }

        client.BaseAddress = baseUri;
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Yotei/0.1");
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }
}
