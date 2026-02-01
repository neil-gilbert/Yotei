namespace Yotei.Api.Features.ChangeTree;

public record ChangeTreeResponse(
    Guid TreeId,
    Guid SnapshotId,
    DateTimeOffset CreatedAt,
    List<ChangeNodeResponse> Nodes);

public record ChangeNodeResponse(
    Guid Id,
    Guid? ParentId,
    string NodeType,
    string Label,
    string? Path,
    string? ChangeType,
    int AddedLines,
    int DeletedLines,
    string? RawDiffRef);

public record ChangeTreeBuildResponse(Guid TreeId, int NodeCount);
