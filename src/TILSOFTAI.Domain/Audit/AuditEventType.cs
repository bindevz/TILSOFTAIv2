namespace TILSOFTAI.Domain.Audit;

/// <summary>
/// Categorizes security-relevant audit events.
/// </summary>
public enum AuditEventType
{
    // Authentication events
    Authentication_Success = 100,
    Authentication_Failure = 101,
    Authentication_Logout = 102,

    // Authorization events
    Authorization_Granted = 200,
    Authorization_Denied = 201,

    // Data access events
    DataAccess_Read = 300,
    DataAccess_Write = 301,
    DataAccess_Delete = 302,

    // Administrative events
    Admin_ConfigChange = 400,
    Admin_UserManagement = 401,

    // Security events
    Security_InputValidationFailure = 500,
    Security_RateLimitExceeded = 501,
    Security_SuspiciousActivity = 502,
    Security_PromptInjectionDetected = 503
}

/// <summary>
/// Outcome of an audited operation.
/// </summary>
public enum AuditOutcome
{
    Success = 0,
    Failure = 1,
    Denied = 2
}

/// <summary>
/// Data operation type for data access events.
/// </summary>
public enum DataOperation
{
    Read = 0,
    Create = 1,
    Update = 2,
    Delete = 3
}
