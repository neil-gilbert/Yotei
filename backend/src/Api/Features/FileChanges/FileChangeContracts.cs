namespace Yotei.Api.Features.FileChanges;

public record FileChangeBatchRequest(List<FileChangeRequest>? Changes);

public record FileChangeRequest(
    string Path,
    string? ChangeType,
    int AddedLines,
    int DeletedLines,
    string? RawDiffRef,
    string? RawDiffText);

public record FileChangeItem(
    string Path,
    string ChangeType,
    int AddedLines,
    int DeletedLines,
    string? RawDiffRef,
    DateTimeOffset CreatedAt);
