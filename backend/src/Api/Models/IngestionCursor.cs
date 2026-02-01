namespace Yotei.Api.Models;

/// <summary>
/// Tracks ingestion sync progress per repository.
/// </summary>
public class IngestionCursor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RepositoryId { get; set; }
    public Repository? Repository { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? LastHeadSha { get; set; }
    public int? LastPrNumber { get; set; }
}
