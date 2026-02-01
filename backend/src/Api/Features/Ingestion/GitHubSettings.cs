namespace Yotei.Api.Features.Ingestion;

/// <summary>
/// Configuration settings for GitHub ingestion.
/// </summary>
public record GitHubSettings
{
    public string BaseUrl { get; init; } = "https://api.github.com";
    public string? Token { get; init; }
    public string[] Repos { get; init; } = [];
    public int SyncIntervalMinutes { get; init; } = 10;
}
