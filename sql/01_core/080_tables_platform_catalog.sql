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

IF OBJECT_ID('dbo.PlatformCatalogChangeRequest', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformCatalogChangeRequest
    (
        ChangeId NVARCHAR(64) NOT NULL,
        TenantId NVARCHAR(50) NOT NULL,
        RecordType NVARCHAR(50) NOT NULL,
        Operation NVARCHAR(50) NOT NULL,
        RecordKey NVARCHAR(200) NOT NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL,
        Status NVARCHAR(50) NOT NULL CONSTRAINT DF_PlatformCatalogChangeRequest_Status DEFAULT ('Pending'),
        Owner NVARCHAR(200) NOT NULL,
        ChangeNote NVARCHAR(1000) NOT NULL,
        VersionTag NVARCHAR(100) NULL,
        ExpectedVersionTag NVARCHAR(100) NULL,
        IdempotencyKey NVARCHAR(200) NULL,
        RollbackOfChangeId NVARCHAR(64) NULL,
        PayloadHash NVARCHAR(128) NOT NULL,
        RiskLevel NVARCHAR(50) NOT NULL CONSTRAINT DF_PlatformCatalogChangeRequest_RiskLevel DEFAULT ('standard'),
        EnvironmentName NVARCHAR(100) NOT NULL CONSTRAINT DF_PlatformCatalogChangeRequest_EnvironmentName DEFAULT ('development'),
        BreakGlass BIT NOT NULL CONSTRAINT DF_PlatformCatalogChangeRequest_BreakGlass DEFAULT (0),
        BreakGlassJustification NVARCHAR(1000) NULL,
        RequestedByUserId NVARCHAR(200) NOT NULL,
        RequestedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_PlatformCatalogChangeRequest_RequestedAtUtc DEFAULT (SYSUTCDATETIME()),
        ReviewedByUserId NVARCHAR(200) NULL,
        ReviewedAtUtc DATETIME2(7) NULL,
        AppliedByUserId NVARCHAR(200) NULL,
        AppliedAtUtc DATETIME2(7) NULL,
        CONSTRAINT PK_PlatformCatalogChangeRequest PRIMARY KEY (TenantId, ChangeId)
    );
END;
GO

IF COL_LENGTH('dbo.PlatformCatalogChangeRequest', 'ExpectedVersionTag') IS NULL
    ALTER TABLE dbo.PlatformCatalogChangeRequest ADD ExpectedVersionTag NVARCHAR(100) NULL;
GO

IF COL_LENGTH('dbo.PlatformCatalogChangeRequest', 'IdempotencyKey') IS NULL
    ALTER TABLE dbo.PlatformCatalogChangeRequest ADD IdempotencyKey NVARCHAR(200) NULL;
GO

IF COL_LENGTH('dbo.PlatformCatalogChangeRequest', 'RollbackOfChangeId') IS NULL
    ALTER TABLE dbo.PlatformCatalogChangeRequest ADD RollbackOfChangeId NVARCHAR(64) NULL;
GO

IF COL_LENGTH('dbo.PlatformCatalogChangeRequest', 'PayloadHash') IS NULL
    ALTER TABLE dbo.PlatformCatalogChangeRequest ADD PayloadHash NVARCHAR(128) NOT NULL CONSTRAINT DF_PlatformCatalogChangeRequest_PayloadHash DEFAULT ('');
GO

IF COL_LENGTH('dbo.PlatformCatalogChangeRequest', 'RiskLevel') IS NULL
    ALTER TABLE dbo.PlatformCatalogChangeRequest ADD RiskLevel NVARCHAR(50) NOT NULL CONSTRAINT DF_PlatformCatalogChangeRequest_RiskLevel DEFAULT ('standard');
GO

IF COL_LENGTH('dbo.PlatformCatalogChangeRequest', 'EnvironmentName') IS NULL
    ALTER TABLE dbo.PlatformCatalogChangeRequest ADD EnvironmentName NVARCHAR(100) NOT NULL CONSTRAINT DF_PlatformCatalogChangeRequest_EnvironmentName DEFAULT ('development');
GO

IF COL_LENGTH('dbo.PlatformCatalogChangeRequest', 'BreakGlass') IS NULL
    ALTER TABLE dbo.PlatformCatalogChangeRequest ADD BreakGlass BIT NOT NULL CONSTRAINT DF_PlatformCatalogChangeRequest_BreakGlass DEFAULT (0);
GO

IF COL_LENGTH('dbo.PlatformCatalogChangeRequest', 'BreakGlassJustification') IS NULL
    ALTER TABLE dbo.PlatformCatalogChangeRequest ADD BreakGlassJustification NVARCHAR(1000) NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_PlatformCatalogChangeRequest_PendingDuplicate'
      AND object_id = OBJECT_ID('dbo.PlatformCatalogChangeRequest')
)
BEGIN
    CREATE INDEX IX_PlatformCatalogChangeRequest_PendingDuplicate
        ON dbo.PlatformCatalogChangeRequest (TenantId, Status, RecordType, Operation, RecordKey, PayloadHash, IdempotencyKey);
END;
GO

IF OBJECT_ID('dbo.PlatformCatalogCertificationEvidence', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformCatalogCertificationEvidence
    (
        EvidenceId NVARCHAR(64) NOT NULL CONSTRAINT PK_PlatformCatalogCertificationEvidence PRIMARY KEY,
        EnvironmentName NVARCHAR(100) NOT NULL,
        EvidenceKind NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        Summary NVARCHAR(1000) NOT NULL,
        EvidenceUri NVARCHAR(1000) NULL,
        RelatedChangeId NVARCHAR(64) NULL,
        RelatedIncidentId NVARCHAR(100) NULL,
        OperatorUserId NVARCHAR(200) NOT NULL,
        ApprovedByUserId NVARCHAR(200) NULL,
        CorrelationId NVARCHAR(100) NULL,
        CapturedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_PlatformCatalogCertificationEvidence_CapturedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_PlatformCatalogCertificationEvidence_EnvironmentKindStatus'
      AND object_id = OBJECT_ID('dbo.PlatformCatalogCertificationEvidence')
)
BEGIN
    CREATE INDEX IX_PlatformCatalogCertificationEvidence_EnvironmentKindStatus
        ON dbo.PlatformCatalogCertificationEvidence (EnvironmentName, EvidenceKind, Status, CapturedAtUtc DESC);
END;
GO

IF OBJECT_ID('dbo.CK_PlatformCatalogCertificationEvidence_Status', 'C') IS NULL
BEGIN
    ALTER TABLE dbo.PlatformCatalogCertificationEvidence
        ADD CONSTRAINT CK_PlatformCatalogCertificationEvidence_Status
            CHECK (Status IN ('accepted', 'pending', 'rejected'));
END;
GO

IF OBJECT_ID('dbo.CK_PlatformCatalogCertificationEvidence_NotEmpty', 'C') IS NULL
BEGIN
    ALTER TABLE dbo.PlatformCatalogCertificationEvidence
        ADD CONSTRAINT CK_PlatformCatalogCertificationEvidence_NotEmpty
            CHECK (
                LEN(LTRIM(RTRIM(EnvironmentName))) > 0
                AND LEN(LTRIM(RTRIM(EvidenceKind))) > 0
                AND LEN(LTRIM(RTRIM(Summary))) > 0
                AND LEN(LTRIM(RTRIM(OperatorUserId))) > 0
            );
END;
GO
