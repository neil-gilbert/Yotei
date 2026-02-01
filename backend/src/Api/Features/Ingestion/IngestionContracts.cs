namespace Yotei.Api.Features.Ingestion;

public record IngestSnapshotRequest(
    string Owner,
    string Name,
    int PrNumber,
    string BaseSha,
    string HeadSha,
    string? DefaultBranch,
    string? Source,
    string? Title);
