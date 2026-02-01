using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    Task ApplyAuthenticationAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}

/// <summary>
/// Issues GitHub API tokens from PAT or GitHub App configuration.
/// </summary>
public sealed class GitHubAccessTokenProvider : IGitHubAccessTokenProvider
{
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan AppTokenLifetime = TimeSpan.FromMinutes(9);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubSettings _settings;
    private readonly ILogger<GitHubAccessTokenProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _cachedTokenExpiresAt = DateTimeOffset.MinValue;
    private RSA? _appPrivateKey;

    /// <summary>
    /// Initializes the provider with configuration and HTTP dependencies.
    /// </summary>
    /// <param name="httpClientFactory">Factory for GitHub API clients.</param>
    /// <param name="settings">GitHub configuration settings.</param>
    /// <param name="logger">Logger used for diagnostics.</param>
    public GitHubAccessTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<GitHubSettings> settings,
        ILogger<GitHubAccessTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ApplyAuthenticationAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // Resolves the access token to use for GitHub API requests.
    private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (IsGitHubAppConfigured())
        {
            return await GetGitHubAppTokenAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(_settings.Token))
        {
            return _settings.Token;
        }

        return null;
    }

    // Determines whether GitHub App credentials are present.
    private bool IsGitHubAppConfigured()
    {
        return _settings.App.AppId > 0 &&
            _settings.App.InstallationId > 0 &&
            !string.IsNullOrWhiteSpace(_settings.App.PrivateKey);
    }

    // Retrieves or refreshes the GitHub App installation token.
    private async Task<string?> GetGitHubAppTokenAsync(CancellationToken cancellationToken)
    {
        if (IsCachedTokenValid())
        {
            return _cachedToken;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (IsCachedTokenValid())
            {
                return _cachedToken;
            }

            var tokenResponse = await RequestInstallationTokenAsync(cancellationToken);
            if (tokenResponse is null)
            {
                return null;
            }

            _cachedToken = tokenResponse.Token;
            _cachedTokenExpiresAt = tokenResponse.ExpiresAt;
            return _cachedToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // Checks whether the cached token is still valid.
    private bool IsCachedTokenValid()
    {
        return !string.IsNullOrWhiteSpace(_cachedToken) &&
            _cachedTokenExpiresAt > DateTimeOffset.UtcNow.Add(TokenRefreshSkew);
    }

    // Requests a GitHub App installation token from the GitHub API.
    private async Task<InstallationTokenResponse?> RequestInstallationTokenAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(GitHubHttpClientConfigurator.ClientName);
        var jwt = CreateAppJwt();
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/app/installations/{_settings.App.InstallationId}/access_tokens")
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

    // Creates a signed JWT used to authenticate as the GitHub App.
    private string? CreateAppJwt()
    {
        try
        {
            var rsa = GetOrCreatePrivateKey();
            if (rsa is null)
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            var payload = new Dictionary<string, object>
            {
                ["iat"] = now.AddSeconds(-30).ToUnixTimeSeconds(),
                ["exp"] = now.Add(AppTokenLifetime).ToUnixTimeSeconds(),
                ["iss"] = _settings.App.AppId
            };

            var header = new Dictionary<string, object>
            {
                ["alg"] = "RS256",
                ["typ"] = "JWT"
            };

            var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header);
            var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
            var headerEncoded = Base64UrlEncode(headerBytes);
            var payloadEncoded = Base64UrlEncode(payloadBytes);
            var unsignedToken = $"{headerEncoded}.{payloadEncoded}";

            var signature = rsa.SignData(
                Encoding.UTF8.GetBytes(unsignedToken),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            var signatureEncoded = Base64UrlEncode(signature);
            return $"{unsignedToken}.{signatureEncoded}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create GitHub App JWT.");
            return null;
        }
    }

    // Lazily parses and caches the GitHub App private key.
    private RSA? GetOrCreatePrivateKey()
    {
        if (_appPrivateKey is not null)
        {
            return _appPrivateKey;
        }

        if (string.IsNullOrWhiteSpace(_settings.App.PrivateKey))
        {
            _logger.LogWarning("GitHub App private key is missing.");
            return null;
        }

        try
        {
            var normalized = NormalizePrivateKey(_settings.App.PrivateKey);
            var rsa = RSA.Create();
            rsa.ImportFromPem(normalized);
            _appPrivateKey = rsa;
            return _appPrivateKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse GitHub App private key.");
            return null;
        }
    }

    // Normalizes raw or base64-encoded PEM key material into PEM format.
    private static string NormalizePrivateKey(string value)
    {
        var trimmed = value.Trim();
        var normalized = trimmed.Replace("\\n", "\n");
        if (normalized.Contains("BEGIN", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var bytes = Convert.FromBase64String(trimmed);
        return Encoding.UTF8.GetString(bytes);
    }

    // Encodes bytes into Base64Url for JWT signing.
    private static string Base64UrlEncode(byte[] value)
    {
        var encoded = Convert.ToBase64String(value);
        return encoded
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed record InstallationTokenResponse(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);
}
