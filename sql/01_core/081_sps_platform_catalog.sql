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

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogchange_create
    @ChangeId NVARCHAR(64),
    @TenantId NVARCHAR(50),
    @RecordType NVARCHAR(50),
    @Operation NVARCHAR(50),
    @RecordKey NVARCHAR(200),
    @PayloadJson NVARCHAR(MAX),
    @Owner NVARCHAR(200),
    @ChangeNote NVARCHAR(1000),
    @VersionTag NVARCHAR(100) = NULL,
    @RequestedByUserId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.PlatformCatalogChangeRequest
    (
        ChangeId, TenantId, RecordType, Operation, RecordKey, PayloadJson,
        Status, Owner, ChangeNote, VersionTag, RequestedByUserId
    )
    VALUES
    (
        @ChangeId, @TenantId, @RecordType, @Operation, @RecordKey, @PayloadJson,
        'Pending', @Owner, @ChangeNote, @VersionTag, @RequestedByUserId
    );

    EXEC dbo.app_platform_catalogchange_get @TenantId = @TenantId, @ChangeId = @ChangeId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogchange_get
    @TenantId NVARCHAR(50),
    @ChangeId NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM dbo.PlatformCatalogChangeRequest
    WHERE TenantId = @TenantId
      AND ChangeId = @ChangeId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogchange_list
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM dbo.PlatformCatalogChangeRequest
    WHERE TenantId = @TenantId
    ORDER BY RequestedAtUtc DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogchange_approve
    @TenantId NVARCHAR(50),
    @ChangeId NVARCHAR(64),
    @ReviewedByUserId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PlatformCatalogChangeRequest
       SET Status = 'Approved',
           ReviewedByUserId = @ReviewedByUserId,
           ReviewedAtUtc = SYSUTCDATETIME()
     WHERE TenantId = @TenantId
       AND ChangeId = @ChangeId
       AND Status = 'Pending';

    EXEC dbo.app_platform_catalogchange_get @TenantId = @TenantId, @ChangeId = @ChangeId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogchange_reject
    @TenantId NVARCHAR(50),
    @ChangeId NVARCHAR(64),
    @ReviewedByUserId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PlatformCatalogChangeRequest
       SET Status = 'Rejected',
           ReviewedByUserId = @ReviewedByUserId,
           ReviewedAtUtc = SYSUTCDATETIME()
     WHERE TenantId = @TenantId
       AND ChangeId = @ChangeId
       AND Status = 'Pending';

    EXEC dbo.app_platform_catalogchange_get @TenantId = @TenantId, @ChangeId = @ChangeId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogchange_mark_applied
    @TenantId NVARCHAR(50),
    @ChangeId NVARCHAR(64),
    @AppliedByUserId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PlatformCatalogChangeRequest
       SET Status = 'Applied',
           AppliedByUserId = @AppliedByUserId,
           AppliedAtUtc = SYSUTCDATETIME()
     WHERE TenantId = @TenantId
       AND ChangeId = @ChangeId
       AND Status = 'Approved';

    EXEC dbo.app_platform_catalogchange_get @TenantId = @TenantId, @ChangeId = @ChangeId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_capabilitycatalog_upsert
    @CapabilityKey NVARCHAR(200),
    @Domain NVARCHAR(100),
    @AdapterType NVARCHAR(100),
    @Operation NVARCHAR(100),
    @TargetSystemId NVARCHAR(200),
    @ExecutionMode NVARCHAR(50),
    @RequiredRolesJson NVARCHAR(MAX) = NULL,
    @AllowedTenantsJson NVARCHAR(MAX) = NULL,
    @IntegrationBindingJson NVARCHAR(MAX),
    @ArgumentContractJson NVARCHAR(MAX) = NULL,
    @VersionTag NVARCHAR(100) = NULL,
    @UpdatedBy NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.PlatformCapabilityCatalog AS target
    USING (SELECT @CapabilityKey AS CapabilityKey) AS source
       ON target.CapabilityKey = source.CapabilityKey
    WHEN MATCHED THEN
        UPDATE SET
            Domain = @Domain,
            AdapterType = @AdapterType,
            Operation = @Operation,
            TargetSystemId = @TargetSystemId,
            ExecutionMode = @ExecutionMode,
            RequiredRolesJson = @RequiredRolesJson,
            AllowedTenantsJson = @AllowedTenantsJson,
            IntegrationBindingJson = @IntegrationBindingJson,
            ArgumentContractJson = @ArgumentContractJson,
            IsEnabled = 1,
            VersionTag = @VersionTag,
            UpdatedBy = @UpdatedBy,
            UpdatedAtUtc = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT
        (
            CapabilityKey, Domain, AdapterType, Operation, TargetSystemId, ExecutionMode,
            RequiredRolesJson, AllowedTenantsJson, IntegrationBindingJson, ArgumentContractJson,
            IsEnabled, VersionTag, UpdatedBy
        )
        VALUES
        (
            @CapabilityKey, @Domain, @AdapterType, @Operation, @TargetSystemId, @ExecutionMode,
            @RequiredRolesJson, @AllowedTenantsJson, @IntegrationBindingJson, @ArgumentContractJson,
            1, @VersionTag, @UpdatedBy
        );
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_capabilitycatalog_disable
    @CapabilityKey NVARCHAR(200),
    @VersionTag NVARCHAR(100) = NULL,
    @UpdatedBy NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PlatformCapabilityCatalog
       SET IsEnabled = 0,
           VersionTag = @VersionTag,
           UpdatedBy = @UpdatedBy,
           UpdatedAtUtc = SYSUTCDATETIME()
     WHERE CapabilityKey = @CapabilityKey;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_externalconnectioncatalog_upsert
    @ConnectionName NVARCHAR(200),
    @BaseUrl NVARCHAR(1000),
    @AuthScheme NVARCHAR(100) = NULL,
    @AuthTokenSecret NVARCHAR(500) = NULL,
    @ApiKeyHeader NVARCHAR(200) = NULL,
    @ApiKeySecret NVARCHAR(500) = NULL,
    @TimeoutSeconds INT,
    @RetryCount INT,
    @RetryDelayMs INT,
    @HeadersJson NVARCHAR(MAX) = NULL,
    @HeaderSecretsJson NVARCHAR(MAX) = NULL,
    @VersionTag NVARCHAR(100) = NULL,
    @UpdatedBy NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.PlatformExternalConnectionCatalog AS target
    USING (SELECT @ConnectionName AS ConnectionName) AS source
       ON target.ConnectionName = source.ConnectionName
    WHEN MATCHED THEN
        UPDATE SET
            BaseUrl = @BaseUrl,
            AuthScheme = @AuthScheme,
            AuthTokenSecret = @AuthTokenSecret,
            ApiKeyHeader = @ApiKeyHeader,
            ApiKeySecret = @ApiKeySecret,
            TimeoutSeconds = @TimeoutSeconds,
            RetryCount = @RetryCount,
            RetryDelayMs = @RetryDelayMs,
            HeadersJson = @HeadersJson,
            HeaderSecretsJson = @HeaderSecretsJson,
            IsEnabled = 1,
            VersionTag = @VersionTag,
            UpdatedBy = @UpdatedBy,
            UpdatedAtUtc = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT
        (
            ConnectionName, BaseUrl, AuthScheme, AuthTokenSecret, ApiKeyHeader, ApiKeySecret,
            TimeoutSeconds, RetryCount, RetryDelayMs, HeadersJson, HeaderSecretsJson,
            IsEnabled, VersionTag, UpdatedBy
        )
        VALUES
        (
            @ConnectionName, @BaseUrl, @AuthScheme, @AuthTokenSecret, @ApiKeyHeader, @ApiKeySecret,
            @TimeoutSeconds, @RetryCount, @RetryDelayMs, @HeadersJson, @HeaderSecretsJson,
            1, @VersionTag, @UpdatedBy
        );
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_externalconnectioncatalog_disable
    @ConnectionName NVARCHAR(200),
    @VersionTag NVARCHAR(100) = NULL,
    @UpdatedBy NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PlatformExternalConnectionCatalog
       SET IsEnabled = 0,
           VersionTag = @VersionTag,
           UpdatedBy = @UpdatedBy,
           UpdatedAtUtc = SYSUTCDATETIME()
     WHERE ConnectionName = @ConnectionName;
END;
GO
