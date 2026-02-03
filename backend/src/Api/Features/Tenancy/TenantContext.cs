using Yotei.Api.Models;

namespace Yotei.Api.Features.Tenancy;

/// <summary>
/// Holds tenant details resolved for the current request.
/// </summary>
public sealed class TenantContext
{
    /// <summary>
    /// Gets the resolved tenant identifier.
    /// </summary>
    public Guid TenantId { get; private set; } = Guid.Empty;

    /// <summary>
    /// Gets the resolved tenant token.
    /// </summary>
    public string TenantToken { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the resolved tenant name.
    /// </summary>
    public string TenantName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the resolved tenant slug.
    /// </summary>
    public string TenantSlug { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the tenant has been resolved.
    /// </summary>
    public bool IsResolved { get; private set; }

    /// <summary>
    /// Sets the resolved tenant data for the request.
    /// </summary>
    /// <param name="tenant">The tenant to apply to the context.</param>
    public void SetTenant(Tenant tenant)
    {
        if (tenant is null)
        {
            throw new ArgumentNullException(nameof(tenant));
        }

        TenantId = tenant.Id;
        TenantToken = tenant.Token;
        TenantName = tenant.Name;
        TenantSlug = tenant.Slug;
        IsResolved = true;
    }
}
