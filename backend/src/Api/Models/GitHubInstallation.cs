namespace Yotei.Api.Models;

/// <summary>
/// Tracks a GitHub App installation linked to a tenant.
/// </summary>
public class GitHubInstallation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public long InstallationId { get; set; }
    public string AccountLogin { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
