IF OBJECT_ID('dbo.PlatformCapabilityCatalog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformCapabilityCatalog
    (
        CapabilityKey NVARCHAR(200) NOT NULL CONSTRAINT PK_PlatformCapabilityCatalog PRIMARY KEY,
        Domain NVARCHAR(100) NOT NULL,
        AdapterType NVARCHAR(100) NOT NULL,
        Operation NVARCHAR(100) NOT NULL,
        TargetSystemId NVARCHAR(200) NOT NULL,
        ExecutionMode NVARCHAR(50) NOT NULL,
        RequiredRolesJson NVARCHAR(MAX) NULL,
        AllowedTenantsJson NVARCHAR(MAX) NULL,
        IntegrationBindingJson NVARCHAR(MAX) NOT NULL,
        ArgumentContractJson NVARCHAR(MAX) NULL,
        IsEnabled BIT NOT NULL CONSTRAINT DF_PlatformCapabilityCatalog_IsEnabled DEFAULT (1),
        VersionTag NVARCHAR(100) NULL,
        UpdatedBy NVARCHAR(200) NOT NULL CONSTRAINT DF_PlatformCapabilityCatalog_UpdatedBy DEFAULT (SUSER_SNAME()),
        UpdatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_PlatformCapabilityCatalog_UpdatedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF OBJECT_ID('dbo.PlatformExternalConnectionCatalog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformExternalConnectionCatalog
    (
        ConnectionName NVARCHAR(200) NOT NULL CONSTRAINT PK_PlatformExternalConnectionCatalog PRIMARY KEY,
        BaseUrl NVARCHAR(1000) NOT NULL,
        AuthScheme NVARCHAR(100) NULL,
        AuthTokenSecret NVARCHAR(500) NULL,
        ApiKeyHeader NVARCHAR(200) NULL,
        ApiKeySecret NVARCHAR(500) NULL,
        TimeoutSeconds INT NOT NULL CONSTRAINT DF_PlatformExternalConnectionCatalog_Timeout DEFAULT (10),
        RetryCount INT NOT NULL CONSTRAINT DF_PlatformExternalConnectionCatalog_RetryCount DEFAULT (0),
        RetryDelayMs INT NOT NULL CONSTRAINT DF_PlatformExternalConnectionCatalog_RetryDelay DEFAULT (0),
        HeadersJson NVARCHAR(MAX) NULL,
        HeaderSecretsJson NVARCHAR(MAX) NULL,
        IsEnabled BIT NOT NULL CONSTRAINT DF_PlatformExternalConnectionCatalog_IsEnabled DEFAULT (1),
        VersionTag NVARCHAR(100) NULL,
        UpdatedBy NVARCHAR(200) NOT NULL CONSTRAINT DF_PlatformExternalConnectionCatalog_UpdatedBy DEFAULT (SUSER_SNAME()),
        UpdatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_PlatformExternalConnectionCatalog_UpdatedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END;
GO
