namespace Yotei.Api.Models;

/// <summary>
/// Stores the explanation generated for a review node.
/// </summary>
public class ReviewNodeExplanation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReviewNodeId { get; set; }
    public ReviewNode? ReviewNode { get; set; }
    public string Response { get; set; } = string.Empty;
    public string Source { get; set; } = "heuristic";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
