using System.Text;
using Yotei.Api.Models;

namespace Yotei.Api.Infrastructure;

/// <summary>
/// Generates explanations for review nodes.
/// </summary>
public interface IReviewExplanationGenerator
{
    /// <summary>
    /// Generates an explanation for the provided review node.
    /// </summary>
    /// <param name="node">The review node to explain.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A populated explanation entity.</returns>
    Task<ReviewNodeExplanation> GenerateAsync(ReviewNode node, CancellationToken cancellationToken);
}

/// <summary>
/// Produces deterministic fallback explanations using captured evidence.
/// </summary>
public sealed class StubReviewExplanationGenerator : IReviewExplanationGenerator
{
    /// <summary>
    /// Generates a fallback explanation that is never empty.
    /// </summary>
    /// <param name="node">The review node to explain.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A populated explanation entity.</returns>
    public Task<ReviewNodeExplanation> GenerateAsync(ReviewNode node, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append($"Change focus: {node.Label}. ");
        builder.Append($"Type: {node.ChangeType}. ");

        var riskText = node.RiskTags.Count > 0
            ? string.Join(", ", node.RiskTags)
            : "none detected";
        builder.Append($"Risk tags: {riskText}. ");

        var evidenceText = node.Evidence.Count > 0
            ? string.Join(", ", node.Evidence.Take(3))
            : "no evidence captured";
        builder.Append($"Evidence: {evidenceText}.");

        var explanation = new ReviewNodeExplanation
        {
            ReviewNodeId = node.Id,
            Response = builder.ToString(),
            Source = "heuristic"
        };

        return Task.FromResult(explanation);
    }
}
