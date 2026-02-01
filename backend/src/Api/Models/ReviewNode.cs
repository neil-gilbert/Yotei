namespace Yotei.Api.Models;

/// <summary>
/// Represents a review node in the comprehension tree.
/// </summary>
public class ReviewNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReviewSessionId { get; set; }
    public ReviewSession? ReviewSession { get; set; }
    public Guid? ParentId { get; set; }
    public ReviewNode? Parent { get; set; }
    public List<ReviewNode> Children { get; set; } = [];
    public string NodeType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ChangeType { get; set; } = "modified";
    public List<string> RiskTags { get; set; } = [];
    public string RiskSeverity { get; set; } = "low";
    public List<string> Evidence { get; set; } = [];
    public string? Path { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
