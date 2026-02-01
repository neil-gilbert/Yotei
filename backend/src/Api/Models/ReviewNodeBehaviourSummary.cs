namespace Yotei.Api.Models;

/// <summary>
/// Stores a persisted behavior summary for a review node.
/// </summary>
public class ReviewNodeBehaviourSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReviewNodeId { get; set; }
    public ReviewNode? ReviewNode { get; set; }
    public string BehaviourChange { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string ReviewerFocus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
