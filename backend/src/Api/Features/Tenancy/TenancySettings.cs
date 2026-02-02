namespace Yotei.Api.Features.Tenancy;

/// <summary>
/// Configuration settings that control tenant resolution behavior.
/// </summary>
public record TenancySettings
{
    /// <summary>
    /// When true, requests without a token will use the only tenant in the database.
    /// </summary>
    public bool AllowSingleTenantFallback { get; init; }
}
