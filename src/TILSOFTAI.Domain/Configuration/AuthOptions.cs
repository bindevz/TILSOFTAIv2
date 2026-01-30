namespace TILSOFTAI.Domain.Configuration;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string JwksUrl { get; set; } = string.Empty;
    public string RoleClaimName { get; set; } = "roles";
    
    // Claim names for identity resolution (defaults follow JWT standards)
    // 'tid' = tenant identifier (common in multi-tenant JWTs)
    // 'sub' = subject (standard JWT claim for user ID, RFC 7519)
    public string TenantClaimName { get; set; } = "tid";
    public string UserIdClaimName { get; set; } = "sub";
    
    // Header fallback configuration (default: disabled for security)
    // Only honored in Development or when gateway_trusted claim is present.
    public bool AllowHeaderFallback { get; set; } = false;

    // Claim name used to indicate trusted gateway mode (allows header fallback).
    public string TrustedGatewayClaimName { get; set; } = "gateway_trusted";
    
    // Deprecated: Use AllowHeaderFallback instead
    [Obsolete("Use AllowHeaderFallback instead. This property maps to AllowHeaderFallback for backward compatibility.")]
    public bool AllowHeaderTenantFallback
    {
        get => AllowHeaderFallback;
        set => AllowHeaderFallback = value;
    }
    
    // Configurable header names for fallback (when AllowHeaderFallback=true)
    public string[] HeaderTenantKeys { get; set; } = new[] { "X-Tenant-Id" };
    public string[] HeaderUserKeys { get; set; } = new[] { "X-User-Id" };
    public string[] HeaderRolesKeys { get; set; } = new[] { "X-Roles" };
    
    public int ClockSkewSeconds { get; set; } = 300;
}
