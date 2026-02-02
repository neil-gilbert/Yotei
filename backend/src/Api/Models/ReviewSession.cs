namespace Yotei.Api.Models;

/// <summary>
/// Represents a review session backed by a snapshot for deterministic analysis.
/// </summary>
public class ReviewSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid PullRequestSnapshotId { get; set; }
    public PullRequestSnapshot? PullRequestSnapshot { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ReviewSummary? Summary { get; set; }
    public List<ReviewNode> Nodes { get; set; } = [];
}
