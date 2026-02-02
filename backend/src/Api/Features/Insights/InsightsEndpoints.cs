using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Features.Tenancy;

namespace Yotei.Api.Features.Insights;

/// <summary>
/// Maps org-wide insights endpoints.
/// </summary>
public static class InsightsEndpoints
{
    /// <summary>
    /// Registers org-wide insights endpoints on the route builder.
    /// </summary>
    /// <param name="app">The route builder used to register endpoints.</param>
    /// <returns>The same route builder instance for chaining.</returns>
    public static IEndpointRouteBuilder MapInsightsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/insights/org", async (
            string? from,
            string? to,
            string? repo,
            TenantContext tenantContext,
            YoteiDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseDate(from, out var fromDate, out var fromError))
            {
                return Results.BadRequest(new { error = fromError });
            }

            if (!TryParseDate(to, out var toDate, out var toError))
            {
                return Results.BadRequest(new { error = toError });
            }

            var snapshotQuery = db.PullRequestSnapshots
                .AsNoTracking()
                .Include(snapshot => snapshot.Repository)
                .Where(snapshot => snapshot.TenantId == tenantContext.TenantId)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                snapshotQuery = snapshotQuery.Where(snapshot => snapshot.IngestedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                snapshotQuery = snapshotQuery.Where(snapshot => snapshot.IngestedAt <= toDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(repo))
            {
                var repoFilter = repo.Trim();
                var repoParts = repoFilter.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (repoParts.Length == 2)
                {
                    snapshotQuery = snapshotQuery.Where(snapshot =>
                        snapshot.Repository != null &&
                        snapshot.Repository.Owner.Equals(repoParts[0], StringComparison.OrdinalIgnoreCase) &&
                        snapshot.Repository.Name.Equals(repoParts[1], StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    snapshotQuery = snapshotQuery.Where(snapshot =>
                        snapshot.Repository != null &&
                        snapshot.Repository.Name.Equals(repoFilter, StringComparison.OrdinalIgnoreCase));
                }
            }

            var snapshots = await snapshotQuery.ToListAsync(cancellationToken);
            var snapshotIds = snapshots.Select(snapshot => snapshot.Id).ToList();

            var summaries = snapshotIds.Count > 0
                ? await db.ReviewSummaries
                    .AsNoTracking()
                    .Where(summary => snapshotIds.Contains(summary.ReviewSessionId))
                    .ToListAsync(cancellationToken)
                : [];

            var repoCounts = snapshots
                .Where(snapshot => snapshot.Repository is not null)
                .GroupBy(snapshot => $"{snapshot.Repository!.Owner}/{snapshot.Repository.Name}", StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var parts = group.Key.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    var owner = parts.Length > 0 ? parts[0] : "unknown";
                    var name = parts.Length > 1 ? parts[1] : "unknown";
                    return new RepoInsightItem(owner, name, group.Count());
                })
                .OrderByDescending(item => item.ReviewSessionCount)
                .ThenBy(item => item.Owner, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var riskTags = summaries
                .SelectMany(summary => summary.RiskTags)
                .GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TagCountItem(group.Key, group.Count()))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var hotPaths = summaries
                .SelectMany(summary => summary.TopPaths)
                .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TagCountItem(group.Key, group.Count()))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var reviewVolume = snapshots
                .GroupBy(snapshot => snapshot.IngestedAt.UtcDateTime.Date)
                .Select(group => new ReviewVolumeItem(
                    new DateTimeOffset(group.Key, TimeSpan.Zero),
                    group.Count()))
                .OrderBy(item => item.Date)
                .ToList();

            var response = new OrgInsightsResponse(
                fromDate,
                toDate,
                string.IsNullOrWhiteSpace(repo) ? null : repo.Trim(),
                snapshots.Count,
                summaries.Count,
                repoCounts,
                riskTags,
                hotPaths,
                reviewVolume);

            return Results.Ok(response);
        });

        return app;
    }

    // Parses optional date query values.
    private static bool TryParseDate(string? input, out DateTimeOffset? date, out string? error)
    {
        date = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(input, out var parsed))
        {
            error = "Invalid date format. Use ISO-8601 timestamps.";
            return false;
        }

        date = parsed;
        return true;
    }
}
