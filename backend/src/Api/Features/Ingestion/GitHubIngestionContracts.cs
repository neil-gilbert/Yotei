namespace Yotei.Api.Features.Ingestion;

/// <summary>
/// Request payload for GitHub pull-based ingestion.
/// </summary>
/// <param name="Owner">The repository owner.</param>
/// <param name="Name">The repository name.</param>
/// <param name="PrNumber">The pull request number.</param>
public record GitHubIngestRequest(string Owner, string Name, int PrNumber);

/// <summary>
/// Request payload for posting a GitHub pull request comment.
/// </summary>
/// <param name="Owner">The repository owner.</param>
/// <param name="Name">The repository name.</param>
/// <param name="PrNumber">The pull request number.</param>
/// <param name="Body">The comment body to post.</param>
public record GitHubPullRequestCommentRequest(string Owner, string Name, int PrNumber, string Body);

/// <summary>
/// API response describing the ingestion result.
/// </summary>
/// <param name="SnapshotId">The snapshot identifier created or reused.</param>
/// <param name="Created">Whether a new snapshot was created.</param>
/// <param name="FileChangesCount">How many file changes were captured.</param>
public record GitHubIngestResponse(Guid SnapshotId, bool Created, int FileChangesCount);

/// <summary>
/// API response describing a GitHub sync operation.
/// </summary>
/// <param name="Repositories">Number of repositories processed.</param>
/// <param name="PullRequests">Number of pull requests scanned.</param>
/// <param name="SnapshotsCreated">Number of snapshots created during sync.</param>
/// <param name="Errors">Any errors captured during sync.</param>
public record GitHubSyncResponse(int Repositories, int PullRequests, int SnapshotsCreated, List<string> Errors);

/// <summary>
/// Internal result for ingestion operations.
/// </summary>
/// <param name="SnapshotId">The snapshot identifier created or reused.</param>
/// <param name="Created">Whether a new snapshot was created.</param>
/// <param name="FileChangesCount">The number of file changes ingested.</param>
/// <param name="Errors">Errors that occurred during ingestion.</param>
public record GitHubIngestResult(Guid? SnapshotId, bool Created, int FileChangesCount, List<string> Errors);

/// <summary>
/// Internal result for sync operations.
/// </summary>
/// <param name="Repositories">Number of repositories processed.</param>
/// <param name="PullRequests">Number of pull requests scanned.</param>
/// <param name="SnapshotsCreated">Number of snapshots created during sync.</param>
/// <param name="Errors">Errors captured during sync.</param>
public record GitHubSyncResult(int Repositories, int PullRequests, int SnapshotsCreated, List<string> Errors);

// Provides the marker used to find the Yotei pull request comment for updates.
internal static class GitHubCommentMarkers
{
    public const string YoteiReviewLink = "<!-- yotei:review-link -->";
}
