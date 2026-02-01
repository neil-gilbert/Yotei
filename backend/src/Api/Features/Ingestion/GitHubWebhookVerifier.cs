using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace Yotei.Api.Features.Ingestion;

/// <summary>
/// Validates GitHub webhook signatures using the shared secret.
/// </summary>
internal static class GitHubWebhookVerifier
{
    private const string SignaturePrefix = "sha256=";

    /// <summary>
    /// Validates the payload signature against the webhook secret.
    /// </summary>
    /// <param name="secret">The webhook secret, or empty when validation is disabled.</param>
    /// <param name="signatureHeader">The signature header value.</param>
    /// <param name="payload">The raw request payload.</param>
    /// <returns>True when the signature matches or validation is disabled.</returns>
    public static bool IsSignatureValid(string? secret, StringValues signatureHeader, string payload)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return true;
        }

        if (StringValues.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        var signature = signatureHeader.ToString();
        if (!signature.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expected = ComputeSignature(secret, payload);
        var provided = signature[SignaturePrefix.Length..];
        return FixedTimeEquals(expected, provided);
    }

    // Computes the HMAC SHA256 signature as a hex string.
    private static string ComputeSignature(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Compares two strings using a constant-time comparison.
    private static bool FixedTimeEquals(string expected, string provided)
    {
        if (expected.Length != provided.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided));
    }
}
