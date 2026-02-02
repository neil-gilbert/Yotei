using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Yotei.Api.Infrastructure;

namespace Yotei.Api.Features.Tenancy;

/// <summary>
/// Provides endpoints for tenant onboarding flows.
/// </summary>
public static class TenantOnboardingEndpoints
{
    /// <summary>
    /// Maps endpoints used by the GitHub App installation callback.
    /// </summary>
    /// <param name="app">The route builder used to register endpoints.</param>
    /// <returns>The same route builder instance for chaining.</returns>
    public static IEndpointRouteBuilder MapTenantOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/github/install", async (
            HttpRequest request,
            TenantProvisioningService provisioningService,
            IOptions<FrontendSettings> frontendOptions,
            CancellationToken cancellationToken) =>
        {
            var installationIdParam = request.Query["installation_id"].ToString();
            if (!long.TryParse(installationIdParam, out var installationId))
            {
                return Results.BadRequest(new { error = "installation_id is required" });
            }

            var tenant = await provisioningService.EnsureTenantForInstallationAsync(installationId, cancellationToken);
            if (tenant is null)
            {
                return Results.Problem(detail: "Unable to provision tenant for installation.");
            }

            var frontendBaseUrl = frontendOptions.Value.BaseUrl;
            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                return Results.Problem(detail: "Frontend base URL is not configured.");
            }

            var redirectUrl = QueryHelpers.AddQueryString(frontendBaseUrl, new Dictionary<string, string?>
            {
                ["tenant"] = tenant.Token,
                ["view"] = "setup"
            });

            return Results.Redirect(redirectUrl);
        });

        return app;
    }
}
