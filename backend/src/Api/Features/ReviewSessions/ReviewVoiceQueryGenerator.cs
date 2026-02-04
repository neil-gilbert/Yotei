using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yotei.Api.Infrastructure;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Yotei.Api.Features.ReviewSessions;

/// <summary>
/// Generates conversational responses scoped to a review node with OpenAI fallback.
/// </summary>
public sealed class ReviewVoiceQueryGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IOpenAiClient _openAiClient;
    private readonly IRawDiffStorage _rawDiffStorage;
    private readonly OpenAiSettings _settings;
    private readonly OpenAiReviewLimitsOptions _limitOptions;
    private readonly ILogger<ReviewVoiceQueryGenerator> _logger;

    /// <summary>
    /// Creates a conversational answer generator with OpenAI dependencies.
    /// </summary>
    /// <param name="openAiClient">The OpenAI client wrapper.</param>
    /// <param name="rawDiffStorage">Raw diff storage for fetching diffs.</param>
    /// <param name="settings">OpenAI configuration settings.</param>
    /// <param name="limitOptions">Prompt sizing limits for diffs.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public ReviewVoiceQueryGenerator(
        IOpenAiClient openAiClient,
        IRawDiffStorage rawDiffStorage,
        IOptions<OpenAiSettings> settings,
        IOptions<OpenAiReviewLimitsOptions> limitOptions,
        ILogger<ReviewVoiceQueryGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(openAiClient);
        ArgumentNullException.ThrowIfNull(rawDiffStorage);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(limitOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _openAiClient = openAiClient;
        _rawDiffStorage = rawDiffStorage;
        _settings = settings.Value;
        _limitOptions = limitOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Generates a response to the provided question using OpenAI when configured.
    /// </summary>
    /// <param name="node">The review node being discussed.</param>
    /// <param name="question">The spoken question or transcript text.</param>
    /// <param name="snapshot">The review session snapshot metadata.</param>
    /// <param name="change">The file change for diff context when available.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A concise answer string scoped to the node.</returns>
    public async Task<string> GenerateAnswerAsync(
        ReviewNode node,
        string question,
        PullRequestSnapshot? snapshot,
        FileChange? change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        if (!IsOpenAiConfigured() || snapshot is null)
        {
            return BuildFallbackAnswer(node, question);
        }

        var diff = change is null
            ? string.Empty
            : await LoadDiffAsync(change, cancellationToken);
        var trimmedDiff = TrimDiff(diff, ResolveLimits().MaxDiffCharactersPerFile);
        var diffExcerpt = string.IsNullOrWhiteSpace(trimmedDiff)
            ? "(diff unavailable)"
            : trimmedDiff;
        var repository = snapshot.Repository is null
            ? "unknown/unknown"
            : $"{snapshot.Repository.Owner}/{snapshot.Repository.Name}";

        var prompt = new OpenAiConversationTurnPrompt(
            repository,
            snapshot.PrNumber,
            snapshot.Title,
            "text",
            node.Id.ToString(),
            node.Path ?? node.Label,
            "none",
            BuildNodeReviewJson(node),
            "none",
            diffExcerpt,
            question.Trim());

        var response = await _openAiClient.GenerateConversationTurnAsync(prompt, cancellationToken);
        if (response is not null && !string.IsNullOrWhiteSpace(response.Answer))
        {
            return response.Answer.Trim();
        }

        _logger.LogWarning(
            "OpenAI conversation response was empty for node {NodeId}.",
            node.Id);
        return BuildFallbackAnswer(node, question);
    }

    // Builds a deterministic fallback answer when OpenAI is unavailable.
    private static string BuildFallbackAnswer(ReviewNode node, string question)
    {
        var context = node.NodeType switch
        {
            "file" when !string.IsNullOrWhiteSpace(node.Path) => $"file {node.Path}",
            "risk" => $"risk tag {node.Label}",
            "side_effect" => $"side effect {node.Label}",
            _ => node.Label
        };

        var severity = string.IsNullOrWhiteSpace(node.RiskSeverity)
            ? "low"
            : node.RiskSeverity;

        var evidenceSnippet = node.Evidence.Count > 0
            ? string.Join(", ", node.Evidence.Take(3))
            : "no evidence captured";

        return $"For {context} (severity {severity}), the review focus is: {question.Trim()} " +
               $"Evidence signals include {evidenceSnippet}.";
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

    // Trims diffs to a maximum character limit.
    private static string TrimDiff(string diff, int maxCharacters)
    {
        var limit = Math.Max(200, maxCharacters);
        if (diff.Length <= limit)
        {
            return diff;
        }

        return diff[..limit];
    }

    // Builds a compact JSON string representing node context for the prompt.
    private static string BuildNodeReviewJson(ReviewNode node)
    {
        var payload = new
        {
            nodeId = node.Id,
            nodeType = node.NodeType,
            label = node.Label,
            path = node.Path,
            changeType = node.ChangeType,
            riskSeverity = node.RiskSeverity,
            riskTags = node.RiskTags,
            evidence = node.Evidence
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    // Resolves effective prompt limits.
    private OpenAiReviewLimits ResolveLimits()
    {
        var defaults = _limitOptions.Default ?? new OpenAiReviewLimits();
        return defaults;
    }

    // Determines if OpenAI configuration is present.
    private bool IsOpenAiConfigured()
    {
        return !string.IsNullOrWhiteSpace(_settings.ApiKey)
            && !string.IsNullOrWhiteSpace(_settings.Model);
    }
}
