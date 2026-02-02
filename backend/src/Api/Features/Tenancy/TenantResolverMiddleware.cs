using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Yotei.Api.Data;

namespace Yotei.Api.Features.Tenancy;

/// <summary>
/// Resolves tenant context from request headers or query parameters.
/// </summary>
public sealed class TenantResolverMiddleware : IMiddleware
{
    private static readonly PathString WebhookPath = new("/ingest/github/webhook");
    private static readonly PathString InstallPath = new("/github/install");
    private readonly TenantContext _tenantContext;
    private readonly TenantProvisioningService _provisioningService;
    private readonly TenancySettings _settings;
    private readonly YoteiDbContext _dbContext;
    private readonly ILogger<TenantResolverMiddleware> _logger;

    /// <summary>
    /// Initializes the middleware with required services.
    /// </summary>
    /// <param name="tenantContext">Scoped tenant context to populate.</param>
    /// <param name="provisioningService">Service for tenant lookups.</param>
    /// <param name="options">Tenancy configuration options.</param>
    /// <param name="dbContext">Database context for fallback checks.</param>
    /// <param name="logger">Logger used for diagnostics.</param>
    public TenantResolverMiddleware(
        TenantContext tenantContext,
        TenantProvisioningService provisioningService,
        IOptions<TenancySettings> options,
        YoteiDbContext dbContext,
        ILogger<TenantResolverMiddleware> logger)
    {
        _tenantContext = tenantContext;
        _provisioningService = provisioningService;
        _settings = options.Value;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the tenant for the current request or short-circuits when missing.
    /// </summary>
    /// <param name="context">HTTP context for the request.</param>
    /// <param name="next">The next middleware in the pipeline.</param>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (ShouldBypassTenant(context.Request))
        {
            await next(context);
            return;
        }

        var token = ResolveToken(context.Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            if (await TryResolveSingleTenantAsync(context.RequestAborted))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var tenant = await _provisioningService.GetTenantByTokenAsync(token, context.RequestAborted);
        if (tenant is null)
        {
            _logger.LogWarning("Tenant token not recognized.");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        _tenantContext.SetTenant(tenant);
        await next(context);
    }

    // Determines whether the request should bypass tenant enforcement.
    private static bool ShouldBypassTenant(HttpRequest request)
    {
        if (HttpMethods.IsOptions(request.Method))
        {
            return true;
        }

        if (request.Path == "/" || request.Path.StartsWithSegments("/health"))
        {
            return true;
        }

        return request.Path.StartsWithSegments(WebhookPath) ||
            request.Path.StartsWithSegments(InstallPath);
    }

    // Attempts to resolve the tenant token from headers or query parameters.
    private static string? ResolveToken(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Tenant-Token", out var headerValues))
        {
            var headerToken = headerValues.ToString();
            if (!string.IsNullOrWhiteSpace(headerToken))
            {
                return headerToken;
            }
        }

        var queryToken = request.Query["tenant"].ToString();
        return string.IsNullOrWhiteSpace(queryToken) ? null : queryToken;
    }

    // Uses the only tenant in the database when fallback is enabled.
    private async Task<bool> TryResolveSingleTenantAsync(CancellationToken cancellationToken)
    {
        if (!_settings.AllowSingleTenantFallback)
        {
            return false;
        }

        var tenant = await _dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (tenant is null)
        {
            return false;
        }

        var multipleTenants = await _dbContext.Tenants.AsNoTracking().Skip(1).AnyAsync(cancellationToken);
        if (multipleTenants)
        {
            return false;
        }

        _tenantContext.SetTenant(tenant);
        return true;
    }
}
