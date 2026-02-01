namespace Yotei.Api.Features.Flow.Inference;

/// <summary>
/// Defines a flow inference adapter for a specific language.
/// </summary>
public interface IFlowInferenceAdapter
{
    /// <summary>
    /// Gets the language identifier for the adapter.
    /// </summary>
    string Language { get; }

    /// <summary>
    /// Infers flow signals for the provided change input.
    /// </summary>
    /// <param name="request">The request describing the file change to analyze.</param>
    /// <param name="cancellationToken">Cancellation token for the inference operation.</param>
    /// <returns>Flow inference signals for entry points and side effects.</returns>
    FlowInferenceResult Infer(FlowInferenceRequest request, CancellationToken cancellationToken);
}
