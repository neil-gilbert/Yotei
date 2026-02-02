namespace Yotei.Api.Models;

/// <summary>
/// Represents a customer tenant scoped by a shared access token.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Repository> Repositories { get; set; } = [];
    public List<GitHubInstallation> GitHubInstallations { get; set; } = [];
}
