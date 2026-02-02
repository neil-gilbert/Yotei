using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Models;

namespace Yotei.Api.Features.Tenancy;

/// <summary>
/// Handles tenant lookup and provisioning for GitHub App installs.
/// </summary>
public sealed class TenantProvisioningService
{
    private readonly YoteiDbContext _dbContext;
    private readonly IGitHubInstallationClient _installationClient;
    private readonly ILogger<TenantProvisioningService> _logger;

    /// <summary>
    /// Initializes the provisioning service with database and GitHub dependencies.
    /// </summary>
    /// <param name="dbContext">Database context for tenant persistence.</param>
    /// <param name="installationClient">Client used to fetch GitHub installation metadata.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public TenantProvisioningService(
        YoteiDbContext dbContext,
        IGitHubInstallationClient installationClient,
        ILogger<TenantProvisioningService> logger)
    {
        _dbContext = dbContext;
        _installationClient = installationClient;
        _logger = logger;
    }

    /// <summary>
    /// Finds a tenant by its access token.
    /// </summary>
    /// <param name="token">The tenant access token.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The tenant or null when not found.</returns>
    public Task<Tenant?> GetTenantByTokenAsync(string token, CancellationToken cancellationToken)
    {
        return _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(tenant => tenant.Token == token, cancellationToken);
    }

    /// <summary>
    /// Finds a tenant linked to the given GitHub installation id.
    /// </summary>
    /// <param name="installationId">The GitHub installation identifier.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The tenant or null when not found.</returns>
    public async Task<Tenant?> GetTenantByInstallationAsync(long installationId, CancellationToken cancellationToken)
    {
        var installation = await _dbContext.GitHubInstallations
            .AsNoTracking()
            .Include(item => item.Tenant)
            .FirstOrDefaultAsync(item => item.InstallationId == installationId, cancellationToken);

        return installation?.Tenant;
    }

    /// <summary>
    /// Ensures a tenant exists for a GitHub installation, creating one if needed.
    /// </summary>
    /// <param name="installationId">The GitHub installation identifier.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The resolved tenant or null when provisioning fails.</returns>
    public async Task<Tenant?> EnsureTenantForInstallationAsync(long installationId, CancellationToken cancellationToken)
    {
        if (installationId <= 0)
        {
            return null;
        }

        var existing = await _dbContext.GitHubInstallations
            .Include(item => item.Tenant)
            .FirstOrDefaultAsync(item => item.InstallationId == installationId, cancellationToken);

        if (existing?.Tenant is not null)
        {
            await UpdateInstallationMetadataAsync(existing, cancellationToken);
            return existing.Tenant;
        }

        var installation = await _installationClient.GetInstallationAsync(installationId, cancellationToken);
        if (installation is null)
        {
            _logger.LogWarning("Unable to provision tenant for installation {InstallationId}.", installationId);
            return null;
        }

        var token = await GenerateUniqueTokenAsync(cancellationToken);
        var slug = await GenerateUniqueSlugAsync(installation.AccountLogin, cancellationToken);

        var tenant = new Tenant
        {
            Name = installation.AccountLogin,
            Slug = slug,
            Token = token
        };

        var newInstallation = new GitHubInstallation
        {
            Tenant = tenant,
            InstallationId = installation.InstallationId,
            AccountLogin = installation.AccountLogin,
            AccountType = installation.AccountType,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.GitHubInstallations.Add(newInstallation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return tenant;
    }

    // Updates stored installation metadata when a tenant already exists.
    private async Task UpdateInstallationMetadataAsync(
        GitHubInstallation installation,
        CancellationToken cancellationToken)
    {
        var details = await _installationClient.GetInstallationAsync(installation.InstallationId, cancellationToken);
        if (details is null)
        {
            return;
        }

        var updated = false;
        if (!string.Equals(installation.AccountLogin, details.AccountLogin, StringComparison.OrdinalIgnoreCase))
        {
            installation.AccountLogin = details.AccountLogin;
            updated = true;
        }

        if (!string.Equals(installation.AccountType, details.AccountType, StringComparison.OrdinalIgnoreCase))
        {
            installation.AccountType = details.AccountType;
            updated = true;
        }

        if (updated)
        {
            installation.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    // Generates a unique tenant token by checking for collisions.
    private async Task<string> GenerateUniqueTokenAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var token = TenantTokenGenerator.CreateToken();
            var exists = await _dbContext.Tenants.AnyAsync(tenant => tenant.Token == token, cancellationToken);
            if (!exists)
            {
                return token;
            }
        }

        return $"{TenantTokenGenerator.CreateToken()}_{Guid.NewGuid():N}";
    }

    // Generates a unique slug based on a display name.
    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken cancellationToken)
    {
        var baseSlug = TenantSlugGenerator.CreateSlug(name);
        var slug = baseSlug;
        var suffix = 1;

        while (await _dbContext.Tenants.AnyAsync(tenant => tenant.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
    }
}
