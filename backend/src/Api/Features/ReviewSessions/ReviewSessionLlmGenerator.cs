using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yotei.Api.Features.Tenancy;
using Yotei.Api.Infrastructure;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Yotei.Api.Features.ReviewSessions;

/// <summary>
/// Represents the combined LLM output for a review session.
/// </summary>
public sealed record ReviewSessionLlmResult(
    ReviewSummaryOverview? Summary,
    IReadOnlyDictionary<Guid, ReviewNodeBehaviourSummary> BehaviourSummaries,
    IReadOnlyDictionary<Guid, ReviewNodeQuestions> Questions)
{
    /// <summary>
    /// Empty LLM result when OpenAI is unavailable or yields invalid output.
    /// </summary>
    public static ReviewSessionLlmResult Empty { get; } =
        new(null, new Dictionary<Guid, ReviewNodeBehaviourSummary>(), new Dictionary<Guid, ReviewNodeQuestions>());
}

/// <summary>
/// Generates a combined OpenAI response for a review session (summary + per-file outputs).
/// </summary>
public sealed class ReviewSessionLlmGenerator
{
    private readonly IOpenAiClient _openAiClient;
    private readonly IRawDiffStorage _rawDiffStorage;
    private readonly OpenAiSettings _settings;
    private readonly OpenAiReviewLimitsOptions _limitOptions;
    private readonly TenantContext _tenantContext;
    private readonly ILogger<ReviewSessionLlmGenerator> _logger;

    /// <summary>
    /// Creates a combined review session generator with OpenAI and fallback dependencies.
    /// </summary>
    /// <param name="openAiClient">The OpenAI client wrapper.</param>
    /// <param name="rawDiffStorage">Raw diff storage for fetching diffs.</param>
    /// <param name="settings">OpenAI configuration settings.</param>
    /// <param name="limitOptions">Limit options for prompt sizing.</param>
    /// <param name="tenantContext">Resolved tenant context for per-tenant limits.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public ReviewSessionLlmGenerator(
        IOpenAiClient openAiClient,
        IRawDiffStorage rawDiffStorage,
        IOptions<OpenAiSettings> settings,
        IOptions<OpenAiReviewLimitsOptions> limitOptions,
        TenantContext tenantContext,
        ILogger<ReviewSessionLlmGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(openAiClient);
        ArgumentNullException.ThrowIfNull(rawDiffStorage);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(limitOptions);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(logger);

        _openAiClient = openAiClient;
        _rawDiffStorage = rawDiffStorage;
        _settings = settings.Value;
        _limitOptions = limitOptions.Value;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Generates a combined summary and file outputs using a single OpenAI call.
    /// </summary>
    /// <param name="snapshot">Snapshot metadata with file changes.</param>
    /// <param name="summary">The computed review summary stats.</param>
    /// <param name="nodes">The review nodes associated with the session.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The combined LLM result, or an empty payload when unavailable.</returns>
    public async Task<ReviewSessionLlmResult> GenerateAsync(
        PullRequestSnapshot snapshot,
        ReviewSummary summary,
        IReadOnlyList<ReviewNode> nodes,
        CancellationToken cancellationToken)
    {
        if (!IsOpenAiConfigured())
        {
            return ReviewSessionLlmResult.Empty;
        }

        var limits = ResolveLimits();
        var candidates = BuildFileCandidates(snapshot, nodes);
        if (candidates.Count == 0)
        {
            return ReviewSessionLlmResult.Empty;
        }

        var selected = candidates
            .OrderByDescending(candidate => candidate.Churn)
            .ThenByDescending(candidate => candidate.Node.RiskTags.Count)
            .ThenBy(candidate => candidate.Node.Path ?? candidate.Node.Label, StringComparer.OrdinalIgnoreCase)
            .Take(limits.MaxFiles)
            .ToList();

        var promptFiles = await BuildPromptFilesAsync(selected, limits, cancellationToken);
        if (promptFiles.Count == 0)
        {
            return ReviewSessionLlmResult.Empty;
        }

        var repoSlug = snapshot.Repository is null
            ? "unknown/unknown"
            : $"{snapshot.Repository.Owner}/{snapshot.Repository.Name}";

        var prompt = new OpenAiReviewSessionPrompt(
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
            promptFiles);

        var response = await _openAiClient.GenerateReviewSessionAsync(prompt, cancellationToken);
        if (response is null)
        {
            _logger.LogWarning("OpenAI review session response was empty.");
            return ReviewSessionLlmResult.Empty;
        }

        var outputs = response.Files ?? Array.Empty<OpenAiReviewSessionFileOutput>();
        var summaryOverview = BuildSummaryOverview(response.Summary);
        var behaviourSummaries = BuildBehaviourSummaries(outputs, selected);
        var questions = BuildQuestions(outputs, selected, limits.MaxQuestionsPerFile);

        if (summaryOverview is null && behaviourSummaries.Count == 0 && questions.Count == 0)
        {
            _logger.LogWarning("OpenAI review session response had no usable content.");
            return ReviewSessionLlmResult.Empty;
        }

        return new ReviewSessionLlmResult(summaryOverview, behaviourSummaries, questions);
    }

    // Builds the list of file candidates eligible for the combined prompt.
    private static List<ReviewSessionFileCandidate> BuildFileCandidates(
        PullRequestSnapshot snapshot,
        IReadOnlyList<ReviewNode> nodes)
    {
        var changesByPath = snapshot.FileChanges
            .Where(change => !string.IsNullOrWhiteSpace(change.Path))
            .ToDictionary(change => change.Path!, StringComparer.OrdinalIgnoreCase);

        var candidates = new List<ReviewSessionFileCandidate>();
        foreach (var node in nodes)
        {
            if (node.NodeType != "file" || string.IsNullOrWhiteSpace(node.Path))
            {
                continue;
            }

            if (!changesByPath.TryGetValue(node.Path, out var change))
            {
                continue;
            }

            var churn = change.AddedLines + change.DeletedLines;
            candidates.Add(new ReviewSessionFileCandidate(node, change, churn));
        }

        return candidates;
    }

    // Builds the OpenAI prompt files for the selected nodes.
    private async Task<IReadOnlyList<OpenAiReviewSessionFile>> BuildPromptFilesAsync(
        IReadOnlyList<ReviewSessionFileCandidate> candidates,
        OpenAiReviewLimits limits,
        CancellationToken cancellationToken)
    {
        var results = new List<OpenAiReviewSessionFile>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var diff = await LoadDiffAsync(candidate.Change, cancellationToken);
            var trimmedDiff = TrimDiff(diff, limits.MaxDiffCharactersPerFile);
            var riskTags = candidate.Node.RiskTags
                .Take(limits.MaxRiskTagsPerFile)
                .ToList();
            var evidence = candidate.Node.Evidence
                .Take(limits.MaxEvidenceItemsPerFile)
                .ToList();
            var path = string.IsNullOrWhiteSpace(candidate.Change.Path)
                ? candidate.Node.Path ?? candidate.Node.Label
                : candidate.Change.Path;

            results.Add(new OpenAiReviewSessionFile(
                candidate.Node.Id,
                path,
                candidate.Change.ChangeType ?? "modified",
                candidate.Change.AddedLines,
                candidate.Change.DeletedLines,
                riskTags,
                evidence,
                trimmedDiff));
        }

        return results;
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
    private static string TrimDiff(string diff, int maxCharacters)
    {
        if (maxCharacters <= 0)
        {
            return string.Empty;
        }

        if (diff.Length <= maxCharacters)
        {
            return diff;
        }

        return diff[..maxCharacters];
    }

    // Builds the session-level summary output when the LLM response is valid.
    private static ReviewSummaryOverview? BuildSummaryOverview(OpenAiReviewSummary? summary)
    {
        if (summary is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(summary.OverallSummary) ||
            string.IsNullOrWhiteSpace(summary.BeforeState) ||
            string.IsNullOrWhiteSpace(summary.AfterState))
        {
            return null;
        }

        return new ReviewSummaryOverview(
            summary.OverallSummary.Trim(),
            summary.BeforeState.Trim(),
            summary.AfterState.Trim());
    }

    // Builds behavior summaries from the OpenAI file outputs.
    private Dictionary<Guid, ReviewNodeBehaviourSummary> BuildBehaviourSummaries(
        IReadOnlyList<OpenAiReviewSessionFileOutput> outputs,
        IReadOnlyList<ReviewSessionFileCandidate> candidates)
    {
        var results = new Dictionary<Guid, ReviewNodeBehaviourSummary>();
        if (outputs.Count == 0)
        {
            return results;
        }

        var candidatesById = candidates.ToDictionary(candidate => candidate.Node.Id);
        var candidatesByPath = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Node.Path))
            .ToDictionary(candidate => candidate.Node.Path!, StringComparer.OrdinalIgnoreCase);

        foreach (var output in outputs)
        {
            if (!TryResolveCandidate(output, candidatesById, candidatesByPath, out var candidate) || candidate is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(output.BehaviourChange) ||
                string.IsNullOrWhiteSpace(output.Scope) ||
                string.IsNullOrWhiteSpace(output.ReviewerFocus))
            {
                continue;
            }

            var summary = new ReviewNodeBehaviourSummary
            {
                ReviewNodeId = candidate.Node.Id,
                BehaviourChange = output.BehaviourChange.Trim(),
                Scope = output.Scope.Trim(),
                ReviewerFocus = output.ReviewerFocus.Trim()
            };

            results[candidate.Node.Id] = summary;
        }

        return results;
    }

    // Builds reviewer question outputs from the OpenAI file outputs.
    private Dictionary<Guid, ReviewNodeQuestions> BuildQuestions(
        IReadOnlyList<OpenAiReviewSessionFileOutput> outputs,
        IReadOnlyList<ReviewSessionFileCandidate> candidates,
        int maxQuestions)
    {
        var results = new Dictionary<Guid, ReviewNodeQuestions>();
        if (outputs.Count == 0)
        {
            return results;
        }

        var candidatesById = candidates.ToDictionary(candidate => candidate.Node.Id);
        var candidatesByPath = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Node.Path))
            .ToDictionary(candidate => candidate.Node.Path!, StringComparer.OrdinalIgnoreCase);

        foreach (var output in outputs)
        {
            if (!TryResolveCandidate(output, candidatesById, candidatesByPath, out var candidate) || candidate is null)
            {
                continue;
            }

            var items = SanitizeQuestions(output.Questions, maxQuestions);
            if (items.Count == 0)
            {
                continue;
            }

            results[candidate.Node.Id] = new ReviewNodeQuestions
            {
                ReviewNodeId = candidate.Node.Id,
                Items = items,
                Source = "llm"
            };
        }

        return results;
    }

    // Sanitizes, deduplicates, and caps reviewer questions.
    private static List<string> SanitizeQuestions(IReadOnlyList<string>? questions, int maxQuestions)
    {
        if (questions is null || questions.Count == 0)
        {
            return [];
        }

        var limit = Math.Max(1, maxQuestions);
        return questions
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    // Resolves a response output to a known file candidate.
    private static bool TryResolveCandidate(
        OpenAiReviewSessionFileOutput output,
        IReadOnlyDictionary<Guid, ReviewSessionFileCandidate> candidatesById,
        IReadOnlyDictionary<string, ReviewSessionFileCandidate> candidatesByPath,
        out ReviewSessionFileCandidate? candidate)
    {
        if (Guid.TryParse(output.NodeId, out var nodeId) && candidatesById.TryGetValue(nodeId, out candidate))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(output.Path) && candidatesByPath.TryGetValue(output.Path, out candidate))
        {
            return true;
        }

        candidate = null;
        return false;
    }

    // Resolves effective prompt limits using tenant overrides when configured.
    private OpenAiReviewLimits ResolveLimits()
    {
        var defaults = _limitOptions.Default ?? new OpenAiReviewLimits();
        if (!_tenantContext.IsResolved || _limitOptions.Tenants.Count == 0)
        {
            return NormalizeLimits(defaults);
        }

        var overrideValue = TryFindTenantOverride();
        if (overrideValue is null)
        {
            return NormalizeLimits(defaults);
        }

        var merged = ApplyOverride(defaults, overrideValue);
        return NormalizeLimits(merged);
    }

    // Finds a tenant override using slug, id, or name.
    private OpenAiReviewLimitOverride? TryFindTenantOverride()
    {
        var keys = new[]
        {
            _tenantContext.TenantSlug,
            _tenantContext.TenantId.ToString(),
            _tenantContext.TenantName
        };

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (_limitOptions.Tenants.TryGetValue(key, out var overrideValue))
            {
                return overrideValue;
            }

            var match = _limitOptions.Tenants
                .FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Key))
            {
                return match.Value;
            }
        }

        return null;
    }

    // Applies a tenant override on top of default limits.
    private static OpenAiReviewLimits ApplyOverride(OpenAiReviewLimits defaults, OpenAiReviewLimitOverride overrideValue)
    {
        return new OpenAiReviewLimits
        {
            MaxFiles = overrideValue.MaxFiles ?? defaults.MaxFiles,
            MaxDiffCharactersPerFile = overrideValue.MaxDiffCharactersPerFile ?? defaults.MaxDiffCharactersPerFile,
            MaxQuestionsPerFile = overrideValue.MaxQuestionsPerFile ?? defaults.MaxQuestionsPerFile,
            MaxRiskTagsPerFile = overrideValue.MaxRiskTagsPerFile ?? defaults.MaxRiskTagsPerFile,
            MaxEvidenceItemsPerFile = overrideValue.MaxEvidenceItemsPerFile ?? defaults.MaxEvidenceItemsPerFile
        };
    }

    // Normalizes limit values to safe minimums.
    private static OpenAiReviewLimits NormalizeLimits(OpenAiReviewLimits limits)
    {
        return new OpenAiReviewLimits
        {
            MaxFiles = Math.Max(1, limits.MaxFiles),
            MaxDiffCharactersPerFile = Math.Max(0, limits.MaxDiffCharactersPerFile),
            MaxQuestionsPerFile = Math.Max(1, limits.MaxQuestionsPerFile),
            MaxRiskTagsPerFile = Math.Max(0, limits.MaxRiskTagsPerFile),
            MaxEvidenceItemsPerFile = Math.Max(0, limits.MaxEvidenceItemsPerFile)
        };
    }

    // Determines if OpenAI configuration is present.
    private bool IsOpenAiConfigured()
    {
        return !string.IsNullOrWhiteSpace(_settings.ApiKey)
            && !string.IsNullOrWhiteSpace(_settings.Model);
    }

    private sealed record ReviewSessionFileCandidate(ReviewNode Node, FileChange Change, int Churn);
}
