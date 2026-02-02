using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Yotei.Api.Features.Ingestion;

/// <summary>
/// Provides access tokens for GitHub API requests based on configuration.
/// </summary>
public interface IGitHubAccessTokenProvider
{
    /// <summary>
    /// Applies authentication headers to an outgoing GitHub API request.
    /// </summary>
    /// <param name="request">The request to authenticate.</param>
    /// <param name="installationId">The optional installation identifier used for GitHub App auth.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    Task ApplyAuthenticationAsync(
        HttpRequestMessage request,
        long? installationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Issues GitHub API tokens from PAT or GitHub App configuration.
/// </summary>
public sealed class GitHubAccessTokenProvider : IGitHubAccessTokenProvider
{
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromMinutes(1);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubSettings _settings;
    private readonly IGitHubAppJwtFactory _jwtFactory;
    private readonly ILogger<GitHubAccessTokenProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Dictionary<long, InstallationTokenCacheEntry> _tokenCache = [];

    /// <summary>
    /// Initializes the provider with configuration and HTTP dependencies.
    /// </summary>
    /// <param name="httpClientFactory">Factory for GitHub API clients.</param>
    /// <param name="settings">GitHub configuration settings.</param>
    /// <param name="logger">Logger used for diagnostics.</param>
    public GitHubAccessTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<GitHubSettings> settings,
        IGitHubAppJwtFactory jwtFactory,
        ILogger<GitHubAccessTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _jwtFactory = jwtFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ApplyAuthenticationAsync(
        HttpRequestMessage request,
        long? installationId,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(installationId, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // Resolves the access token to use for GitHub API requests.
    private async Task<string?> GetAccessTokenAsync(long? installationId, CancellationToken cancellationToken)
    {
        var resolvedInstallationId = ResolveInstallationId(installationId);
        if (IsGitHubAppConfigured() && resolvedInstallationId is > 0)
        {
            return await GetGitHubAppTokenAsync(resolvedInstallationId.Value, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(_settings.Token))
        {
            return _settings.Token;
        }

        return null;
    }

    // Resolves the installation id from request input or configuration.
    private long? ResolveInstallationId(long? installationId)
    {
        if (installationId is > 0)
        {
            return installationId;
        }

        return _settings.App.InstallationId > 0 ? _settings.App.InstallationId : null;
    }

    // Determines whether GitHub App credentials are present.
    private bool IsGitHubAppConfigured()
    {
        return _settings.App.AppId > 0 &&
            !string.IsNullOrWhiteSpace(_settings.App.PrivateKey);
    }

    // Retrieves or refreshes the GitHub App installation token.
    private async Task<string?> GetGitHubAppTokenAsync(long installationId, CancellationToken cancellationToken)
    {
        if (TryGetCachedToken(installationId, out var cachedToken))
        {
            return cachedToken;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCachedToken(installationId, out cachedToken))
            {
                return cachedToken;
            }

            var tokenResponse = await RequestInstallationTokenAsync(installationId, cancellationToken);
            if (tokenResponse is null)
            {
                return null;
            }

            _tokenCache[installationId] = new InstallationTokenCacheEntry(tokenResponse.Token, tokenResponse.ExpiresAt);
            return tokenResponse.Token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // Checks whether the cached token is still valid.
    private bool TryGetCachedToken(long installationId, out string? token)
    {
        token = null;
        if (!_tokenCache.TryGetValue(installationId, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow.Add(TokenRefreshSkew))
        {
            return false;
        }

        token = entry.Token;
        return true;
    }

    // Requests a GitHub App installation token from the GitHub API.
    private async Task<InstallationTokenResponse?> RequestInstallationTokenAsync(
        long installationId,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(GitHubHttpClientConfigurator.ClientName);
        var jwt = _jwtFactory.CreateJwt();
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/app/installations/{installationId}/access_tokens")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub App token request failed with status {StatusCode}.",
                response.StatusCode);
            return null;
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<InstallationTokenResponse>(
            cancellationToken: cancellationToken);

        return tokenResponse;
    }

    private sealed record InstallationTokenResponse(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);

    private sealed record InstallationTokenCacheEntry(string Token, DateTimeOffset ExpiresAt);
}
