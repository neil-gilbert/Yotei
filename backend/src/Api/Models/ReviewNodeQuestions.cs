namespace Yotei.Api.Models;

/// <summary>
/// Stores LLM or heuristic reviewer questions for a review node.
/// </summary>
public class ReviewNodeQuestions
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReviewNodeId { get; set; }
    public ReviewNode? ReviewNode { get; set; }
    public List<string> Items { get; set; } = [];
    public string Source { get; set; } = "heuristic";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
