namespace Yotei.Api.Models;

public class ChangeTree
{
    public Guid Id { get; set; }
    public Guid PullRequestSnapshotId { get; set; }
    public PullRequestSnapshot? PullRequestSnapshot { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ChangeNode> Nodes { get; set; } = [];
}
