using System.Net.Http.Headers;
using System.Net.Http.Json;
using Yotei.Api.Features.Ingestion;

namespace Yotei.Api.Features.Tenancy;

/// <summary>
/// Fetches GitHub App installation metadata via the GitHub REST API.
/// </summary>
public interface IGitHubInstallationClient
{
    /// <summary>
    /// Retrieves installation details for the specified installation id.
    /// </summary>
    /// <param name="installationId">The GitHub installation identifier.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The installation details or null when unavailable.</returns>
    Task<GitHubInstallationDetails?> GetInstallationAsync(long installationId, CancellationToken cancellationToken);
}

/// <summary>
/// Default GitHub installation client using app-scoped JWTs.
/// </summary>
public sealed class GitHubInstallationClient : IGitHubInstallationClient
{
    private readonly HttpClient _httpClient;
    private readonly IGitHubAppJwtFactory _jwtFactory;
    private readonly ILogger<GitHubInstallationClient> _logger;

    /// <summary>
    /// Initializes the GitHub installation client with dependencies.
    /// </summary>
    /// <param name="httpClient">HTTP client configured for GitHub.</param>
    /// <param name="jwtFactory">Factory used to build GitHub App JWTs.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public GitHubInstallationClient(
        HttpClient httpClient,
        IGitHubAppJwtFactory jwtFactory,
        ILogger<GitHubInstallationClient> logger)
    {
        _httpClient = httpClient;
        _jwtFactory = jwtFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GitHubInstallationDetails?> GetInstallationAsync(
        long installationId,
        CancellationToken cancellationToken)
    {
        if (installationId <= 0)
        {
            return null;
        }

        var jwt = _jwtFactory.CreateJwt();
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/app/installations/{installationId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub installation fetch failed with status {StatusCode}.", response.StatusCode);
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<GitHubInstallationPayload>(
            cancellationToken: cancellationToken);

        if (payload?.Account is null || string.IsNullOrWhiteSpace(payload.Account.Login))
        {
            _logger.LogWarning("GitHub installation payload missing account details.");
            return null;
        }

        return new GitHubInstallationDetails(
            installationId,
            payload.Account.Login,
            payload.Account.Type ?? "User");
    }

    private sealed record GitHubInstallationPayload(GitHubInstallationAccount? Account);

    private sealed record GitHubInstallationAccount(string Login, string? Type);
}

/// <summary>
/// Describes the GitHub App installation metadata used for provisioning.
/// </summary>
public sealed record GitHubInstallationDetails(long InstallationId, string AccountLogin, string AccountType);
