namespace Yotei.Api.Models;

public class PullRequestSnapshot
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Repository? Repository { get; set; }

    public int PrNumber { get; set; }
    public string BaseSha { get; set; } = string.Empty;
    public string HeadSha { get; set; } = string.Empty;
    public string Source { get; set; } = "fixture";
    public string? Title { get; set; }
    public DateTimeOffset IngestedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<FileChange> FileChanges { get; set; } = [];
}
