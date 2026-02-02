using System.Security.Cryptography;

namespace Yotei.Api.Features.Tenancy;

/// <summary>
/// Generates tenant access tokens for API authentication.
/// </summary>
public static class TenantTokenGenerator
{
    private const int TokenByteLength = 32;

    /// <summary>
    /// Creates a new URL-safe token string.
    /// </summary>
    /// <returns>The generated token.</returns>
    public static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        var encoded = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"yotei_{encoded}";
    }
}
