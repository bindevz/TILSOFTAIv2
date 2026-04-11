using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Capabilities;

public static class CapabilityAccessPolicy
{
    public const string AccessDeniedCode = "CAPABILITY_ACCESS_DENIED";

    public static CapabilityAccessDecision Evaluate(
        CapabilityDescriptor capability,
        TilsoftExecutionContext runtimeContext)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(runtimeContext);

        var allowedTenants = capability.AllowedTenants
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (allowedTenants.Length > 0
            && !allowedTenants.Contains(runtimeContext.TenantId, StringComparer.OrdinalIgnoreCase))
        {
            return CapabilityAccessDecision.Denied(
                AccessDeniedCode,
                new
                {
                    capabilityKey = capability.CapabilityKey,
                    tenantId = runtimeContext.TenantId,
                    allowedTenants
                });
        }

        var requiredRoles = capability.RequiredRoles
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (requiredRoles.Length == 0)
        {
            return CapabilityAccessDecision.Permit();
        }

        var callerRoles = runtimeContext.Roles ?? Array.Empty<string>();
        var hasRequiredRole = requiredRoles.Any(required =>
            callerRoles.Contains(required, StringComparer.OrdinalIgnoreCase));

        return hasRequiredRole
            ? CapabilityAccessDecision.Permit()
            : CapabilityAccessDecision.Denied(
                AccessDeniedCode,
                new
                {
                    capabilityKey = capability.CapabilityKey,
                    requiredRoles,
                    callerRoles
                });
    }
}

public sealed class CapabilityAccessDecision
{
    private CapabilityAccessDecision(bool allowed, string? code, object? detail)
    {
        Allowed = allowed;
        Code = code;
        Detail = detail;
    }

    public bool Allowed { get; }
    public string? Code { get; }
    public object? Detail { get; }

    public static CapabilityAccessDecision Permit() => new(true, null, null);

    public static CapabilityAccessDecision Denied(string code, object detail) => new(false, code, detail);
}
