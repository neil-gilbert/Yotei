namespace Yotei.Api.Features.Flow.Inference;

/// <summary>
/// Provides a no-op flow inference adapter.
/// </summary>
public sealed class NullFlowInferenceAdapter : IFlowInferenceAdapter
{
    /// <summary>
    /// Gets the adapter language identifier.
    /// </summary>
    public string Language => "none";

    /// <summary>
    /// Returns an empty flow inference result.
    /// </summary>
    /// <param name="request">The inference request payload.</param>
    /// <param name="cancellationToken">Cancellation token for the inference operation.</param>
    /// <returns>An empty flow inference result.</returns>
    public FlowInferenceResult Infer(FlowInferenceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return FlowInferenceResult.Empty;
    }
}
