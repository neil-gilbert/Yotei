namespace Yotei.Api.Models;

public class ChangeNodeExplanation
{
    public Guid Id { get; set; }
    public Guid ChangeNodeId { get; set; }
    public ChangeNode? ChangeNode { get; set; }
    public string Model { get; set; } = "stub";
    public string Prompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
