CREATE OR ALTER PROCEDURE dbo.app_platform_capabilitycatalog_list
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CapabilityKey,
        Domain,
        AdapterType,
        Operation,
        TargetSystemId,
        ExecutionMode,
        RequiredRolesJson,
        AllowedTenantsJson,
        IntegrationBindingJson,
        ArgumentContractJson,
        VersionTag,
        UpdatedBy,
        UpdatedAtUtc
    FROM dbo.PlatformCapabilityCatalog
    WHERE IsEnabled = 1
    ORDER BY Domain, CapabilityKey;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_externalconnectioncatalog_list
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ConnectionName,
        BaseUrl,
        AuthScheme,
        AuthTokenSecret,
        ApiKeyHeader,
        ApiKeySecret,
        TimeoutSeconds,
        RetryCount,
        RetryDelayMs,
        HeadersJson,
        HeaderSecretsJson,
        VersionTag,
        UpdatedBy,
        UpdatedAtUtc
    FROM dbo.PlatformExternalConnectionCatalog
    WHERE IsEnabled = 1
    ORDER BY ConnectionName;
END;
GO
