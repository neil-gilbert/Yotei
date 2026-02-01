using Yotei.Api.Features.FileChanges;

namespace Yotei.Api.Features.Snapshots;

public record SnapshotListItem(
    Guid Id,
    string Owner,
    string Name,
    int PrNumber,
    string BaseSha,
    string HeadSha,
    string? Title,
    DateTimeOffset IngestedAt);

public record SnapshotDetail(
    Guid Id,
    string Owner,
    string Name,
    int PrNumber,
    string BaseSha,
    string HeadSha,
    string? Title,
    string Source,
    string DefaultBranch,
    DateTimeOffset IngestedAt,
    List<FileChangeItem> FileChanges);
