using Microsoft.Extensions.Options;

namespace Yotei.Api.Features.Flow.Inference;

/// <summary>
/// Resolves flow inference adapters based on repository language configuration.
/// </summary>
public sealed class FlowInferenceAdapterRegistry
{
    private readonly IReadOnlyDictionary<string, IFlowInferenceAdapter> adapters;
    private readonly FlowInferenceOptions options;
    private readonly IFlowInferenceAdapter fallbackAdapter = new NullFlowInferenceAdapter();

    /// <summary>
    /// Initializes the registry with available adapters and configuration.
    /// </summary>
    /// <param name="adapters">The available inference adapters.</param>
    /// <param name="options">The configured adapter options.</param>
    public FlowInferenceAdapterRegistry(
        IEnumerable<IFlowInferenceAdapter> adapters,
        IOptions<FlowInferenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        ArgumentNullException.ThrowIfNull(options);

        this.options = options.Value ?? new FlowInferenceOptions();
        this.adapters = adapters
            .Where(adapter => !string.IsNullOrWhiteSpace(adapter.Language))
            .GroupBy(adapter => adapter.Language, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves an adapter for the provided repo and file path.
    /// </summary>
    /// <param name="repoKey">The repo key in "owner/name" form.</param>
    /// <param name="filePath">The path of the file being analyzed.</param>
    /// <returns>The adapter matching the resolved language or a fallback adapter.</returns>
    public IFlowInferenceAdapter ResolveAdapter(string? repoKey, string? filePath)
    {
        var language = ResolveLanguage(repoKey, filePath);
        if (string.IsNullOrWhiteSpace(language))
        {
            return fallbackAdapter;
        }

        return adapters.TryGetValue(language, out var adapter)
            ? adapter
            : fallbackAdapter;
    }

    /// <summary>
    /// Resolves the language configured for a repo or inferred from the file path.
    /// </summary>
    /// <param name="repoKey">The repo key in "owner/name" form.</param>
    /// <param name="filePath">The path of the file being analyzed.</param>
    /// <returns>The resolved language identifier, if any.</returns>
    public string? ResolveLanguage(string? repoKey, string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(repoKey))
        {
            var map = options.RepoLanguages ?? new Dictionary<string, string>();
            if (map.TryGetValue(repoKey.Trim(), out var configuredLanguage) &&
                !string.IsNullOrWhiteSpace(configuredLanguage))
            {
                return configuredLanguage.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(options.DefaultLanguage))
        {
            return options.DefaultLanguage;
        }

        return InferLanguageFromPath(filePath);
    }

    /// <summary>
    /// Infers a language based on file extension.
    /// </summary>
    /// <param name="filePath">The path of the file being analyzed.</param>
    /// <returns>The inferred language identifier, if any.</returns>
    private static string? InferLanguageFromPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var extension = Path.GetExtension(filePath);
        return extension.ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".js" or ".jsx" or ".ts" or ".tsx" => "javascript",
            _ => null
        };
    }
}
