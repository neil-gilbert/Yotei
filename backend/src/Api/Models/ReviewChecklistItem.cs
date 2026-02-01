namespace Yotei.Api.Models;

/// <summary>
/// Stores a checklist item with source metadata.
/// </summary>
public class ReviewChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReviewNodeChecklistId { get; set; }
    public ReviewNodeChecklist? ReviewNodeChecklist { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Source { get; set; } = "heuristic";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
