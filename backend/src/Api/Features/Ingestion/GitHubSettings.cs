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
    /// <summary>
    /// GitHub App authentication settings.
    /// </summary>
    public GitHubAppSettings App { get; init; } = new();
}

/// <summary>
/// Configuration settings for GitHub App authentication.
/// </summary>
public record GitHubAppSettings
{
    /// <summary>
    /// The numeric GitHub App identifier.
    /// </summary>
    public long AppId { get; init; }

    /// <summary>
    /// Optional GitHub installation identifier used as a fallback for single-tenant setups.
    /// </summary>
    public long InstallationId { get; init; }

    /// <summary>
    /// The PEM or base64-encoded private key for the GitHub App.
    /// </summary>
    public string PrivateKey { get; init; } = string.Empty;

    /// <summary>
    /// The webhook secret used to validate GitHub webhook signatures.
    /// </summary>
    public string WebhookSecret { get; init; } = string.Empty;
}
