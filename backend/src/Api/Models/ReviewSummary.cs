namespace Yotei.Api.Models;

/// <summary>
/// Stores the persisted summary for a review session.
/// </summary>
public class ReviewSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReviewSessionId { get; set; }
    public ReviewSession? ReviewSession { get; set; }
    public int ChangedFilesCount { get; set; }
    public int NewFilesCount { get; set; }
    public int ModifiedFilesCount { get; set; }
    public int DeletedFilesCount { get; set; }
    public string OverallSummary { get; set; } = string.Empty;
    public string BeforeState { get; set; } = string.Empty;
    public string AfterState { get; set; } = string.Empty;
    public List<string> EntryPoints { get; set; } = [];
    public List<string> SideEffects { get; set; } = [];
    public List<string> RiskTags { get; set; } = [];
    public List<string> TopPaths { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
