namespace Yotei.Api.Models;

/// <summary>
/// Stores a persisted review checklist for a review node.
/// </summary>
public class ReviewNodeChecklist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReviewNodeId { get; set; }
    public ReviewNode? ReviewNode { get; set; }
    public List<string> Items { get; set; } = [];
    public List<ReviewChecklistItem> ItemsDetailed { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
