namespace Yotei.Api.Models;

/// <summary>
/// Represents a locally stored raw diff blob used by database-backed storage.
/// </summary>
public class RawDiffBlob
{
    public Guid Id { get; set; }
    public Guid PullRequestSnapshotId { get; set; }
    public PullRequestSnapshot? PullRequestSnapshot { get; set; }
    public string Path { get; set; } = string.Empty;
    public string DiffText { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
