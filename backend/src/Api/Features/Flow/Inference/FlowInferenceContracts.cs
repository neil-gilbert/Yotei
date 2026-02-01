namespace Yotei.Api.Features.Flow.Inference;

/// <summary>
/// Represents a flow inference request for a single file change.
/// </summary>
/// <example>
/// <code>
/// {
///   "path": "src/api/PaymentsController.cs",
///   "diffText": "@@ ...",
///   "language": "csharp"
/// }
/// </code>
/// </example>
public sealed record FlowInferenceRequest(
    string Path,
    string DiffText,
    string? Language);

/// <summary>
/// Represents a signal derived from flow inference.
/// </summary>
/// <example>
/// <code>
/// {
///   "label": "network",
///   "evidence": ["keyword:HttpClient", "path:src/api/PaymentsController.cs"]
/// }
/// </code>
/// </example>
public sealed record FlowInferenceSignal(
    string Label,
    IReadOnlyList<string> Evidence);

/// <summary>
/// Represents the result of flow inference for a file change.
/// </summary>
/// <example>
/// <code>
/// {
///   "entryPoints": [],
///   "sideEffects": []
/// }
/// </code>
/// </example>
public sealed record FlowInferenceResult(
    IReadOnlyList<FlowInferenceSignal> EntryPoints,
    IReadOnlyList<FlowInferenceSignal> SideEffects)
{
    /// <summary>
    /// Represents an empty flow inference result.
    /// </summary>
    public static FlowInferenceResult Empty { get; } = new([], []);
}
