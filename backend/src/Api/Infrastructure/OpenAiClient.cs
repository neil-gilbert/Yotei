using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Yotei.Api.Infrastructure;

/// <summary>
/// Configuration for the OpenAI API client used to generate structured summaries.
/// </summary>
public record OpenAiSettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.openai.com/v1/";
    public double? Temperature { get; init; } = 1;
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 3;
}

/// <summary>
/// Input payload for a behavior summary prompt sent to OpenAI.
/// </summary>
/// <param name="FilePath">The path of the changed file.</param>
/// <param name="ChangeType">The change type label.</param>
/// <param name="Diff">The raw diff text.</param>
/// <param name="RiskTags">Risk tags associated with the file node.</param>
/// <param name="Evidence">Evidence strings associated with the node.</param>
public sealed record OpenAiBehaviourSummaryPrompt(
    string FilePath,
    string ChangeType,
    string Diff,
    IReadOnlyList<string> RiskTags,
    IReadOnlyList<string> Evidence);

/// <summary>
/// Input payload for reviewer question generation.
/// </summary>
/// <param name="FilePath">The path of the changed file.</param>
/// <param name="ChangeType">The change type label.</param>
/// <param name="Diff">The raw diff text.</param>
/// <param name="RiskTags">Risk tags associated with the file node.</param>
/// <param name="Evidence">Evidence strings associated with the node.</param>
public sealed record OpenAiReviewerQuestionsPrompt(
    string FilePath,
    string ChangeType,
    string Diff,
    IReadOnlyList<string> RiskTags,
    IReadOnlyList<string> Evidence);

/// <summary>
/// Input payload for a review summary prompt sent to OpenAI.
/// </summary>
/// <param name="Repository">Repository slug in owner/name format.</param>
/// <param name="PrNumber">Pull request number.</param>
/// <param name="Title">Pull request title.</param>
/// <param name="ChangedFilesCount">Total changed file count.</param>
/// <param name="NewFilesCount">New files count.</param>
/// <param name="ModifiedFilesCount">Modified files count.</param>
/// <param name="DeletedFilesCount">Deleted files count.</param>
/// <param name="EntryPoints">Entry point paths.</param>
/// <param name="SideEffects">Side-effect tags.</param>
/// <param name="RiskTags">Risk tags.</param>
/// <param name="TopPaths">Top changed paths.</param>
/// <param name="Files">File-level diffs to summarize.</param>
public sealed record OpenAiReviewSummaryPrompt(
    string Repository,
    int PrNumber,
    string? Title,
    int ChangedFilesCount,
    int NewFilesCount,
    int ModifiedFilesCount,
    int DeletedFilesCount,
    IReadOnlyList<string> EntryPoints,
    IReadOnlyList<string> SideEffects,
    IReadOnlyList<string> RiskTags,
    IReadOnlyList<string> TopPaths,
    IReadOnlyList<OpenAiReviewSummaryFile> Files);

/// <summary>
/// Represents a file summary input for a review summary prompt.
/// </summary>
/// <param name="Path">File path.</param>
/// <param name="ChangeType">Change type label.</param>
/// <param name="AddedLines">Added lines count.</param>
/// <param name="DeletedLines">Deleted lines count.</param>
/// <param name="Diff">Raw diff excerpt.</param>
public sealed record OpenAiReviewSummaryFile(
    string Path,
    string ChangeType,
    int AddedLines,
    int DeletedLines,
    string Diff);

/// <summary>
/// Input payload for a combined review session prompt sent to OpenAI.
/// </summary>
/// <param name="Repository">Repository slug in owner/name format.</param>
/// <param name="PrNumber">Pull request number.</param>
/// <param name="Title">Pull request title.</param>
/// <param name="ChangedFilesCount">Total changed file count.</param>
/// <param name="NewFilesCount">New files count.</param>
/// <param name="ModifiedFilesCount">Modified files count.</param>
/// <param name="DeletedFilesCount">Deleted files count.</param>
/// <param name="EntryPoints">Entry point paths.</param>
/// <param name="SideEffects">Side-effect tags.</param>
/// <param name="RiskTags">Risk tags.</param>
/// <param name="TopPaths">Top changed paths.</param>
/// <param name="Files">File-level diffs to summarize.</param>
public sealed record OpenAiReviewSessionPrompt(
    string Repository,
    int PrNumber,
    string? Title,
    int ChangedFilesCount,
    int NewFilesCount,
    int ModifiedFilesCount,
    int DeletedFilesCount,
    IReadOnlyList<string> EntryPoints,
    IReadOnlyList<string> SideEffects,
    IReadOnlyList<string> RiskTags,
    IReadOnlyList<string> TopPaths,
    IReadOnlyList<OpenAiReviewSessionFile> Files);

/// <summary>
/// Represents a file summary input for a combined review session prompt.
/// </summary>
/// <param name="NodeId">The review node identifier.</param>
/// <param name="Path">File path.</param>
/// <param name="ChangeType">Change type label.</param>
/// <param name="AddedLines">Added lines count.</param>
/// <param name="DeletedLines">Deleted lines count.</param>
/// <param name="RiskTags">Risk tags for this file node.</param>
/// <param name="Evidence">Evidence tokens for this file node.</param>
/// <param name="Diff">Raw diff excerpt.</param>
public sealed record OpenAiReviewSessionFile(
    Guid NodeId,
    string Path,
    string ChangeType,
    int AddedLines,
    int DeletedLines,
    IReadOnlyList<string> RiskTags,
    IReadOnlyList<string> Evidence,
    string Diff);

/// <summary>
/// Structured OpenAI response for a behavior summary.
/// </summary>
/// <param name="BehaviourChange">Summary of behavioral change.</param>
/// <param name="Scope">Scope and confidence of the change.</param>
/// <param name="ReviewerFocus">Reviewer focus guidance.</param>
public sealed record OpenAiBehaviourSummary(
    [property: JsonPropertyName("behaviourChange")] string BehaviourChange,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("reviewerFocus")] string ReviewerFocus);

/// <summary>
/// Structured OpenAI response for reviewer questions.
/// </summary>
/// <param name="Questions">Targeted reviewer questions.</param>
public sealed record OpenAiReviewerQuestions(
    [property: JsonPropertyName("questions")] IReadOnlyList<string> Questions);

/// <summary>
/// Structured OpenAI response for a review summary overview.
/// </summary>
/// <param name="OverallSummary">High-level summary of the change.</param>
/// <param name="BeforeState">Description of the previous behavior or state.</param>
/// <param name="AfterState">Description of what the change introduces.</param>
public sealed record OpenAiReviewSummary(
    [property: JsonPropertyName("overallSummary")] string OverallSummary,
    [property: JsonPropertyName("beforeState")] string BeforeState,
    [property: JsonPropertyName("afterState")] string AfterState);

/// <summary>
/// Structured OpenAI response for a combined review session summary.
/// </summary>
/// <param name="Summary">Session-level summary payload.</param>
/// <param name="Files">Per-file review outputs.</param>
public sealed record OpenAiReviewSessionResponse(
    [property: JsonPropertyName("summary")] OpenAiReviewSummary Summary,
    [property: JsonPropertyName("files")] IReadOnlyList<OpenAiReviewSessionFileOutput> Files);

/// <summary>
/// Structured OpenAI response for a single file output in a combined review session.
/// </summary>
/// <param name="NodeId">The review node identifier.</param>
/// <param name="Path">File path.</param>
/// <param name="BehaviourChange">Summary of behavioral change.</param>
/// <param name="Scope">Scope and confidence of the change.</param>
/// <param name="ReviewerFocus">Reviewer focus guidance.</param>
/// <param name="Questions">Targeted reviewer questions.</param>
public sealed record OpenAiReviewSessionFileOutput(
    [property: JsonPropertyName("nodeId")] string NodeId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("behaviourChange")] string BehaviourChange,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("reviewerFocus")] string ReviewerFocus,
    [property: JsonPropertyName("questions")] IReadOnlyList<string> Questions);

/// <summary>
/// Minimal OpenAI client abstraction for structured behavior summaries.
/// </summary>
public interface IOpenAiClient
{
    /// <summary>
    /// Calls OpenAI chat completions to generate a behavior summary JSON payload.
    /// </summary>
    /// <param name="prompt">The prompt payload describing the change.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The parsed behavior summary or null on failure.</returns>
    Task<OpenAiBehaviourSummary?> GenerateBehaviourSummaryAsync(
        OpenAiBehaviourSummaryPrompt prompt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Calls OpenAI chat completions to generate reviewer questions as JSON.
    /// </summary>
    /// <param name="prompt">The prompt payload describing the change.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The parsed reviewer questions or null on failure.</returns>
    Task<OpenAiReviewerQuestions?> GenerateReviewerQuestionsAsync(
        OpenAiReviewerQuestionsPrompt prompt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Calls OpenAI chat completions to generate an overall review summary JSON payload.
    /// </summary>
    /// <param name="prompt">The prompt payload describing the review session.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The parsed review summary or null on failure.</returns>
    Task<OpenAiReviewSummary?> GenerateReviewSummaryAsync(
        OpenAiReviewSummaryPrompt prompt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Calls OpenAI chat completions to generate a combined review session JSON payload.
    /// </summary>
    /// <param name="prompt">The prompt payload describing the review session.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The parsed combined review session response or null on failure.</returns>
    Task<OpenAiReviewSessionResponse?> GenerateReviewSessionAsync(
        OpenAiReviewSessionPrompt prompt,
        CancellationToken cancellationToken);
}

/// <summary>
/// HTTP client wrapper for the OpenAI Chat Completions API used to generate structured JSON summaries.
/// </summary>
public sealed class OpenAiClient : IOpenAiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiPromptTemplates _promptTemplates;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiClient> _logger;
    private readonly OpenAiResponseFormat _behaviourSummaryFormat;
    private readonly OpenAiResponseFormat _reviewerQuestionsFormat;
    private readonly OpenAiResponseFormat _reviewSummaryFormat;
    private readonly OpenAiResponseFormat _reviewSessionFormat;

    /// <summary>
    /// Creates an OpenAI client wrapper with injected HTTP and configuration dependencies.
    /// </summary>
    /// <param name="httpClient">The HttpClient used to call OpenAI.</param>
    /// <param name="settings">The OpenAI configuration settings.</param>
    /// <param name="promptTemplates">The prompt templates and schema descriptions.</param>
    /// <param name="logger">Logger for diagnostics and retries.</param>
    public OpenAiClient(
        HttpClient httpClient,
        IOptions<OpenAiSettings> settings,
        IOptions<OpenAiPromptTemplates> promptTemplates,
        ILogger<OpenAiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(promptTemplates);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _settings = settings.Value;
        _promptTemplates = promptTemplates.Value;
        _logger = logger;
        _behaviourSummaryFormat = BuildBehaviourSummaryFormat(_promptTemplates.Schemas);
        _reviewerQuestionsFormat = BuildReviewerQuestionsFormat(_promptTemplates.Schemas);
        _reviewSummaryFormat = BuildReviewSummaryFormat(_promptTemplates.Schemas);
        _reviewSessionFormat = BuildReviewSessionFormat(_promptTemplates.Schemas);
    }

    /// <summary>
    /// Calls OpenAI for a structured behavior summary with retry and timeout handling.
    /// </summary>
    /// <param name="prompt">Prompt data including file path, diff, and risk evidence.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The structured behavior summary, or null when generation fails.</returns>
    public async Task<OpenAiBehaviourSummary?> GenerateBehaviourSummaryAsync(
        OpenAiBehaviourSummaryPrompt prompt,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            return null;
        }

        var requestPayload = BuildSummaryRequest(prompt);
        var requestJson = JsonSerializer.Serialize(requestPayload, JsonOptions);

        var result = await SendWithRetriesAsync(
            requestJson,
            TryParseSummary,
            cancellationToken);
        return result;
    }

    /// <summary>
    /// Calls OpenAI for structured reviewer questions with retry and timeout handling.
    /// </summary>
    /// <param name="prompt">Prompt data including file path, diff, and risk evidence.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The structured reviewer questions, or null when generation fails.</returns>
    public async Task<OpenAiReviewerQuestions?> GenerateReviewerQuestionsAsync(
        OpenAiReviewerQuestionsPrompt prompt,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            return null;
        }

        var requestPayload = BuildQuestionsRequest(prompt);
        var requestJson = JsonSerializer.Serialize(requestPayload, JsonOptions);

        var result = await SendWithRetriesAsync(
            requestJson,
            TryParseQuestions,
            cancellationToken);
        return result;
    }

    /// <summary>
    /// Calls OpenAI for a structured review summary with retry and timeout handling.
    /// </summary>
    /// <param name="prompt">Prompt data including key files and diff excerpts.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The structured review summary, or null when generation fails.</returns>
    public async Task<OpenAiReviewSummary?> GenerateReviewSummaryAsync(
        OpenAiReviewSummaryPrompt prompt,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            return null;
        }

        var requestPayload = BuildReviewSummaryRequest(prompt);
        var requestJson = JsonSerializer.Serialize(requestPayload, JsonOptions);

        var result = await SendWithRetriesAsync(
            requestJson,
            TryParseReviewSummary,
            cancellationToken);
        return result;
    }

    /// <summary>
    /// Calls OpenAI for a combined review session output with retry and timeout handling.
    /// </summary>
    /// <param name="prompt">Prompt data including summary stats and file-level diffs.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The structured review session response, or null when generation fails.</returns>
    public async Task<OpenAiReviewSessionResponse?> GenerateReviewSessionAsync(
        OpenAiReviewSessionPrompt prompt,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            return null;
        }

        var requestPayload = BuildReviewSessionRequest(prompt);
        var requestJson = JsonSerializer.Serialize(requestPayload, JsonOptions);

        var result = await SendWithRetriesAsync(
            requestJson,
            TryParseReviewSession,
            cancellationToken);
        return result;
    }

    // Builds the request payload for the OpenAI chat completions endpoint for summaries.
    private OpenAiChatCompletionRequest BuildSummaryRequest(OpenAiBehaviourSummaryPrompt prompt)
    {
        var systemPrompt = _promptTemplates.BehaviourSummarySystemPrompt;
        var userPrompt = BuildPromptBody(
            prompt.FilePath,
            prompt.ChangeType,
            prompt.Diff,
            prompt.RiskTags,
            prompt.Evidence);

        return new OpenAiChatCompletionRequest(
            _settings.Model,
            [
                new OpenAiChatMessage("system", systemPrompt),
                new OpenAiChatMessage("user", userPrompt)
            ],
            _behaviourSummaryFormat,
            _settings.Temperature);
    }

    // Builds the request payload for the OpenAI chat completions endpoint for questions.
    private OpenAiChatCompletionRequest BuildQuestionsRequest(OpenAiReviewerQuestionsPrompt prompt)
    {
        var systemPrompt = _promptTemplates.ReviewerQuestionsSystemPrompt;
        var userPrompt = BuildPromptBody(
            prompt.FilePath,
            prompt.ChangeType,
            prompt.Diff,
            prompt.RiskTags,
            prompt.Evidence);

        return new OpenAiChatCompletionRequest(
            _settings.Model,
            [
                new OpenAiChatMessage("system", systemPrompt),
                new OpenAiChatMessage("user", userPrompt)
            ],
            _reviewerQuestionsFormat,
            _settings.Temperature);
    }

    // Builds the request payload for the OpenAI chat completions endpoint for review summaries.
    private OpenAiChatCompletionRequest BuildReviewSummaryRequest(OpenAiReviewSummaryPrompt prompt)
    {
        var systemPrompt = _promptTemplates.ReviewSummarySystemPrompt;
        var userPrompt = BuildReviewSummaryPromptBody(prompt);

        return new OpenAiChatCompletionRequest(
            _settings.Model,
            [
                new OpenAiChatMessage("system", systemPrompt),
                new OpenAiChatMessage("user", userPrompt)
            ],
            _reviewSummaryFormat,
            _settings.Temperature);
    }

    // Builds the request payload for the OpenAI chat completions endpoint for combined review sessions.
    private OpenAiChatCompletionRequest BuildReviewSessionRequest(OpenAiReviewSessionPrompt prompt)
    {
        var systemPrompt = _promptTemplates.ReviewSessionSystemPrompt;
        var userPrompt = BuildReviewSessionPromptBody(prompt);

        return new OpenAiChatCompletionRequest(
            _settings.Model,
            [
                new OpenAiChatMessage("system", systemPrompt),
                new OpenAiChatMessage("user", userPrompt)
            ],
            _reviewSessionFormat,
            _settings.Temperature);
    }

    // Builds the prompt body including file path, diff, and risk evidence.
    private string BuildPromptBody(
        string filePath,
        string changeType,
        string diff,
        IReadOnlyList<string> riskTags,
        IReadOnlyList<string> evidence)
    {
        var riskText = riskTags.Count > 0
            ? string.Join(", ", riskTags)
            : _promptTemplates.NoneValue;
        var evidenceText = evidence.Count > 0
            ? string.Join(", ", evidence)
            : _promptTemplates.NoneValue;

        var tokens = new Dictionary<string, string>
        {
            ["FilePath"] = filePath,
            ["ChangeType"] = changeType,
            ["RiskTags"] = riskText,
            ["Evidence"] = evidenceText,
            ["Diff"] = diff
        };

        return ApplyTemplate(_promptTemplates.FilePromptTemplate, tokens);
    }

    // Builds the prompt body for review summary generation.
    private string BuildReviewSummaryPromptBody(OpenAiReviewSummaryPrompt prompt)
    {
        var entryPointsText = prompt.EntryPoints.Count > 0
            ? string.Join(", ", prompt.EntryPoints)
            : _promptTemplates.NoneValue;
        var sideEffectsText = prompt.SideEffects.Count > 0
            ? string.Join(", ", prompt.SideEffects)
            : _promptTemplates.NoneValue;
        var riskText = prompt.RiskTags.Count > 0
            ? string.Join(", ", prompt.RiskTags)
            : _promptTemplates.NoneValue;
        var topPathsText = prompt.TopPaths.Count > 0
            ? string.Join(", ", prompt.TopPaths)
            : _promptTemplates.NoneValue;

        var testFiles = prompt.Files
            .Select(file => file.Path)
            .Where(path => path.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("spec", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var testFilesText = testFiles.Count > 0
            ? string.Join(", ", testFiles)
            : _promptTemplates.NoneValue;

        var titleText = string.IsNullOrWhiteSpace(prompt.Title)
            ? _promptTemplates.UntitledValue
            : prompt.Title;

        var headerTokens = new Dictionary<string, string>
        {
            ["Repository"] = prompt.Repository,
            ["PrNumber"] = prompt.PrNumber.ToString(CultureInfo.InvariantCulture),
            ["Title"] = titleText,
            ["ChangedFilesCount"] = prompt.ChangedFilesCount.ToString(CultureInfo.InvariantCulture),
            ["NewFilesCount"] = prompt.NewFilesCount.ToString(CultureInfo.InvariantCulture),
            ["ModifiedFilesCount"] = prompt.ModifiedFilesCount.ToString(CultureInfo.InvariantCulture),
            ["DeletedFilesCount"] = prompt.DeletedFilesCount.ToString(CultureInfo.InvariantCulture),
            ["EntryPoints"] = entryPointsText,
            ["SideEffects"] = sideEffectsText,
            ["RiskTags"] = riskText,
            ["TopPaths"] = topPathsText,
            ["TestFiles"] = testFilesText
        };

        var builder = new StringBuilder();
        builder.Append(ApplyTemplate(_promptTemplates.ReviewSummaryHeaderTemplate, headerTokens));

        foreach (var file in prompt.Files)
        {
            var fileTokens = new Dictionary<string, string>
            {
                ["Path"] = file.Path,
                ["ChangeType"] = file.ChangeType,
                ["AddedLines"] = file.AddedLines.ToString(CultureInfo.InvariantCulture),
                ["DeletedLines"] = file.DeletedLines.ToString(CultureInfo.InvariantCulture)
            };
            builder.AppendLine(ApplyTemplate(_promptTemplates.ReviewSummaryFileTemplate, fileTokens));
            builder.AppendLine(_promptTemplates.ReviewSummaryDiffLabel);
            builder.AppendLine(file.Diff);
            builder.AppendLine(_promptTemplates.ReviewSummarySeparator);
        }

        return builder.ToString();
    }

    // Builds the prompt body for combined review session generation.
    private string BuildReviewSessionPromptBody(OpenAiReviewSessionPrompt prompt)
    {
        var entryPointsText = prompt.EntryPoints.Count > 0
            ? string.Join(", ", prompt.EntryPoints)
            : _promptTemplates.NoneValue;
        var sideEffectsText = prompt.SideEffects.Count > 0
            ? string.Join(", ", prompt.SideEffects)
            : _promptTemplates.NoneValue;
        var riskText = prompt.RiskTags.Count > 0
            ? string.Join(", ", prompt.RiskTags)
            : _promptTemplates.NoneValue;
        var topPathsText = prompt.TopPaths.Count > 0
            ? string.Join(", ", prompt.TopPaths)
            : _promptTemplates.NoneValue;

        var titleText = string.IsNullOrWhiteSpace(prompt.Title)
            ? _promptTemplates.UntitledValue
            : prompt.Title;

        var testFiles = prompt.Files
            .Select(file => file.Path)
            .Where(path => path.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("spec", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var testFilesText = testFiles.Count > 0
            ? string.Join(", ", testFiles)
            : _promptTemplates.NoneValue;

        var headerTokens = new Dictionary<string, string>
        {
            ["Repository"] = prompt.Repository,
            ["PrNumber"] = prompt.PrNumber.ToString(CultureInfo.InvariantCulture),
            ["Title"] = titleText,
            ["ChangedFilesCount"] = prompt.ChangedFilesCount.ToString(CultureInfo.InvariantCulture),
            ["NewFilesCount"] = prompt.NewFilesCount.ToString(CultureInfo.InvariantCulture),
            ["ModifiedFilesCount"] = prompt.ModifiedFilesCount.ToString(CultureInfo.InvariantCulture),
            ["DeletedFilesCount"] = prompt.DeletedFilesCount.ToString(CultureInfo.InvariantCulture),
            ["EntryPoints"] = entryPointsText,
            ["SideEffects"] = sideEffectsText,
            ["RiskTags"] = riskText,
            ["TopPaths"] = topPathsText,
            ["TestFiles"] = testFilesText
        };

        var builder = new StringBuilder();
        builder.Append(ApplyTemplate(_promptTemplates.ReviewSessionHeaderTemplate, headerTokens));

        for (var index = 0; index < prompt.Files.Count; index++)
        {
            var file = prompt.Files[index];
            var riskTagsText = file.RiskTags.Count > 0
                ? string.Join(", ", file.RiskTags)
                : _promptTemplates.NoneValue;
            var evidenceText = file.Evidence.Count > 0
                ? string.Join(", ", file.Evidence)
                : _promptTemplates.NoneValue;
            var diffText = string.IsNullOrWhiteSpace(file.Diff)
                ? _promptTemplates.DiffUnavailableValue
                : file.Diff;

            var fileTokens = new Dictionary<string, string>
            {
                ["Index"] = (index + 1).ToString(CultureInfo.InvariantCulture),
                ["NodeId"] = file.NodeId.ToString(),
                ["Path"] = file.Path,
                ["ChangeType"] = file.ChangeType,
                ["AddedLines"] = file.AddedLines.ToString(CultureInfo.InvariantCulture),
                ["DeletedLines"] = file.DeletedLines.ToString(CultureInfo.InvariantCulture),
                ["RiskTags"] = riskTagsText,
                ["Evidence"] = evidenceText
            };

            builder.AppendLine(ApplyTemplate(_promptTemplates.ReviewSessionFileTemplate, fileTokens));
            builder.AppendLine(_promptTemplates.ReviewSessionDiffLabel);
            builder.AppendLine(diffText);
            builder.AppendLine(_promptTemplates.ReviewSessionSeparator);
        }

        return builder.ToString();
    }

    // Applies placeholder tokens like {TokenName} in the template with provided values.
    private static string ApplyTemplate(string template, IReadOnlyDictionary<string, string> tokens)
    {
        var result = template;
        foreach (var (key, value) in tokens)
        {
            var token = $"{{{key}}}";
            result = result.Replace(token, value ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
    }

    // Sends the OpenAI request with retry and backoff for transient failures.
    private async Task<T?> SendWithRetriesAsync<T>(
        string requestJson,
        Func<string, T?> parser,
        CancellationToken cancellationToken)
        where T : class
    {
        var maxAttempts = Math.Max(1, _settings.MaxRetries);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var timeoutCts = CreateTimeoutToken(cancellationToken);
                var result = await SendOnceAsync(requestJson, parser, timeoutCts.Token);

                if (result.Payload is not null)
                {
                    return result.Payload;
                }

                if (!result.ShouldRetry || attempt == maxAttempts)
                {
                    return null;
                }
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "OpenAI request timed out on attempt {Attempt}.", attempt);

                if (attempt == maxAttempts)
                {
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "OpenAI request failed on attempt {Attempt}.", attempt);

                if (attempt == maxAttempts)
                {
                    return null;
                }
            }

            var delay = GetBackoffDelay(attempt);
            await Task.Delay(delay, cancellationToken);
        }

        return null;
    }

    // Sends a single OpenAI chat completion request and parses the JSON response.
    private async Task<OpenAiClientResult<T>> SendOnceAsync<T>(
        string requestJson,
        Func<string, T?> parser,
        CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "OpenAI request failed with status {StatusCode}. Body: {Body}",
                (int)response.StatusCode,
                body);

            var shouldRetry = IsRetryableStatus(response.StatusCode);
            return new OpenAiClientResult<T>(null, shouldRetry);
        }

        var payload = await response.Content.ReadFromJsonAsync<OpenAiChatCompletionResponse>(
            JsonOptions,
            cancellationToken);
        var content = payload?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("OpenAI response content was empty.");
            return new OpenAiClientResult<T>(null, false);
        }

        try
        {
            var parsed = parser(content);
            if (parsed is null)
            {
                _logger.LogWarning("OpenAI response parsed to null.");
            }

            return new OpenAiClientResult<T>(parsed, false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OpenAI response could not be parsed as JSON.");
            return new OpenAiClientResult<T>(null, false);
        }
    }

    // Parses the structured summary response.
    private OpenAiBehaviourSummary? TryParseSummary(string content)
    {
        return JsonSerializer.Deserialize<OpenAiBehaviourSummary>(content, JsonOptions);
    }

    // Parses the structured reviewer questions response.
    private OpenAiReviewerQuestions? TryParseQuestions(string content)
    {
        return JsonSerializer.Deserialize<OpenAiReviewerQuestions>(content, JsonOptions);
    }

    // Parses the structured review summary response.
    private OpenAiReviewSummary? TryParseReviewSummary(string content)
    {
        return JsonSerializer.Deserialize<OpenAiReviewSummary>(content, JsonOptions);
    }

    // Parses the structured review session response.
    private OpenAiReviewSessionResponse? TryParseReviewSession(string content)
    {
        return JsonSerializer.Deserialize<OpenAiReviewSessionResponse>(content, JsonOptions);
    }

    // Creates a linked cancellation token source with a configured timeout.
    private CancellationTokenSource CreateTimeoutToken(CancellationToken cancellationToken)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_settings.TimeoutSeconds > 0)
        {
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
        }

        return timeoutCts;
    }

    // Checks whether OpenAI configuration is present.
    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_settings.ApiKey)
            && !string.IsNullOrWhiteSpace(_settings.Model);
    }

    // Determines whether the status code is safe to retry.
    private static bool IsRetryableStatus(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => true,
            >= HttpStatusCode.InternalServerError => true,
            _ => false
        };
    }

    // Calculates exponential backoff delay for retries.
    private static TimeSpan GetBackoffDelay(int attempt)
    {
        var cappedAttempt = Math.Clamp(attempt, 1, 5);
        var delayMs = 250 * Math.Pow(2, cappedAttempt - 1);
        return TimeSpan.FromMilliseconds(delayMs);
    }

    // Builds the response format schema for behavior summary outputs.
    private static OpenAiResponseFormat BuildBehaviourSummaryFormat(OpenAiSchemaTemplates schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);

        var schema = new OpenAiSchema(
            "object",
            schemas.BehaviourSummaryDescription,
            new Dictionary<string, OpenAiSchema>
            {
                ["behaviourChange"] = OpenAiSchema.String(schemas.BehaviourChangeDescription),
                ["scope"] = OpenAiSchema.String(schemas.ScopeDescription),
                ["reviewerFocus"] = OpenAiSchema.String(schemas.ReviewerFocusDescription)
            },
            ["behaviourChange", "scope", "reviewerFocus"],
            null,
            false);

        var jsonSchema = new OpenAiJsonSchema(
            schemas.BehaviourSummaryName,
            schema,
            true);

        return new OpenAiResponseFormat("json_schema", jsonSchema);
    }

    // Builds the response format schema for reviewer question outputs.
    private static OpenAiResponseFormat BuildReviewerQuestionsFormat(OpenAiSchemaTemplates schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);

        var schema = new OpenAiSchema(
            "object",
            schemas.ReviewerQuestionsDescription,
            new Dictionary<string, OpenAiSchema>
            {
                ["questions"] = OpenAiSchema.StringArray(
                    schemas.ReviewerQuestionsDescriptionText,
                    schemas.ReviewerQuestionItemDescription)
            },
            ["questions"],
            null,
            false);

        var jsonSchema = new OpenAiJsonSchema(
            schemas.ReviewerQuestionsName,
            schema,
            true);

        return new OpenAiResponseFormat("json_schema", jsonSchema);
    }

    // Builds the response format schema for review summary outputs.
    private static OpenAiResponseFormat BuildReviewSummaryFormat(OpenAiSchemaTemplates schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);

        var schema = new OpenAiSchema(
            "object",
            schemas.ReviewSummaryDescription,
            new Dictionary<string, OpenAiSchema>
            {
                ["overallSummary"] = OpenAiSchema.String(schemas.OverallSummaryDescription),
                ["beforeState"] = OpenAiSchema.String(schemas.BeforeStateDescription),
                ["afterState"] = OpenAiSchema.String(schemas.AfterStateDescription)
            },
            ["overallSummary", "beforeState", "afterState"],
            null,
            false);

        var jsonSchema = new OpenAiJsonSchema(
            schemas.ReviewSummaryName,
            schema,
            true);

        return new OpenAiResponseFormat("json_schema", jsonSchema);
    }

    // Builds the response format schema for combined review session outputs.
    private static OpenAiResponseFormat BuildReviewSessionFormat(OpenAiSchemaTemplates schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);

        var summarySchema = OpenAiSchema.Object(
            schemas.ReviewSessionSummaryDescription,
            new Dictionary<string, OpenAiSchema>
            {
                ["overallSummary"] = OpenAiSchema.String(schemas.OverallSummaryDescription),
                ["beforeState"] = OpenAiSchema.String(schemas.BeforeStateDescription),
                ["afterState"] = OpenAiSchema.String(schemas.AfterStateDescription)
            },
            ["overallSummary", "beforeState", "afterState"],
            false);

        var fileSchema = OpenAiSchema.Object(
            schemas.ReviewSessionFileDescription,
            new Dictionary<string, OpenAiSchema>
            {
                ["nodeId"] = OpenAiSchema.String(schemas.ReviewSessionFileNodeIdDescription),
                ["path"] = OpenAiSchema.String(schemas.ReviewSessionFilePathDescription),
                ["behaviourChange"] = OpenAiSchema.String(schemas.BehaviourChangeDescription),
                ["scope"] = OpenAiSchema.String(schemas.ScopeDescription),
                ["reviewerFocus"] = OpenAiSchema.String(schemas.ReviewerFocusDescription),
                ["questions"] = OpenAiSchema.StringArray(
                    schemas.ReviewerQuestionsDescriptionText,
                    schemas.ReviewerQuestionItemDescription)
            },
            ["nodeId", "path", "behaviourChange", "scope", "reviewerFocus", "questions"],
            false);

        var schema = OpenAiSchema.Object(
            schemas.ReviewSessionDescription,
            new Dictionary<string, OpenAiSchema>
            {
                ["summary"] = summarySchema,
                ["files"] = OpenAiSchema.Array(schemas.ReviewSessionFilesDescription, fileSchema)
            },
            ["summary", "files"],
            false);

        var jsonSchema = new OpenAiJsonSchema(
            schemas.ReviewSessionName,
            schema,
            true);

        return new OpenAiResponseFormat("json_schema", jsonSchema);
    }

    private sealed record OpenAiClientResult<T>(T? Payload, bool ShouldRetry)
        where T : class;

    private sealed record OpenAiChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiChatMessage> Messages,
        [property: JsonPropertyName("response_format")] OpenAiResponseFormat ResponseFormat,
        [property: JsonPropertyName("temperature"),
         JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Temperature);

    private sealed record OpenAiChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenAiResponseFormat(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("json_schema")] OpenAiJsonSchema JsonSchema);

    private sealed record OpenAiJsonSchema(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("schema")] OpenAiSchema Schema,
        [property: JsonPropertyName("strict")] bool Strict);

    private sealed record OpenAiSchema(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("properties")] Dictionary<string, OpenAiSchema>? Properties,
        [property: JsonPropertyName("required")] string[]? Required,
        [property: JsonPropertyName("items")] OpenAiSchema? Items,
        [property: JsonPropertyName("additionalProperties")] bool? AdditionalProperties)
    {
        /// <summary>
        /// Builds a simple string property schema node.
        /// </summary>
        /// <param name="description">The description for the string field.</param>
        /// <returns>A string schema descriptor.</returns>
        public static OpenAiSchema String(string description)
        {
            return new OpenAiSchema("string", description, null, null, null, null);
        }

        /// <summary>
        /// Builds a string array schema node.
        /// </summary>
        /// <param name="description">The description for the array field.</param>
        /// <param name="itemDescription">The description for each array item.</param>
        /// <returns>An array schema descriptor.</returns>
        public static OpenAiSchema StringArray(string description, string itemDescription)
        {
            var itemSchema = new OpenAiSchema("string", itemDescription, null, null, null, null);
            return new OpenAiSchema("array", description, null, null, itemSchema, null);
        }

        /// <summary>
        /// Builds an object schema node with required properties.
        /// </summary>
        /// <param name="description">The description for the object.</param>
        /// <param name="properties">The properties contained within the object.</param>
        /// <param name="required">The required property names.</param>
        /// <param name="additionalProperties">Whether extra properties are allowed.</param>
        /// <returns>An object schema descriptor.</returns>
        public static OpenAiSchema Object(
            string description,
            Dictionary<string, OpenAiSchema> properties,
            string[] required,
            bool? additionalProperties)
        {
            return new OpenAiSchema("object", description, properties, required, null, additionalProperties);
        }

        /// <summary>
        /// Builds an array schema node with a provided item schema.
        /// </summary>
        /// <param name="description">The description for the array.</param>
        /// <param name="items">The schema for each array item.</param>
        /// <returns>An array schema descriptor.</returns>
        public static OpenAiSchema Array(string description, OpenAiSchema items)
        {
            return new OpenAiSchema("array", description, null, null, items, null);
        }
    }

    private sealed record OpenAiChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<OpenAiChatCompletionChoice>? Choices);

    private sealed record OpenAiChatCompletionChoice(
        [property: JsonPropertyName("message")] OpenAiChatCompletionMessage? Message);

    private sealed record OpenAiChatCompletionMessage(
        [property: JsonPropertyName("content")] string Content);
}
