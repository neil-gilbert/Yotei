namespace Yotei.Api.Features.Flow.Inference;

/// <summary>
/// Provides heuristic flow inference for JavaScript and TypeScript changes.
/// </summary>
public sealed class JavaScriptFlowInferenceAdapter : IFlowInferenceAdapter
{
    private static readonly string[] EntryPointTokens =
    [
        "app.get(",
        "app.post(",
        "app.put(",
        "app.delete(",
        "router.get(",
        "router.post(",
        "router.put(",
        "router.delete(",
        "express(",
        "fastify(",
        "nextjs",
        "middleware"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> SideEffectKeywords =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["db"] = ["prisma", "mongoose", "sequelize", "knex", "pg", "typeorm"],
            ["network"] = ["fetch(", "axios", "superagent", "got(", "http.request"],
            ["filesystem"] = ["fs.", "readfile", "writefile", "createwritestream"],
            ["messaging"] = ["bull", "kafka", "rabbitmq", "sqs", "pubsub"],
            ["email"] = ["nodemailer", "sendgrid"]
        };

    /// <summary>
    /// Gets the adapter language identifier.
    /// </summary>
    public string Language => "javascript";

    /// <summary>
    /// Infers entry points and side effects for a JavaScript diff.
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

        var entryPoints = new List<FlowInferenceSignal>();
        if (EntryPointTokens.Any(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            var matches = CollectMatches(normalized, EntryPointTokens);
            entryPoints.Add(new FlowInferenceSignal(path, BuildEvidence(path, matches)));
        }

        var sideEffects = BuildSideEffects(path, normalized);

        return new FlowInferenceResult(entryPoints, sideEffects);
    }

    /// <summary>
    /// Builds side-effect inference signals for a JavaScript diff.
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

            results.Add(new FlowInferenceSignal(label, BuildEvidence(path, matches)));
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
