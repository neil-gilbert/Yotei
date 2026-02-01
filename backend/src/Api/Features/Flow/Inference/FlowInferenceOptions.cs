namespace Yotei.Api.Features.Flow.Inference;

/// <summary>
/// Configures flow inference adapter selection.
/// </summary>
public sealed class FlowInferenceOptions
{
    /// <summary>
    /// Gets the repo-specific language overrides keyed by "owner/name".
    /// </summary>
    public Dictionary<string, string> RepoLanguages { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the default language when no repo-specific mapping is provided.
    /// </summary>
    public string? DefaultLanguage { get; init; }
}
