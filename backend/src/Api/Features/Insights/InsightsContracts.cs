namespace Yotei.Api.Features.Insights;

/// <summary>
/// Represents aggregated org-wide insights for review sessions.
/// </summary>
/// <example>
/// <code>
/// {
///   "from": "2025-01-01T00:00:00Z",
///   "to": "2025-01-31T00:00:00Z",
///   "repo": "octo/repo",
///   "reviewSessionCount": 12,
///   "reviewSummaryCount": 8,
///   "repositories": [],
///   "riskTags": [],
///   "hotPaths": [],
///   "reviewVolume": []
/// }
/// </code>
/// </example>
public record OrgInsightsResponse(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Repo,
    int ReviewSessionCount,
    int ReviewSummaryCount,
    IReadOnlyList<RepoInsightItem> Repositories,
    IReadOnlyList<TagCountItem> RiskTags,
    IReadOnlyList<TagCountItem> HotPaths,
    IReadOnlyList<ReviewVolumeItem> ReviewVolume);

/// <summary>
/// Represents counts per repository.
/// </summary>
/// <example>
/// <code>
/// {
///   "owner": "octo",
///   "name": "repo",
///   "reviewSessionCount": 5
/// }
/// </code>
/// </example>
public record RepoInsightItem(
    string Owner,
    string Name,
    int ReviewSessionCount);

/// <summary>
/// Represents a tag or path with an aggregate count.
/// </summary>
/// <example>
/// <code>
/// {
///   "label": "auth",
///   "count": 3
/// }
/// </code>
/// </example>
public record TagCountItem(
    string Label,
    int Count);

/// <summary>
/// Represents review volume aggregated by day.
/// </summary>
/// <example>
/// <code>
/// {
///   "date": "2025-01-01T00:00:00Z",
///   "count": 2
/// }
/// </code>
/// </example>
public record ReviewVolumeItem(
    DateTimeOffset Date,
    int Count);
