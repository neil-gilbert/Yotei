using Yotei.Api.Models;

namespace Yotei.Api.Infrastructure;

public interface IExplanationGenerator
{
    Task<ChangeNodeExplanation> GenerateAsync(ChangeNode node, CancellationToken cancellationToken);
}

public sealed class StubExplanationGenerator : IExplanationGenerator
{
    public Task<ChangeNodeExplanation> GenerateAsync(ChangeNode node, CancellationToken cancellationToken)
    {
        var prompt = $"Explain the change node: {node.Label}.";
        var response = $"Auto explanation for {node.Label}.";

        var explanation = new ChangeNodeExplanation
        {
            ChangeNodeId = node.Id,
            Model = "stub",
            Prompt = prompt,
            Response = response
        };

        return Task.FromResult(explanation);
    }
}
