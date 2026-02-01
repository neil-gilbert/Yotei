namespace Yotei.Api.Models;

public class ChangeNode
{
    public Guid Id { get; set; }
    public Guid ChangeTreeId { get; set; }
    public ChangeTree? ChangeTree { get; set; }
    public Guid? ParentId { get; set; }
    public ChangeNode? Parent { get; set; }

    public string NodeType { get; set; } = "file";
    public string Label { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? ChangeType { get; set; }
    public int AddedLines { get; set; }
    public int DeletedLines { get; set; }
    public string? RawDiffRef { get; set; }
}
