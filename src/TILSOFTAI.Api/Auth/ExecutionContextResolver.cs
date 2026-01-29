using System.Security.Claims;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Auth;

internal static class ExecutionContextResolver
{
    public static string? GetClaim(ClaimsPrincipal? principal, string claimType)
    {
        return principal?.FindFirst(claimType)?.Value;
    }

    public static string? ResolveIdentity(string? claimValue, string? headerValue, bool allowHeaderFallback, string fieldName)
    {
        var claim = NormalizeIdentity(claimValue);
        var header = NormalizeIdentity(headerValue);

        if (!string.IsNullOrWhiteSpace(claim))
        {
            if (!string.IsNullOrWhiteSpace(header)
                && !string.Equals(claim, header, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"{fieldName} does not match token.");
            }

            return claim;
        }

        if (allowHeaderFallback && !string.IsNullOrWhiteSpace(header))
        {
            return header;
        }

        return null;
    }

    public static string ResolveLanguage(
        string? claimLang,
        string? headerLang,
        string? acceptLanguage,
        LocalizationOptions options)
    {
        var supported = options.SupportedLanguages.Length > 0
            ? new HashSet<string>(options.SupportedLanguages, StringComparer.OrdinalIgnoreCase)
            : null;

        var candidate = NormalizeLanguage(claimLang);
        if (IsSupported(candidate, supported))
        {
            return candidate!;
        }

        candidate = NormalizeLanguage(headerLang);
        if (IsSupported(candidate, supported))
        {
            return candidate!;
        }

        candidate = NormalizeLanguage(acceptLanguage);
        if (IsSupported(candidate, supported))
        {
            return candidate!;
        }

        candidate = NormalizeLanguage(options.DefaultLanguage);
        if (IsSupported(candidate, supported))
        {
            return candidate!;
        }

        return "en";
    }

    private static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var trimmed = language.Trim();
        var commaIndex = trimmed.IndexOf(',');
        if (commaIndex >= 0)
        {
            trimmed = trimmed[..commaIndex];
        }

        var dashIndex = trimmed.IndexOf('-');
        if (dashIndex >= 0)
        {
            trimmed = trimmed[..dashIndex];
        }

        var underscoreIndex = trimmed.IndexOf('_');
        if (underscoreIndex >= 0)
        {
            trimmed = trimmed[..underscoreIndex];
        }

        return trimmed;
    }

    private static bool IsSupported(string? language, HashSet<string>? supported)
    {
        return !string.IsNullOrWhiteSpace(language) && (supported is null || supported.Contains(language.Trim()));
    }

    private static string? NormalizeIdentity(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
