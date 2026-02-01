namespace Yotei.Api.Models;

public class FileChange
{
    public Guid Id { get; set; }
    public Guid PullRequestSnapshotId { get; set; }
    public PullRequestSnapshot? PullRequestSnapshot { get; set; }

    public string Path { get; set; } = string.Empty;
    public string ChangeType { get; set; } = "modified";
    public int AddedLines { get; set; }
    public int DeletedLines { get; set; }
    public string? RawDiffRef { get; set; }
    public string? RawDiffText { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
