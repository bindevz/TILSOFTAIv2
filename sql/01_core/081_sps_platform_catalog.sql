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

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogrecord_version
    @RecordType NVARCHAR(50),
    @RecordKey NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    IF @RecordType = 'capability'
    BEGIN
        SELECT
            CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.PlatformCapabilityCatalog WHERE CapabilityKey = @RecordKey AND IsEnabled = 1) THEN 1 ELSE 0 END AS BIT) AS RecordExists,
            ISNULL((SELECT TOP (1) VersionTag FROM dbo.PlatformCapabilityCatalog WHERE CapabilityKey = @RecordKey AND IsEnabled = 1), '') AS VersionTag;
        RETURN;
    END;

    IF @RecordType = 'external_connection'
    BEGIN
        SELECT
            CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.PlatformExternalConnectionCatalog WHERE ConnectionName = @RecordKey AND IsEnabled = 1) THEN 1 ELSE 0 END AS BIT) AS RecordExists,
            ISNULL((SELECT TOP (1) VersionTag FROM dbo.PlatformExternalConnectionCatalog WHERE ConnectionName = @RecordKey AND IsEnabled = 1), '') AS VersionTag;
        RETURN;
    END;

    SELECT CAST(0 AS BIT) AS RecordExists, '' AS VersionTag;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogchange_find_duplicate
    @TenantId NVARCHAR(50),
    @RecordType NVARCHAR(50),
    @Operation NVARCHAR(50),
    @RecordKey NVARCHAR(200),
    @PayloadHash NVARCHAR(128),
    @IdempotencyKey NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1) *
    FROM dbo.PlatformCatalogChangeRequest
    WHERE TenantId = @TenantId
      AND Status = 'Pending'
      AND RecordType = @RecordType
      AND Operation = @Operation
      AND RecordKey = @RecordKey
      AND (
            PayloadHash = @PayloadHash
            OR (NULLIF(@IdempotencyKey, '') IS NOT NULL AND IdempotencyKey = @IdempotencyKey)
          )
    ORDER BY RequestedAtUtc DESC;
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
    @ExpectedVersionTag NVARCHAR(100) = NULL,
    @IdempotencyKey NVARCHAR(200) = NULL,
    @RollbackOfChangeId NVARCHAR(64) = NULL,
    @PayloadHash NVARCHAR(128),
    @RiskLevel NVARCHAR(50),
    @EnvironmentName NVARCHAR(100),
    @BreakGlass BIT = 0,
    @BreakGlassJustification NVARCHAR(1000) = NULL,
    @RequestedByUserId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.PlatformCatalogChangeRequest
    (
        ChangeId, TenantId, RecordType, Operation, RecordKey, PayloadJson,
        Status, Owner, ChangeNote, VersionTag, ExpectedVersionTag, IdempotencyKey, RollbackOfChangeId,
        PayloadHash, RiskLevel, EnvironmentName, BreakGlass, BreakGlassJustification,
        RequestedByUserId
    )
    VALUES
    (
        @ChangeId, @TenantId, @RecordType, @Operation, @RecordKey, @PayloadJson,
        'Pending', @Owner, @ChangeNote, @VersionTag, @ExpectedVersionTag, @IdempotencyKey, @RollbackOfChangeId,
        @PayloadHash, @RiskLevel, @EnvironmentName, @BreakGlass, @BreakGlassJustification,
        @RequestedByUserId
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

    IF EXISTS (
        SELECT 1
        FROM dbo.PlatformCatalogChangeRequest
        WHERE TenantId = @TenantId
          AND ChangeId = @ChangeId
          AND Status = 'Applied'
    )
    BEGIN
        EXEC dbo.app_platform_catalogchange_get @TenantId = @TenantId, @ChangeId = @ChangeId;
        RETURN;
    END;

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
    @ExpectedVersionTag NVARCHAR(100) = NULL,
    @UpdatedBy NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.PlatformCapabilityCatalog WHERE CapabilityKey = @CapabilityKey)
    BEGIN
        IF NULLIF(@ExpectedVersionTag, '') IS NOT NULL
           AND ISNULL((SELECT TOP (1) VersionTag FROM dbo.PlatformCapabilityCatalog WHERE CapabilityKey = @CapabilityKey), '') <> @ExpectedVersionTag
        BEGIN
            THROW 51011, 'Platform capability catalog version conflict.', 1;
        END;

        UPDATE dbo.PlatformCapabilityCatalog
           SET
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
         WHERE CapabilityKey = @CapabilityKey;
        RETURN;
    END;

    IF NULLIF(@ExpectedVersionTag, '') IS NOT NULL
    BEGIN
        THROW 51012, 'Platform capability catalog expected version supplied for missing record.', 1;
    END;

    INSERT INTO dbo.PlatformCapabilityCatalog
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
    @ExpectedVersionTag NVARCHAR(100) = NULL,
    @UpdatedBy NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.PlatformCapabilityCatalog WHERE CapabilityKey = @CapabilityKey AND IsEnabled = 1)
    BEGIN
        IF NULLIF(@ExpectedVersionTag, '') IS NOT NULL
            THROW 51013, 'Platform capability catalog expected version supplied for missing record.', 1;
        RETURN;
    END;

    IF NULLIF(@ExpectedVersionTag, '') IS NOT NULL
       AND ISNULL((SELECT TOP (1) VersionTag FROM dbo.PlatformCapabilityCatalog WHERE CapabilityKey = @CapabilityKey), '') <> @ExpectedVersionTag
    BEGIN
        THROW 51014, 'Platform capability catalog version conflict.', 1;
    END;

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
    @ExpectedVersionTag NVARCHAR(100) = NULL,
    @UpdatedBy NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.PlatformExternalConnectionCatalog WHERE ConnectionName = @ConnectionName)
    BEGIN
        IF NULLIF(@ExpectedVersionTag, '') IS NOT NULL
           AND ISNULL((SELECT TOP (1) VersionTag FROM dbo.PlatformExternalConnectionCatalog WHERE ConnectionName = @ConnectionName), '') <> @ExpectedVersionTag
        BEGIN
            THROW 51021, 'Platform external connection catalog version conflict.', 1;
        END;

        UPDATE dbo.PlatformExternalConnectionCatalog
           SET
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
         WHERE ConnectionName = @ConnectionName;
        RETURN;
    END;

    IF NULLIF(@ExpectedVersionTag, '') IS NOT NULL
    BEGIN
        THROW 51022, 'Platform external connection expected version supplied for missing record.', 1;
    END;

    INSERT INTO dbo.PlatformExternalConnectionCatalog
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
    @ExpectedVersionTag NVARCHAR(100) = NULL,
    @UpdatedBy NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.PlatformExternalConnectionCatalog WHERE ConnectionName = @ConnectionName AND IsEnabled = 1)
    BEGIN
        IF NULLIF(@ExpectedVersionTag, '') IS NOT NULL
            THROW 51023, 'Platform external connection expected version supplied for missing record.', 1;
        RETURN;
    END;

    IF NULLIF(@ExpectedVersionTag, '') IS NOT NULL
       AND ISNULL((SELECT TOP (1) VersionTag FROM dbo.PlatformExternalConnectionCatalog WHERE ConnectionName = @ConnectionName), '') <> @ExpectedVersionTag
    BEGIN
        THROW 51024, 'Platform external connection catalog version conflict.', 1;
    END;

    UPDATE dbo.PlatformExternalConnectionCatalog
       SET IsEnabled = 0,
           VersionTag = @VersionTag,
           UpdatedBy = @UpdatedBy,
           UpdatedAtUtc = SYSUTCDATETIME()
     WHERE ConnectionName = @ConnectionName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogcertification_list
    @EnvironmentName NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        EvidenceId,
        EnvironmentName,
        EvidenceKind,
        Status,
        Summary,
        EvidenceUri,
        RelatedChangeId,
        RelatedIncidentId,
        OperatorUserId,
        ApprovedByUserId,
        CorrelationId,
        CapturedAtUtc,
        ArtifactHash,
        ArtifactHashAlgorithm,
        ArtifactContentType,
        ArtifactType,
        SourceSystem,
        CollectedAtUtc,
        VerificationStatus,
        VerificationNotes,
        VerifiedByUserId,
        VerifiedAtUtc,
        ExpiresAtUtc,
        SupersededByEvidenceId,
        TrustTier,
        ArtifactProvider,
        ProviderVerifiedAtUtc,
        ArtifactSizeBytes,
        SignedPayload,
        Signature,
        SignatureAlgorithm,
        SignerId,
        SignerPublicKeyId,
        SignatureVerifiedAtUtc,
        VerificationMethod,
        VerificationPolicyVersion
    FROM dbo.PlatformCatalogCertificationEvidence
    WHERE EnvironmentName = @EnvironmentName
    ORDER BY CapturedAtUtc DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogcertification_get
    @EvidenceId NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        EvidenceId,
        EnvironmentName,
        EvidenceKind,
        Status,
        Summary,
        EvidenceUri,
        RelatedChangeId,
        RelatedIncidentId,
        OperatorUserId,
        ApprovedByUserId,
        CorrelationId,
        CapturedAtUtc,
        ArtifactHash,
        ArtifactHashAlgorithm,
        ArtifactContentType,
        ArtifactType,
        SourceSystem,
        CollectedAtUtc,
        VerificationStatus,
        VerificationNotes,
        VerifiedByUserId,
        VerifiedAtUtc,
        ExpiresAtUtc,
        SupersededByEvidenceId,
        TrustTier,
        ArtifactProvider,
        ProviderVerifiedAtUtc,
        ArtifactSizeBytes,
        SignedPayload,
        Signature,
        SignatureAlgorithm,
        SignerId,
        SignerPublicKeyId,
        SignatureVerifiedAtUtc,
        VerificationMethod,
        VerificationPolicyVersion
    FROM dbo.PlatformCatalogCertificationEvidence
    WHERE EvidenceId = @EvidenceId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogcertification_create
    @EvidenceId NVARCHAR(64),
    @EnvironmentName NVARCHAR(100),
    @EvidenceKind NVARCHAR(100),
    @Status NVARCHAR(50),
    @Summary NVARCHAR(1000),
    @EvidenceUri NVARCHAR(1000) = NULL,
    @RelatedChangeId NVARCHAR(64) = NULL,
    @RelatedIncidentId NVARCHAR(100) = NULL,
    @OperatorUserId NVARCHAR(200),
    @ApprovedByUserId NVARCHAR(200) = NULL,
    @CorrelationId NVARCHAR(100) = NULL,
    @ArtifactHash NVARCHAR(128) = NULL,
    @ArtifactHashAlgorithm NVARCHAR(40) = NULL,
    @ArtifactContentType NVARCHAR(200) = NULL,
    @ArtifactType NVARCHAR(100) = NULL,
    @SourceSystem NVARCHAR(100) = NULL,
    @CollectedAtUtc DATETIME2(7) = NULL,
    @VerificationStatus NVARCHAR(50) = 'unverified',
    @VerificationNotes NVARCHAR(1000) = NULL,
    @VerifiedByUserId NVARCHAR(200) = NULL,
    @VerifiedAtUtc DATETIME2(7) = NULL,
    @ExpiresAtUtc DATETIME2(7) = NULL,
    @SupersededByEvidenceId NVARCHAR(64) = NULL,
    @TrustTier NVARCHAR(80) = NULL,
    @ArtifactProvider NVARCHAR(100) = NULL,
    @ProviderVerifiedAtUtc DATETIME2(7) = NULL,
    @ArtifactSizeBytes BIGINT = NULL,
    @SignedPayload NVARCHAR(MAX) = NULL,
    @Signature NVARCHAR(MAX) = NULL,
    @SignatureAlgorithm NVARCHAR(80) = NULL,
    @SignerId NVARCHAR(200) = NULL,
    @SignerPublicKeyId NVARCHAR(200) = NULL,
    @SignatureVerifiedAtUtc DATETIME2(7) = NULL,
    @VerificationMethod NVARCHAR(80) = NULL,
    @VerificationPolicyVersion NVARCHAR(80) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.PlatformCatalogCertificationEvidence
    (
        EvidenceId,
        EnvironmentName,
        EvidenceKind,
        Status,
        Summary,
        EvidenceUri,
        RelatedChangeId,
        RelatedIncidentId,
        OperatorUserId,
        ApprovedByUserId,
        CorrelationId,
        ArtifactHash,
        ArtifactHashAlgorithm,
        ArtifactContentType,
        ArtifactType,
        SourceSystem,
        CollectedAtUtc,
        VerificationStatus,
        VerificationNotes,
        VerifiedByUserId,
        VerifiedAtUtc,
        ExpiresAtUtc,
        SupersededByEvidenceId,
        TrustTier,
        ArtifactProvider,
        ProviderVerifiedAtUtc,
        ArtifactSizeBytes,
        SignedPayload,
        Signature,
        SignatureAlgorithm,
        SignerId,
        SignerPublicKeyId,
        SignatureVerifiedAtUtc,
        VerificationMethod,
        VerificationPolicyVersion
    )
    VALUES
    (
        @EvidenceId,
        LTRIM(RTRIM(@EnvironmentName)),
        LTRIM(RTRIM(@EvidenceKind)),
        LOWER(LTRIM(RTRIM(@Status))),
        LTRIM(RTRIM(@Summary)),
        NULLIF(LTRIM(RTRIM(@EvidenceUri)), ''),
        NULLIF(LTRIM(RTRIM(@RelatedChangeId)), ''),
        NULLIF(LTRIM(RTRIM(@RelatedIncidentId)), ''),
        LTRIM(RTRIM(@OperatorUserId)),
        NULLIF(LTRIM(RTRIM(@ApprovedByUserId)), ''),
        NULLIF(LTRIM(RTRIM(@CorrelationId)), ''),
        NULLIF(LTRIM(RTRIM(@ArtifactHash)), ''),
        NULLIF(LTRIM(RTRIM(@ArtifactHashAlgorithm)), ''),
        NULLIF(LTRIM(RTRIM(@ArtifactContentType)), ''),
        NULLIF(LTRIM(RTRIM(@ArtifactType)), ''),
        NULLIF(LTRIM(RTRIM(@SourceSystem)), ''),
        @CollectedAtUtc,
        LOWER(LTRIM(RTRIM(@VerificationStatus))),
        NULLIF(LTRIM(RTRIM(@VerificationNotes)), ''),
        NULLIF(LTRIM(RTRIM(@VerifiedByUserId)), ''),
        @VerifiedAtUtc,
        @ExpiresAtUtc,
        NULLIF(LTRIM(RTRIM(@SupersededByEvidenceId)), ''),
        NULLIF(LTRIM(RTRIM(@TrustTier)), ''),
        NULLIF(LTRIM(RTRIM(@ArtifactProvider)), ''),
        @ProviderVerifiedAtUtc,
        @ArtifactSizeBytes,
        NULLIF(LTRIM(RTRIM(@SignedPayload)), ''),
        NULLIF(LTRIM(RTRIM(@Signature)), ''),
        NULLIF(LTRIM(RTRIM(@SignatureAlgorithm)), ''),
        NULLIF(LTRIM(RTRIM(@SignerId)), ''),
        NULLIF(LTRIM(RTRIM(@SignerPublicKeyId)), ''),
        @SignatureVerifiedAtUtc,
        NULLIF(LTRIM(RTRIM(@VerificationMethod)), ''),
        NULLIF(LTRIM(RTRIM(@VerificationPolicyVersion)), '')
    );

    SELECT *
    FROM dbo.PlatformCatalogCertificationEvidence
    WHERE EvidenceId = @EvidenceId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_catalogcertification_verify
    @EvidenceId NVARCHAR(64),
    @Status NVARCHAR(50),
    @VerificationStatus NVARCHAR(50),
    @VerificationNotes NVARCHAR(1000) = NULL,
    @VerifiedByUserId NVARCHAR(200),
    @VerifiedAtUtc DATETIME2(7),
    @ExpiresAtUtc DATETIME2(7) = NULL,
    @TrustTier NVARCHAR(80) = NULL,
    @ArtifactProvider NVARCHAR(100) = NULL,
    @ProviderVerifiedAtUtc DATETIME2(7) = NULL,
    @ArtifactSizeBytes BIGINT = NULL,
    @SignerId NVARCHAR(200) = NULL,
    @SignerPublicKeyId NVARCHAR(200) = NULL,
    @SignatureVerifiedAtUtc DATETIME2(7) = NULL,
    @VerificationMethod NVARCHAR(80) = NULL,
    @VerificationPolicyVersion NVARCHAR(80) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.PlatformCatalogCertificationEvidence
    SET
        Status = LOWER(LTRIM(RTRIM(@Status))),
        VerificationStatus = LOWER(LTRIM(RTRIM(@VerificationStatus))),
        VerificationNotes = NULLIF(LTRIM(RTRIM(@VerificationNotes)), ''),
        VerifiedByUserId = LTRIM(RTRIM(@VerifiedByUserId)),
        VerifiedAtUtc = @VerifiedAtUtc,
        ExpiresAtUtc = @ExpiresAtUtc,
        TrustTier = NULLIF(LTRIM(RTRIM(@TrustTier)), ''),
        ArtifactProvider = NULLIF(LTRIM(RTRIM(@ArtifactProvider)), ''),
        ProviderVerifiedAtUtc = @ProviderVerifiedAtUtc,
        ArtifactSizeBytes = @ArtifactSizeBytes,
        SignerId = NULLIF(LTRIM(RTRIM(@SignerId)), ''),
        SignerPublicKeyId = NULLIF(LTRIM(RTRIM(@SignerPublicKeyId)), ''),
        SignatureVerifiedAtUtc = @SignatureVerifiedAtUtc,
        VerificationMethod = NULLIF(LTRIM(RTRIM(@VerificationMethod)), ''),
        VerificationPolicyVersion = NULLIF(LTRIM(RTRIM(@VerificationPolicyVersion)), '')
    WHERE EvidenceId = @EvidenceId;

    SELECT *
    FROM dbo.PlatformCatalogCertificationEvidence
    WHERE EvidenceId = @EvidenceId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_promotionmanifest_create
    @ManifestId NVARCHAR(64),
    @ManifestHash NVARCHAR(128),
    @EnvironmentName NVARCHAR(100),
    @Status NVARCHAR(50),
    @ChangeIdsJson NVARCHAR(MAX),
    @EvidenceIdsJson NVARCHAR(MAX),
    @GateSummaryJson NVARCHAR(MAX),
    @RollbackOfManifestId NVARCHAR(64) = NULL,
    @RelatedIncidentId NVARCHAR(100) = NULL,
    @Notes NVARCHAR(1000) = NULL,
    @CreatedByUserId NVARCHAR(200),
    @IssuedByUserId NVARCHAR(200),
    @CorrelationId NVARCHAR(100) = NULL,
    @CreatedAtUtc DATETIME2(7),
    @IssuedAtUtc DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.PlatformCatalogPromotionManifest
    (
        ManifestId,
        ManifestHash,
        EnvironmentName,
        Status,
        ChangeIdsJson,
        EvidenceIdsJson,
        GateSummaryJson,
        RollbackOfManifestId,
        RelatedIncidentId,
        Notes,
        CreatedByUserId,
        IssuedByUserId,
        CorrelationId,
        CreatedAtUtc,
        IssuedAtUtc
    )
    VALUES
    (
        @ManifestId,
        @ManifestHash,
        LTRIM(RTRIM(@EnvironmentName)),
        LOWER(LTRIM(RTRIM(@Status))),
        @ChangeIdsJson,
        @EvidenceIdsJson,
        @GateSummaryJson,
        NULLIF(LTRIM(RTRIM(@RollbackOfManifestId)), ''),
        NULLIF(LTRIM(RTRIM(@RelatedIncidentId)), ''),
        NULLIF(LTRIM(RTRIM(@Notes)), ''),
        LTRIM(RTRIM(@CreatedByUserId)),
        LTRIM(RTRIM(@IssuedByUserId)),
        NULLIF(LTRIM(RTRIM(@CorrelationId)), ''),
        @CreatedAtUtc,
        @IssuedAtUtc
    );

    SELECT *
    FROM dbo.PlatformCatalogPromotionManifest
    WHERE ManifestId = @ManifestId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_promotionmanifest_get
    @ManifestId NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM dbo.PlatformCatalogPromotionManifest
    WHERE ManifestId = @ManifestId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_promotionmanifest_list
    @EnvironmentName NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM dbo.PlatformCatalogPromotionManifest
    WHERE EnvironmentName = @EnvironmentName
    ORDER BY IssuedAtUtc DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_promotionattestation_create
    @AttestationId NVARCHAR(64),
    @ManifestId NVARCHAR(64),
    @EnvironmentName NVARCHAR(100),
    @State NVARCHAR(50),
    @Notes NVARCHAR(1000) = NULL,
    @EvidenceIdsJson NVARCHAR(MAX),
    @ActorUserId NVARCHAR(200),
    @AcceptedByUserId NVARCHAR(200) = NULL,
    @CorrelationId NVARCHAR(100) = NULL,
    @CreatedAtUtc DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.PlatformCatalogRolloutAttestation
    (
        AttestationId,
        ManifestId,
        EnvironmentName,
        State,
        Notes,
        EvidenceIdsJson,
        ActorUserId,
        AcceptedByUserId,
        CorrelationId,
        CreatedAtUtc
    )
    VALUES
    (
        @AttestationId,
        @ManifestId,
        LTRIM(RTRIM(@EnvironmentName)),
        LOWER(LTRIM(RTRIM(@State))),
        NULLIF(LTRIM(RTRIM(@Notes)), ''),
        @EvidenceIdsJson,
        LTRIM(RTRIM(@ActorUserId)),
        NULLIF(LTRIM(RTRIM(@AcceptedByUserId)), ''),
        NULLIF(LTRIM(RTRIM(@CorrelationId)), ''),
        @CreatedAtUtc
    );

    SELECT *
    FROM dbo.PlatformCatalogRolloutAttestation
    WHERE AttestationId = @AttestationId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_platform_promotionattestation_list
    @ManifestId NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM dbo.PlatformCatalogRolloutAttestation
    WHERE ManifestId = @ManifestId
    ORDER BY CreatedAtUtc ASC;
END;
GO
