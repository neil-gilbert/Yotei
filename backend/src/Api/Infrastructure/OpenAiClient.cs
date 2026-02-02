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

    private static readonly OpenAiResponseFormat BehaviourSummaryFormat = BuildBehaviourSummaryFormat();
    private static readonly OpenAiResponseFormat ReviewerQuestionsFormat = BuildReviewerQuestionsFormat();
    private static readonly OpenAiResponseFormat ReviewSummaryFormat = BuildReviewSummaryFormat();
    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiClient> _logger;

    /// <summary>
    /// Creates an OpenAI client wrapper with injected HTTP and configuration dependencies.
    /// </summary>
    /// <param name="httpClient">The HttpClient used to call OpenAI.</param>
    /// <param name="settings">The OpenAI configuration settings.</param>
    /// <param name="logger">Logger for diagnostics and retries.</param>
    public OpenAiClient(
        HttpClient httpClient,
        IOptions<OpenAiSettings> settings,
        ILogger<OpenAiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
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

    // Builds the request payload for the OpenAI chat completions endpoint for summaries.
    private OpenAiChatCompletionRequest BuildSummaryRequest(OpenAiBehaviourSummaryPrompt prompt)
    {
        var systemPrompt = "You are a code review assistant. Return JSON only using the provided schema. " +
                           "Keep responses concise and specific to the diff.";
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
            BehaviourSummaryFormat,
            _settings.Temperature);
    }

    // Builds the request payload for the OpenAI chat completions endpoint for questions.
    private OpenAiChatCompletionRequest BuildQuestionsRequest(OpenAiReviewerQuestionsPrompt prompt)
    {
        var systemPrompt = "You are a code review assistant. Return JSON only using the provided schema. " +
                           "Generate 4-6 targeted reviewer questions that are not generic checklist items.";
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
            ReviewerQuestionsFormat,
            _settings.Temperature);
    }

    // Builds the request payload for the OpenAI chat completions endpoint for review summaries.
    private OpenAiChatCompletionRequest BuildReviewSummaryRequest(OpenAiReviewSummaryPrompt prompt)
    {
        var systemPrompt = "You are a senior developer explaining a change to another developer. " +
                           "Return JSON only using the provided schema. " +
                           "Write conversational sentences (no bullets, no file lists). " +
                           "Explain how it worked before, what changed now, and why it matters. " +
                           "Mention test updates if any are present.";
        var userPrompt = BuildReviewSummaryPromptBody(prompt);

        return new OpenAiChatCompletionRequest(
            _settings.Model,
            [
                new OpenAiChatMessage("system", systemPrompt),
                new OpenAiChatMessage("user", userPrompt)
            ],
            ReviewSummaryFormat,
            _settings.Temperature);
    }

    // Builds the prompt body including file path, diff, and risk evidence.
    private static string BuildPromptBody(
        string filePath,
        string changeType,
        string diff,
        IReadOnlyList<string> riskTags,
        IReadOnlyList<string> evidence)
    {
        var riskText = riskTags.Count > 0
            ? string.Join(", ", riskTags)
            : "none";
        var evidenceText = evidence.Count > 0
            ? string.Join(", ", evidence)
            : "none";

        return $"""
File path: {filePath}
Change type: {changeType}
Risk tags: {riskText}
Evidence: {evidenceText}
Diff:
{diff}
""";
    }

    // Builds the prompt body for review summary generation.
    private static string BuildReviewSummaryPromptBody(OpenAiReviewSummaryPrompt prompt)
    {
        var entryPointsText = prompt.EntryPoints.Count > 0
            ? string.Join(", ", prompt.EntryPoints)
            : "none";
        var sideEffectsText = prompt.SideEffects.Count > 0
            ? string.Join(", ", prompt.SideEffects)
            : "none";
        var riskText = prompt.RiskTags.Count > 0
            ? string.Join(", ", prompt.RiskTags)
            : "none";
        var topPathsText = prompt.TopPaths.Count > 0
            ? string.Join(", ", prompt.TopPaths)
            : "none";

        var builder = new StringBuilder();
        builder.AppendLine($"Repository: {prompt.Repository}");
        builder.AppendLine($"PR: #{prompt.PrNumber}");
        builder.AppendLine($"Title: {prompt.Title ?? "Untitled"}");
        builder.AppendLine(
            $"Files changed: {prompt.ChangedFilesCount} (new {prompt.NewFilesCount}, modified {prompt.ModifiedFilesCount}, deleted {prompt.DeletedFilesCount})");
        builder.AppendLine($"Entry points: {entryPointsText}");
        builder.AppendLine($"Side effects: {sideEffectsText}");
        builder.AppendLine($"Risk tags: {riskText}");
        builder.AppendLine($"Top paths: {topPathsText}");

        var testFiles = prompt.Files
            .Select(file => file.Path)
            .Where(path => path.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("spec", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        builder.AppendLine(testFiles.Count > 0
            ? $"Test files: {string.Join(", ", testFiles)}"
            : "Test files: none");
        builder.AppendLine("Diff excerpts:");

        foreach (var file in prompt.Files)
        {
            builder.AppendLine($"File: {file.Path} ({file.ChangeType}, +{file.AddedLines}/-{file.DeletedLines})");
            builder.AppendLine("Diff:");
            builder.AppendLine(file.Diff);
            builder.AppendLine("---");
        }

        return builder.ToString();
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
    private static OpenAiResponseFormat BuildBehaviourSummaryFormat()
    {
        var schema = new OpenAiSchema(
            "object",
            "Behavior summary for a single file.",
            new Dictionary<string, OpenAiSchema>
            {
                ["behaviourChange"] = OpenAiSchema.String("Concise summary of what behavior changed."),
                ["scope"] = OpenAiSchema.String("Scope of impact and confidence."),
                ["reviewerFocus"] = OpenAiSchema.String("Reviewer focus guidance.")
            },
            ["behaviourChange", "scope", "reviewerFocus"],
            null,
            false);

        var jsonSchema = new OpenAiJsonSchema(
            "review_behaviour_summary",
            schema,
            true);

        return new OpenAiResponseFormat("json_schema", jsonSchema);
    }

    // Builds the response format schema for reviewer question outputs.
    private static OpenAiResponseFormat BuildReviewerQuestionsFormat()
    {
        var schema = new OpenAiSchema(
            "object",
            "Reviewer questions for a single file.",
            new Dictionary<string, OpenAiSchema>
            {
                ["questions"] = OpenAiSchema.StringArray("Targeted reviewer questions.")
            },
            ["questions"],
            null,
            false);

        var jsonSchema = new OpenAiJsonSchema(
            "review_reviewer_questions",
            schema,
            true);

        return new OpenAiResponseFormat("json_schema", jsonSchema);
    }

    // Builds the response format schema for review summary outputs.
    private static OpenAiResponseFormat BuildReviewSummaryFormat()
    {
        var schema = new OpenAiSchema(
            "object",
            "Overall summary for a review session.",
            new Dictionary<string, OpenAiSchema>
            {
                ["overallSummary"] = OpenAiSchema.String("High-level summary of the change."),
                ["beforeState"] = OpenAiSchema.String("What the code or behavior was before the change."),
                ["afterState"] = OpenAiSchema.String("What the change introduces or does now.")
            },
            ["overallSummary", "beforeState", "afterState"],
            null,
            false);

        var jsonSchema = new OpenAiJsonSchema(
            "review_summary_overview",
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
        /// <returns>An array schema descriptor.</returns>
        public static OpenAiSchema StringArray(string description)
        {
            var itemSchema = new OpenAiSchema("string", "Question text.", null, null, null, null);
            return new OpenAiSchema("array", description, null, null, itemSchema, null);
        }
    }

    private sealed record OpenAiChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<OpenAiChatCompletionChoice>? Choices);

    private sealed record OpenAiChatCompletionChoice(
        [property: JsonPropertyName("message")] OpenAiChatCompletionMessage? Message);

    private sealed record OpenAiChatCompletionMessage(
        [property: JsonPropertyName("content")] string Content);
}
