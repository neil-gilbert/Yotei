namespace Yotei.Api.Models;

public class Repository
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<PullRequestSnapshot> PullRequestSnapshots { get; set; } = [];
}
