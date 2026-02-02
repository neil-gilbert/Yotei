namespace Yotei.Api.Infrastructure;

/// <summary>
/// Configuration settings for frontend integration.
/// </summary>
public record FrontendSettings
{
    /// <summary>
    /// Base URL for the frontend application.
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:5173";
}
