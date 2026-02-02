using System.Text;
using System.Text.RegularExpressions;

namespace Yotei.Api.Features.Tenancy;

/// <summary>
/// Generates URL-safe slugs for tenant identifiers.
/// </summary>
public static class TenantSlugGenerator
{
    private static readonly Regex NonSlugChars = new("[^a-z0-9-]", RegexOptions.Compiled);

    /// <summary>
    /// Produces a normalized slug from a display name.
    /// </summary>
    /// <param name="name">The tenant display name.</param>
    /// <returns>The normalized slug value.</returns>
    public static string CreateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "tenant";
        }

        var builder = new StringBuilder(name.Trim().ToLowerInvariant());
        builder.Replace(' ', '-');
        var normalized = NonSlugChars.Replace(builder.ToString(), string.Empty);
        normalized = Regex.Replace(normalized, "-{2,}", "-").Trim('-');

        return string.IsNullOrWhiteSpace(normalized) ? "tenant" : normalized;
    }
}
