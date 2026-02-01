namespace Yotei.Api.Features.Flow.Inference;

/// <summary>
/// Provides heuristic flow inference for C# code changes.
/// </summary>
public sealed class CSharpFlowInferenceAdapter : IFlowInferenceAdapter
{
    private static readonly string[] EntryPointPathHints =
    [
        "controller",
        "endpoint",
        "handler",
        "middleware",
        "grpc"
    ];

    private static readonly string[] EntryPointTokens =
    [
        "[httpget",
        "[httppost",
        "[httpput",
        "[httpdelete",
        "mapget(",
        "mappost(",
        "mapput(",
        "mapdelete(",
        "mapgroup(",
        "mapcontrollers("
    ];

    private static readonly IReadOnlyDictionary<string, string[]> SideEffectKeywords =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["db"] = ["dbcontext", "dbset", "sqlconnection", "fromsql", "executesql", "dapper"],
            ["network"] = ["httpclient", "httprequest", "restclient", "httpwebrequest", "grpcchannel"],
            ["filesystem"] = ["file.", "filestream", "directory.", "path."],
            ["messaging"] = ["servicebus", "kafka", "rabbitmq", "queue", "topic", "hangfire"],
            ["email"] = ["smtpclient", "sendgrid", "mailmessage"]
        };

    /// <summary>
    /// Gets the adapter language identifier.
    /// </summary>
    public string Language => "csharp";

    /// <summary>
    /// Infers entry points and side effects for a C# diff.
    /// </summary>
    /// <param name="request">The inference request payload.</param>
    /// <param name="cancellationToken">Cancellation token for the inference operation.</param>
    /// <returns>A flow inference result for the change.</returns>
    public FlowInferenceResult Infer(FlowInferenceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var diffText = request.DiffText ?? string.Empty;
        var normalized = diffText.ToLowerInvariant();
        var path = request.Path ?? string.Empty;
        var normalizedPath = path.ToLowerInvariant();

        var entryPoints = new List<FlowInferenceSignal>();
        if (IsEntryPoint(normalizedPath, normalized))
        {
            var evidence = BuildEvidence(path, CollectMatches(normalized, EntryPointTokens));
            entryPoints.Add(new FlowInferenceSignal(path, evidence));
        }

        var sideEffects = BuildSideEffects(path, normalized);

        return new FlowInferenceResult(entryPoints, sideEffects);
    }

    /// <summary>
    /// Determines whether the change appears to be an entry point.
    /// </summary>
    /// <param name="path">The normalized file path.</param>
    /// <param name="diffText">The normalized diff text.</param>
    /// <returns>True when the file appears to define an entry point.</returns>
    private static bool IsEntryPoint(string path, string diffText)
    {
        var hasPathHint = EntryPointPathHints.Any(hint => path.Contains(hint, StringComparison.OrdinalIgnoreCase));
        var hasToken = EntryPointTokens.Any(token => diffText.Contains(token, StringComparison.OrdinalIgnoreCase));

        return hasPathHint || hasToken;
    }

    /// <summary>
    /// Builds side-effect inference signals for a C# diff.
    /// </summary>
    /// <param name="path">The file path for evidence.</param>
    /// <param name="diffText">The normalized diff text.</param>
    /// <returns>The inferred side-effect signals.</returns>
    private static List<FlowInferenceSignal> BuildSideEffects(string path, string diffText)
    {
        var results = new List<FlowInferenceSignal>();

        foreach (var (label, keywords) in SideEffectKeywords)
        {
            var matches = CollectMatches(diffText, keywords);
            if (matches.Count == 0)
            {
                continue;
            }

            var evidence = BuildEvidence(path, matches);
            results.Add(new FlowInferenceSignal(label, evidence));
        }

        return results;
    }

    /// <summary>
    /// Collects keyword matches for inference evidence.
    /// </summary>
    /// <param name="text">The normalized diff text.</param>
    /// <param name="keywords">Keywords to scan for.</param>
    /// <returns>Matched keywords.</returns>
    private static List<string> CollectMatches(string text, IEnumerable<string> keywords)
    {
        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(keyword);
            }
        }

        return matches.OrderBy(match => match, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Builds evidence tags for flow inference signals.
    /// </summary>
    /// <param name="path">The file path to include.</param>
    /// <param name="matches">Keyword matches to include.</param>
    /// <returns>The evidence tags.</returns>
    private static List<string> BuildEvidence(string path, IReadOnlyList<string> matches)
    {
        var evidence = new List<string> { $"path:{path}" };
        evidence.AddRange(matches.Select(match => $"keyword:{match}"));
        return evidence;
    }
}
