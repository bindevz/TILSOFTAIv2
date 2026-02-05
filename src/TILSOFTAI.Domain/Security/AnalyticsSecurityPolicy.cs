namespace TILSOFTAI.Domain.Security;

/// <summary>
/// PATCH 29.07: Role-aware security policy for analytics field access.
/// Maps roles to allowed security tags (PII, SENSITIVE, RESTRICTED).
/// </summary>
public static class AnalyticsSecurityPolicy
{
    /// <summary>
    /// Security tags that fields can be tagged with.
    /// </summary>
    public static class Tags
    {
        public const string PII = "PII";
        public const string Sensitive = "SENSITIVE";
        public const string Restricted = "RESTRICTED";
        public const string Internal = "INTERNAL";
        public const string Public = "PUBLIC";
    }

    /// <summary>
    /// Roles that grant access to specific security tags.
    /// </summary>
    public static class Roles
    {
        public const string AnalyticsRead = "analytics.read";
        public const string AnalyticsPiiAccess = "analytics.pii";
        public const string AnalyticsSensitiveAccess = "analytics.sensitive";
        public const string AnalyticsAdmin = "analytics.admin";
    }

    /// <summary>
    /// Checks if a role grants access to a security tag.
    /// </summary>
    public static bool CanAccessTag(IEnumerable<string> userRoles, string securityTag)
    {
        if (string.IsNullOrWhiteSpace(securityTag))
            return true;

        var roles = new HashSet<string>(userRoles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        // Admin has access to everything
        if (roles.Contains(Roles.AnalyticsAdmin))
            return true;

        return securityTag.ToUpperInvariant() switch
        {
            "PUBLIC" => true, // Everyone can access public
            "INTERNAL" => roles.Contains(Roles.AnalyticsRead), // Internal requires analytics.read
            "PII" => roles.Contains(Roles.AnalyticsPiiAccess),
            "SENSITIVE" => roles.Contains(Roles.AnalyticsSensitiveAccess),
            "RESTRICTED" => roles.Contains(Roles.AnalyticsAdmin), // Only admin can access restricted
            _ => false // Unknown tags are denied by default
        };
    }

    /// <summary>
    /// Gets the allowed security tags for a set of roles.
    /// </summary>
    public static IReadOnlyList<string> GetAllowedTags(IEnumerable<string> userRoles)
    {
        var roles = new HashSet<string>(userRoles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var allowed = new List<string> { Tags.Public };

        if (roles.Contains(Roles.AnalyticsRead))
            allowed.Add(Tags.Internal);

        if (roles.Contains(Roles.AnalyticsPiiAccess))
            allowed.Add(Tags.PII);

        if (roles.Contains(Roles.AnalyticsSensitiveAccess))
            allowed.Add(Tags.Sensitive);

        if (roles.Contains(Roles.AnalyticsAdmin))
        {
            allowed.Add(Tags.Restricted);
            allowed.Add(Tags.PII);
            allowed.Add(Tags.Sensitive);
        }

        return allowed.Distinct().ToList();
    }

    /// <summary>
    /// Builds JSON array of allowed tags for SQL consumption.
    /// </summary>
    public static string GetAllowedTagsJson(IEnumerable<string> userRoles)
    {
        var tags = GetAllowedTags(userRoles);
        return "[" + string.Join(",", tags.Select(t => $"\"{t}\"")) + "]";
    }
}
