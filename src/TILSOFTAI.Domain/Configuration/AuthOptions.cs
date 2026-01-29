namespace TILSOFTAI.Domain.Configuration;

public sealed class AuthOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string JwksUrl { get; set; } = string.Empty;
    public string RoleClaimName { get; set; } = "roles";
    
    // Claim names for identity resolution
    public string TenantClaimName { get; set; } = "tenant_id";
    public string UserIdClaimName { get; set; } = "user_id";
    
    // Header fallback configuration (default: disabled for security)
    public bool AllowHeaderFallback { get; set; } = false;
    
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
