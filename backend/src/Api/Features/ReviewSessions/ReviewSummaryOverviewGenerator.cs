using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yotei.Api.Infrastructure;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Yotei.Api.Features.ReviewSessions;

/// <summary>
/// Generates an overall review summary with OpenAI and a deterministic fallback.
/// </summary>
public sealed class ReviewSummaryOverviewGenerator
{
    private const int MaxFiles = 4;
    private const int MaxDiffCharacters = 6000;
    private readonly IOpenAiClient _openAiClient;
    private readonly IRawDiffStorage _rawDiffStorage;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<ReviewSummaryOverviewGenerator> _logger;

    /// <summary>
    /// Creates a review summary generator with OpenAI and diff storage dependencies.
    /// </summary>
    /// <param name="openAiClient">The OpenAI client wrapper.</param>
    /// <param name="rawDiffStorage">Raw diff storage for fetching diffs.</param>
    /// <param name="settings">OpenAI configuration settings.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public ReviewSummaryOverviewGenerator(
        IOpenAiClient openAiClient,
        IRawDiffStorage rawDiffStorage,
        IOptions<OpenAiSettings> settings,
        ILogger<ReviewSummaryOverviewGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(openAiClient);
        ArgumentNullException.ThrowIfNull(rawDiffStorage);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _openAiClient = openAiClient;
        _rawDiffStorage = rawDiffStorage;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Generates an overall summary for the review session.
    /// </summary>
    /// <param name="snapshot">Snapshot metadata with file changes.</param>
    /// <param name="summary">The computed review summary stats.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A summary overview with overall, before, and after descriptions.</returns>
    public async Task<ReviewSummaryOverview> GenerateAsync(
        PullRequestSnapshot snapshot,
        ReviewSummary summary,
        CancellationToken cancellationToken)
    {
        if (!IsOpenAiConfigured())
        {
            return BuildFallbackSummary(snapshot, summary);
        }

        var repoSlug = snapshot.Repository is null
            ? "unknown/unknown"
            : $"{snapshot.Repository.Owner}/{snapshot.Repository.Name}";
        var files = await BuildFilePromptsAsync(snapshot, cancellationToken);

        var prompt = new OpenAiReviewSummaryPrompt(
            repoSlug,
            snapshot.PrNumber,
            snapshot.Title,
            summary.ChangedFilesCount,
            summary.NewFilesCount,
            summary.ModifiedFilesCount,
            summary.DeletedFilesCount,
            summary.EntryPoints,
            summary.SideEffects,
            summary.RiskTags,
            summary.TopPaths,
            files);

        var response = await _openAiClient.GenerateReviewSummaryAsync(prompt, cancellationToken);
        if (!IsValidSummary(response))
        {
            _logger.LogWarning(
                "OpenAI summary fallback used for review session {SessionId}.",
                summary.ReviewSessionId);
            return BuildFallbackSummary(snapshot, summary);
        }

        return new ReviewSummaryOverview(
            response!.OverallSummary.Trim(),
            response.BeforeState.Trim(),
            response.AfterState.Trim());
    }

    /// <summary>
    /// Builds the list of file-level prompt data for OpenAI.
    /// </summary>
    private async Task<IReadOnlyList<OpenAiReviewSummaryFile>> BuildFilePromptsAsync(
        PullRequestSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var files = snapshot.FileChanges
            .OrderByDescending(change => change.AddedLines + change.DeletedLines)
            .Take(MaxFiles)
            .ToList();

        var results = new List<OpenAiReviewSummaryFile>(files.Count);
        foreach (var change in files)
        {
            var diff = await LoadDiffAsync(change, cancellationToken);
            var trimmedDiff = TrimDiff(diff);
            var path = string.IsNullOrWhiteSpace(change.Path) ? "unknown" : change.Path;
            results.Add(new OpenAiReviewSummaryFile(
                path,
                change.ChangeType ?? "modified",
                change.AddedLines,
                change.DeletedLines,
                string.IsNullOrWhiteSpace(trimmedDiff) ? "(diff unavailable)" : trimmedDiff));
        }

        return results;
    }

    /// <summary>
    /// Loads the raw diff from either inline text or storage.
    /// </summary>
    private async Task<string> LoadDiffAsync(FileChange change, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(change.RawDiffText))
        {
            return change.RawDiffText;
        }

        if (string.IsNullOrWhiteSpace(change.RawDiffRef))
        {
            return string.Empty;
        }

        var diff = await _rawDiffStorage.GetDiffAsync(change.RawDiffRef, cancellationToken);
        return diff ?? string.Empty;
    }

    /// <summary>
    /// Ensures the diff passed to the model stays within a safe character limit.
    /// </summary>
    private static string TrimDiff(string diff)
    {
        if (diff.Length <= MaxDiffCharacters)
        {
            return diff;
        }

        return diff[..MaxDiffCharacters];
    }

    /// <summary>
    /// Builds a deterministic fallback summary when OpenAI is unavailable.
    /// </summary>
    private static ReviewSummaryOverview BuildFallbackSummary(
        PullRequestSnapshot snapshot,
        ReviewSummary summary)
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
        var after = $"Now those paths are updated, and we’re calling out {riskTags}.";

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

    /// <summary>
    /// Checks that the OpenAI response includes all required fields.
    /// </summary>
    private static bool IsValidSummary(OpenAiReviewSummary? summary)
    {
        return summary is not null
            && !string.IsNullOrWhiteSpace(summary.OverallSummary)
            && !string.IsNullOrWhiteSpace(summary.BeforeState)
            && !string.IsNullOrWhiteSpace(summary.AfterState);
    }

    /// <summary>
    /// Determines if OpenAI configuration is present.
    /// </summary>
    private bool IsOpenAiConfigured()
    {
        return !string.IsNullOrWhiteSpace(_settings.ApiKey)
            && !string.IsNullOrWhiteSpace(_settings.Model);
    }
}

/// <summary>
/// Represents the generated overview fields for a review summary.
/// </summary>
public sealed record ReviewSummaryOverview(
    string OverallSummary,
    string BeforeState,
    string AfterState);
