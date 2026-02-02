using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Yotei.Api.Features.Ingestion;

/// <summary>
/// Creates GitHub App JWTs for app-scoped API calls.
/// </summary>
public interface IGitHubAppJwtFactory
{
    /// <summary>
    /// Builds a signed JWT for the configured GitHub App.
    /// </summary>
    /// <returns>The JWT value or null when configuration is invalid.</returns>
    string? CreateJwt();
}

/// <summary>
/// Default implementation for GitHub App JWT creation.
/// </summary>
public sealed class GitHubAppJwtFactory : IGitHubAppJwtFactory
{
    private static readonly TimeSpan AppTokenLifetime = TimeSpan.FromMinutes(9);
    private readonly GitHubSettings _settings;
    private readonly ILogger<GitHubAppJwtFactory> _logger;
    private RSA? _appPrivateKey;

    /// <summary>
    /// Initializes the factory with app settings and logging.
    /// </summary>
    /// <param name="settings">GitHub settings used to read the app configuration.</param>
    /// <param name="logger">Logger used for diagnostics.</param>
    public GitHubAppJwtFactory(
        IOptions<GitHubSettings> settings,
        ILogger<GitHubAppJwtFactory> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string? CreateJwt()
    {
        if (_settings.App.AppId <= 0)
        {
            _logger.LogWarning("GitHub App ID is missing.");
            return null;
        }

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
}
