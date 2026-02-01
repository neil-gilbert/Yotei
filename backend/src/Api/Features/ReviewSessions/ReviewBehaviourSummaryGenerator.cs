using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Yotei.Api.Infrastructure;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Yotei.Api.Features.ReviewSessions;

/// <summary>
/// Generates behavior summaries for review nodes using OpenAI with deterministic fallback.
/// </summary>
public sealed class ReviewBehaviourSummaryGenerator
{
    private const int MaxDiffCharacters = 8000;
    private readonly IOpenAiClient _openAiClient;
    private readonly IRawDiffStorage _rawDiffStorage;
    private readonly OpenAiSettings _settings;
    private readonly ReviewNodeInsightsGenerator _fallbackGenerator;
    private readonly ILogger<ReviewBehaviourSummaryGenerator> _logger;

    /// <summary>
    /// Creates a review behavior summary generator with OpenAI and fallback dependencies.
    /// </summary>
    /// <param name="openAiClient">The OpenAI client wrapper.</param>
    /// <param name="rawDiffStorage">Raw diff storage for fetching diffs.</param>
    /// <param name="settings">OpenAI configuration settings.</param>
    /// <param name="fallbackGenerator">Deterministic heuristic generator.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public ReviewBehaviourSummaryGenerator(
        IOpenAiClient openAiClient,
        IRawDiffStorage rawDiffStorage,
        IOptions<OpenAiSettings> settings,
        ReviewNodeInsightsGenerator fallbackGenerator,
        ILogger<ReviewBehaviourSummaryGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(openAiClient);
        ArgumentNullException.ThrowIfNull(rawDiffStorage);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(fallbackGenerator);
        ArgumentNullException.ThrowIfNull(logger);

        _openAiClient = openAiClient;
        _rawDiffStorage = rawDiffStorage;
        _settings = settings.Value;
        _fallbackGenerator = fallbackGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Generates a behavior summary using OpenAI when configured, falling back to heuristics.
    /// </summary>
    /// <param name="fileNode">The file-level review node.</param>
    /// <param name="change">The file change metadata.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A populated behavior summary entity.</returns>
    public async Task<ReviewNodeBehaviourSummary> GenerateAsync(
        ReviewNode fileNode,
        FileChange change,
        CancellationToken cancellationToken)
    {
        if (!IsOpenAiConfigured())
        {
            return BuildFallbackSummary(fileNode, change);
        }

        var diff = await LoadDiffAsync(change, cancellationToken);
        var trimmedDiff = TrimDiff(diff);

        var prompt = new OpenAiBehaviourSummaryPrompt(
            fileNode.Path ?? fileNode.Label,
            change.ChangeType,
            trimmedDiff,
            fileNode.RiskTags,
            fileNode.Evidence);

        var summary = await _openAiClient.GenerateBehaviourSummaryAsync(prompt, cancellationToken);
        if (!IsValidSummary(summary))
        {
            _logger.LogWarning(
                "OpenAI summary fallback used for node {NodeId} at {Path}.",
                fileNode.Id,
                fileNode.Path ?? fileNode.Label);
            return BuildFallbackSummary(fileNode, change);
        }

        return new ReviewNodeBehaviourSummary
        {
            ReviewNodeId = fileNode.Id,
            BehaviourChange = summary!.BehaviourChange.Trim(),
            Scope = summary.Scope.Trim(),
            ReviewerFocus = summary.ReviewerFocus.Trim()
        };
    }

    // Loads the raw diff from either inline text or storage.
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

    // Ensures the diff passed to the model stays within a safe character limit.
    private static string TrimDiff(string diff)
    {
        if (diff.Length <= MaxDiffCharacters)
        {
            return diff;
        }

        return diff[..MaxDiffCharacters];
    }

    // Checks that the OpenAI summary includes all required fields.
    private static bool IsValidSummary(OpenAiBehaviourSummary? summary)
    {
        return summary is not null
            && !string.IsNullOrWhiteSpace(summary.BehaviourChange)
            && !string.IsNullOrWhiteSpace(summary.Scope)
            && !string.IsNullOrWhiteSpace(summary.ReviewerFocus);
    }

    // Builds the deterministic fallback summary.
    private ReviewNodeBehaviourSummary BuildFallbackSummary(ReviewNode fileNode, FileChange change)
    {
        return _fallbackGenerator.BuildBehaviourSummary(fileNode, change);
    }

    // Determines if OpenAI configuration is present.
    private bool IsOpenAiConfigured()
    {
        return !string.IsNullOrWhiteSpace(_settings.ApiKey)
            && !string.IsNullOrWhiteSpace(_settings.Model);
    }
}
