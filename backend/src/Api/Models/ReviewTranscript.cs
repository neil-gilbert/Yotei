namespace Yotei.Api.Models;

/// <summary>
/// Stores a voice query transcript and response for a review node.
/// </summary>
public class ReviewTranscript
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReviewSessionId { get; set; }
    public Guid ReviewNodeId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
