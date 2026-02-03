namespace Yotei.Api.Infrastructure;

/// <summary>
/// Default hard limits for combined OpenAI review session prompts.
/// </summary>
public sealed record OpenAiReviewLimits
{
    /// <summary>
    /// Maximum number of file nodes to include in a single prompt.
    /// </summary>
    public int MaxFiles { get; init; } = 20;

    /// <summary>
    /// Maximum diff characters per file included in the prompt.
    /// </summary>
    public int MaxDiffCharactersPerFile { get; init; } = 2000;

    /// <summary>
    /// Maximum reviewer questions per file in the LLM response.
    /// </summary>
    public int MaxQuestionsPerFile { get; init; } = 6;

    /// <summary>
    /// Maximum risk tags per file included in the prompt.
    /// </summary>
    public int MaxRiskTagsPerFile { get; init; } = 6;

    /// <summary>
    /// Maximum evidence items per file included in the prompt.
    /// </summary>
    public int MaxEvidenceItemsPerFile { get; init; } = 6;
}

/// <summary>
/// Override values for tenant-specific OpenAI review prompt limits.
/// </summary>
public sealed record OpenAiReviewLimitOverride
{
    public int? MaxFiles { get; init; }
    public int? MaxDiffCharactersPerFile { get; init; }
    public int? MaxQuestionsPerFile { get; init; }
    public int? MaxRiskTagsPerFile { get; init; }
    public int? MaxEvidenceItemsPerFile { get; init; }
}

/// <summary>
/// Configurable limits for OpenAI review sessions, with optional tenant overrides.
/// </summary>
public sealed record OpenAiReviewLimitsOptions
{
    /// <summary>
    /// Baseline limits applied when no tenant override is found.
    /// </summary>
    public OpenAiReviewLimits Default { get; init; } = new();

    /// <summary>
    /// Tenant-specific overrides keyed by tenant slug, id, or name.
    /// </summary>
    public Dictionary<string, OpenAiReviewLimitOverride> Tenants { get; init; } = new();
}
