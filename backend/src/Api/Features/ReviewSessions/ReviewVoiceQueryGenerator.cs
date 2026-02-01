using Yotei.Api.Models;

namespace Yotei.Api.Features.ReviewSessions;

/// <summary>
/// Generates deterministic voice query responses scoped to a review node.
/// </summary>
public sealed class ReviewVoiceQueryGenerator
{
    /// <summary>
    /// Generates a response to the provided question using node context.
    /// </summary>
    /// <param name="node">The review node being discussed.</param>
    /// <param name="question">The spoken question or transcript text.</param>
    /// <returns>A concise answer string scoped to the node.</returns>
    public string GenerateAnswer(ReviewNode node, string question)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

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
}
