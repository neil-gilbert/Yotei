using Yotei.Api.Models;

namespace Yotei.Api.Features.ReviewSessions;

/// <summary>
/// Builds deterministic fallback summaries for review sessions.
/// </summary>
public static class ReviewSummaryFallbackBuilder
{
    /// <summary>
    /// Builds a fallback summary overview using heuristic metadata.
    /// </summary>
    /// <param name="snapshot">Snapshot metadata with file changes.</param>
    /// <param name="summary">Computed summary stats.</param>
    /// <returns>A populated summary overview.</returns>
    public static ReviewSummaryOverview Build(PullRequestSnapshot snapshot, ReviewSummary summary)
    {
        var entryPoints = summary.EntryPoints.Count > 0
            ? string.Join(", ", summary.EntryPoints)
            : "primary entry points";
        var topPaths = summary.TopPaths.Count > 0
            ? string.Join(", ", summary.TopPaths)
            : "key paths";
        var sideEffects = summary.SideEffects.Count > 0
            ? string.Join(", ", summary.SideEffects)
            : "no notable side effects";
        var riskTags = summary.RiskTags.Count > 0
            ? string.Join(", ", summary.RiskTags)
            : "no elevated risk tags";
        var testTouched = snapshot.FileChanges.Any(change =>
            change.Path.Contains("test", StringComparison.OrdinalIgnoreCase) ||
            change.Path.Contains("spec", StringComparison.OrdinalIgnoreCase));

        var overall = $"I touched {summary.ChangedFilesCount} files, mostly around {topPaths}.";
        var before = $"Before this, {entryPoints} handled the flow with {sideEffects}.";
        var after = $"Now those paths are updated, and we're calling out {riskTags}.";

        if (testTouched)
        {
            after += " I also added/updated tests to cover the change.";
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Title))
        {
            overall = $"{snapshot.Title.Trim()} — I updated {summary.ChangedFilesCount} files across {topPaths}.";
        }

        return new ReviewSummaryOverview(overall, before, after);
    }
}
