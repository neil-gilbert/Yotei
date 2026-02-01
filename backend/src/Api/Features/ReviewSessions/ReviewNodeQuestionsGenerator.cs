using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yotei.Api.Infrastructure;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Yotei.Api.Features.ReviewSessions;

/// <summary>
/// Generates reviewer questions for file nodes using OpenAI with heuristic fallback.
/// </summary>
public sealed class ReviewNodeQuestionsGenerator
{
    private const int MaxDiffCharacters = 8000;
    private const int MaxQuestions = 6;
    private readonly IOpenAiClient _openAiClient;
    private readonly IRawDiffStorage _rawDiffStorage;
    private readonly OpenAiSettings _settings;
    private readonly ReviewNodeInsightsGenerator _fallbackGenerator;
    private readonly ILogger<ReviewNodeQuestionsGenerator> _logger;

    /// <summary>
    /// Creates a reviewer questions generator with OpenAI and fallback dependencies.
    /// </summary>
    /// <param name="openAiClient">The OpenAI client wrapper.</param>
    /// <param name="rawDiffStorage">Raw diff storage for fetching diffs.</param>
    /// <param name="settings">OpenAI configuration settings.</param>
    /// <param name="fallbackGenerator">Deterministic heuristic generator.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public ReviewNodeQuestionsGenerator(
        IOpenAiClient openAiClient,
        IRawDiffStorage rawDiffStorage,
        IOptions<OpenAiSettings> settings,
        ReviewNodeInsightsGenerator fallbackGenerator,
        ILogger<ReviewNodeQuestionsGenerator> logger)
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
    /// Generates reviewer questions using OpenAI when configured, falling back to heuristics.
    /// </summary>
    /// <param name="fileNode">The file-level review node.</param>
    /// <param name="change">The file change metadata.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A populated reviewer questions entity.</returns>
    public async Task<ReviewNodeQuestions> GenerateAsync(
        ReviewNode fileNode,
        FileChange change,
        CancellationToken cancellationToken)
    {
        if (!IsOpenAiConfigured())
        {
            return BuildFallbackQuestions(fileNode);
        }

        var diff = await LoadDiffAsync(change, cancellationToken);
        var trimmedDiff = TrimDiff(diff);
        var prompt = new OpenAiReviewerQuestionsPrompt(
            fileNode.Path ?? fileNode.Label,
            change.ChangeType,
            trimmedDiff,
            fileNode.RiskTags,
            fileNode.Evidence);

        var questions = await _openAiClient.GenerateReviewerQuestionsAsync(prompt, cancellationToken);
        var items = SanitizeQuestions(questions?.Questions);
        if (items.Count == 0)
        {
            _logger.LogWarning(
                "OpenAI reviewer questions fallback used for node {NodeId} at {Path}.",
                fileNode.Id,
                fileNode.Path ?? fileNode.Label);
            return BuildFallbackQuestions(fileNode);
        }

        return new ReviewNodeQuestions
        {
            ReviewNodeId = fileNode.Id,
            Items = items,
            Source = "llm"
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

    // Normalizes, deduplicates, and caps reviewer questions.
    private static List<string> SanitizeQuestions(IReadOnlyList<string>? questions)
    {
        if (questions is null || questions.Count == 0)
        {
            return [];
        }

        return questions
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxQuestions)
            .ToList();
    }

    // Builds the deterministic fallback questions from the heuristic checklist.
    private ReviewNodeQuestions BuildFallbackQuestions(ReviewNode fileNode)
    {
        var checklist = _fallbackGenerator.BuildChecklist(fileNode);
        return new ReviewNodeQuestions
        {
            ReviewNodeId = fileNode.Id,
            Items = checklist.Items,
            Source = "heuristic"
        };
    }

    // Determines if OpenAI configuration is present.
    private bool IsOpenAiConfigured()
    {
        return !string.IsNullOrWhiteSpace(_settings.ApiKey)
            && !string.IsNullOrWhiteSpace(_settings.Model);
    }
}
