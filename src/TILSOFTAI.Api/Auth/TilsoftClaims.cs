namespace TILSOFTAI.Api.Auth;

public static class TilsoftClaims
{
    // JWT standard claim names
    public const string TenantId = "tid";     // Tenant identifier (common in multi-tenant JWTs)
    public const string UserId = "sub";       // Subject (RFC 7519 standard for user ID)
    public const string Roles = "roles";
    public const string Role = "role";
    public const string Language = "lang";
}
