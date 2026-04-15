SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- SQL compatibility observability and lifecycle governance.
-- Tracks usage of legacy compatibility procedures and the
-- capability-scope wrapper procedures that replace them.
-- ============================================================

IF OBJECT_ID('dbo.SqlCompatibilityUsageLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SqlCompatibilityUsageLog
    (
        UsageId            bigint IDENTITY(1,1) NOT NULL,
        SurfaceName        nvarchar(128) NOT NULL,
        SurfaceKind        nvarchar(40)  NOT NULL,
        ForwardSurfaceName nvarchar(128) NULL,
        TenantId           nvarchar(50)  NULL,
        AppKey             nvarchar(50)  NULL,
        Language           nvarchar(10)  NULL,
        SessionLogin       nvarchar(128) NULL,
        OriginalLogin      nvarchar(128) NULL,
        HostName           nvarchar(128) NULL,
        AppName            nvarchar(128) NULL,
        DatabaseName       nvarchar(128) NULL,
        RequestId          uniqueidentifier NULL,
        CompatibilityNotes nvarchar(400) NULL,
        ObservedAtUtc      datetime2(3) NOT NULL
            CONSTRAINT DF_SqlCompatibilityUsageLog_ObservedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SqlCompatibilityUsageLog PRIMARY KEY CLUSTERED (UsageId),
        CONSTRAINT CK_SqlCompatibilityUsageLog_SurfaceKind CHECK
            (SurfaceKind IN (N'legacy-procedure', N'capability-scope-wrapper'))
    );
END;
GO

IF OBJECT_ID('dbo.SqlCompatibilityUsageRollup', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SqlCompatibilityUsageRollup
    (
        ObservedDateUtc    date NOT NULL,
        SurfaceKind        nvarchar(40)  NOT NULL,
        SurfaceName        nvarchar(128) NOT NULL,
        ForwardSurfaceName nvarchar(128) NULL,
        TenantId           nvarchar(50)  NOT NULL
            CONSTRAINT DF_SqlCompatibilityUsageRollup_TenantId DEFAULT N'',
        AppKey             nvarchar(50)  NOT NULL
            CONSTRAINT DF_SqlCompatibilityUsageRollup_AppKey DEFAULT N'',
        Language           nvarchar(10)  NOT NULL
            CONSTRAINT DF_SqlCompatibilityUsageRollup_Language DEFAULT N'',
        UsageCount         bigint        NOT NULL,
        FirstObservedAtUtc datetime2(3)  NOT NULL,
        LastObservedAtUtc  datetime2(3)  NOT NULL,
        RolledUpAtUtc      datetime2(3)  NOT NULL
            CONSTRAINT DF_SqlCompatibilityUsageRollup_RolledUpAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SqlCompatibilityUsageRollup PRIMARY KEY CLUSTERED
        (
            ObservedDateUtc,
            SurfaceKind,
            SurfaceName,
            TenantId,
            AppKey,
            Language
        ),
        CONSTRAINT CK_SqlCompatibilityUsageRollup_SurfaceKind CHECK
            (SurfaceKind IN (N'legacy-procedure', N'capability-scope-wrapper'))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_SqlCompatibilityUsageLog_Surface_Date'
      AND object_id = OBJECT_ID(N'dbo.SqlCompatibilityUsageLog')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_SqlCompatibilityUsageLog_Surface_Date
        ON dbo.SqlCompatibilityUsageLog (SurfaceKind, SurfaceName, ObservedAtUtc DESC)
        INCLUDE (ForwardSurfaceName, TenantId, AppKey, Language);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_SqlCompatibilityUsageLog_Tenant_Date'
      AND object_id = OBJECT_ID(N'dbo.SqlCompatibilityUsageLog')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_SqlCompatibilityUsageLog_Tenant_Date
        ON dbo.SqlCompatibilityUsageLog (TenantId, ObservedAtUtc DESC)
        INCLUDE (SurfaceKind, SurfaceName, ForwardSurfaceName, AppKey);
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_sql_compatibility_usage_record
    @SurfaceName        nvarchar(128),
    @SurfaceKind        nvarchar(40),
    @ForwardSurfaceName nvarchar(128) = NULL,
    @TenantId           nvarchar(50) = NULL,
    @AppKey             nvarchar(50) = NULL,
    @Language           nvarchar(10) = NULL,
    @RequestId          uniqueidentifier = NULL,
    @CompatibilityNotes nvarchar(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @SurfaceKind NOT IN (N'legacy-procedure', N'capability-scope-wrapper')
    BEGIN
        RAISERROR('@SurfaceKind must be legacy-procedure or capability-scope-wrapper.', 16, 1);
        RETURN;
    END

    INSERT INTO dbo.SqlCompatibilityUsageLog
        (
            SurfaceName,
            SurfaceKind,
            ForwardSurfaceName,
            TenantId,
            AppKey,
            Language,
            SessionLogin,
            OriginalLogin,
            HostName,
            AppName,
            DatabaseName,
            RequestId,
            CompatibilityNotes
        )
    VALUES
        (
            @SurfaceName,
            @SurfaceKind,
            @ForwardSurfaceName,
            @TenantId,
            @AppKey,
            @Language,
            SUSER_SNAME(),
            ORIGINAL_LOGIN(),
            HOST_NAME(),
            APP_NAME(),
            DB_NAME(),
            @RequestId,
            @CompatibilityNotes
        );
END;
GO

CREATE OR ALTER VIEW dbo.SqlCompatibilityUsageDaily
AS
    WITH RawDaily AS
    (
        SELECT
            CAST(ObservedAtUtc AS date) AS ObservedDateUtc,
            SurfaceKind,
            SurfaceName,
            ForwardSurfaceName,
            TenantId,
            AppKey,
            Language,
            COUNT_BIG(*) AS UsageCount,
            MIN(ObservedAtUtc) AS FirstObservedAtUtc,
            MAX(LastObservedAtUtc) AS LastObservedAtUtc
        FROM dbo.SqlCompatibilityUsageLog raw
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.SqlCompatibilityUsageRollup rollup
            WHERE rollup.ObservedDateUtc = CAST(raw.ObservedAtUtc AS date)
              AND rollup.SurfaceKind = raw.SurfaceKind
              AND rollup.SurfaceName = raw.SurfaceName
              AND ISNULL(rollup.TenantId, N'') = ISNULL(raw.TenantId, N'')
              AND ISNULL(rollup.AppKey, N'') = ISNULL(raw.AppKey, N'')
              AND ISNULL(rollup.Language, N'') = ISNULL(raw.Language, N'')
        )
        GROUP BY
            CAST(ObservedAtUtc AS date),
            SurfaceKind,
            SurfaceName,
            ForwardSurfaceName,
            TenantId,
            AppKey,
            Language
    )
    SELECT
        ObservedDateUtc,
        SurfaceKind,
        SurfaceName,
        ForwardSurfaceName,
        TenantId,
        AppKey,
        Language,
        UsageCount,
        FirstObservedAtUtc,
        LastObservedAtUtc
    FROM dbo.SqlCompatibilityUsageRollup
    UNION ALL
    SELECT
        ObservedDateUtc,
        SurfaceKind,
        SurfaceName,
        ForwardSurfaceName,
        TenantId,
        AppKey,
        Language,
        UsageCount,
        FirstObservedAtUtc,
        LastObservedAtUtc
    FROM RawDaily;
GO

CREATE OR ALTER PROCEDURE dbo.app_sql_compatibility_usage_rollup
    @ThroughUtc datetime2(3) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RollupThroughUtc datetime2(3) = COALESCE(@ThroughUtc, DATEADD(day, -90, SYSUTCDATETIME()));

    ;WITH Daily AS
    (
        SELECT
            CAST(ObservedAtUtc AS date) AS ObservedDateUtc,
            SurfaceKind,
            SurfaceName,
            MAX(ForwardSurfaceName) AS ForwardSurfaceName,
            TenantId,
            AppKey,
            Language,
            COUNT_BIG(*) AS UsageCount,
            MIN(ObservedAtUtc) AS FirstObservedAtUtc,
            MAX(ObservedAtUtc) AS LastObservedAtUtc
        FROM dbo.SqlCompatibilityUsageLog
        WHERE ObservedAtUtc < @RollupThroughUtc
        GROUP BY
            CAST(ObservedAtUtc AS date),
            SurfaceKind,
            SurfaceName,
            TenantId,
            AppKey,
            Language
    )
    MERGE dbo.SqlCompatibilityUsageRollup AS target
    USING Daily AS source
       ON target.ObservedDateUtc = source.ObservedDateUtc
      AND target.SurfaceKind = source.SurfaceKind
      AND target.SurfaceName = source.SurfaceName
      AND ISNULL(target.TenantId, N'') = ISNULL(source.TenantId, N'')
      AND ISNULL(target.AppKey, N'') = ISNULL(source.AppKey, N'')
      AND ISNULL(target.Language, N'') = ISNULL(source.Language, N'')
    WHEN MATCHED THEN
        UPDATE SET
            ForwardSurfaceName = source.ForwardSurfaceName,
            UsageCount = source.UsageCount,
            FirstObservedAtUtc = source.FirstObservedAtUtc,
            LastObservedAtUtc = source.LastObservedAtUtc,
            RolledUpAtUtc = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT
            (
                ObservedDateUtc,
                SurfaceKind,
                SurfaceName,
                ForwardSurfaceName,
                TenantId,
                AppKey,
                Language,
                UsageCount,
                FirstObservedAtUtc,
                LastObservedAtUtc
            )
        VALUES
            (
                source.ObservedDateUtc,
                source.SurfaceKind,
                source.SurfaceName,
                source.ForwardSurfaceName,
                COALESCE(source.TenantId, N''),
                COALESCE(source.AppKey, N''),
                COALESCE(source.Language, N''),
                source.UsageCount,
                source.FirstObservedAtUtc,
                source.LastObservedAtUtc
            );

    SELECT @@ROWCOUNT AS RollupRowsAffected;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_sql_compatibility_usage_purge
    @RawRetentionDays int = 90
AS
BEGIN
    SET NOCOUNT ON;

    IF @RawRetentionDays < 30
    BEGIN
        RAISERROR('@RawRetentionDays must be at least 30 days.', 16, 1);
        RETURN;
    END

    DECLARE @CutoffUtc datetime2(3) = DATEADD(day, -@RawRetentionDays, SYSUTCDATETIME());

    EXEC dbo.app_sql_compatibility_usage_rollup @ThroughUtc = @CutoffUtc;

    DELETE FROM dbo.SqlCompatibilityUsageLog
    WHERE ObservedAtUtc < @CutoffUtc;

    SELECT
        @CutoffUtc AS RawCutoffUtc,
        @RawRetentionDays AS RawRetentionDays,
        @@ROWCOUNT AS DeletedRawUsageRows;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_sql_compatibility_usage_summary
    @SinceUtc    datetime2(3) = NULL,
    @SurfaceKind nvarchar(40) = NULL,
    @TenantId    nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        SurfaceKind,
        SurfaceName,
        ForwardSurfaceName,
        TenantId,
        AppKey,
        Language,
        SUM(UsageCount) AS UsageCount,
        MIN(FirstObservedAtUtc) AS FirstObservedAtUtc,
        MAX(LastObservedAtUtc) AS LastObservedAtUtc
    FROM dbo.SqlCompatibilityUsageDaily
    WHERE (@SinceUtc IS NULL OR LastObservedAtUtc >= @SinceUtc)
      AND (@SurfaceKind IS NULL OR SurfaceKind = @SurfaceKind)
      AND (@TenantId IS NULL OR TenantId = @TenantId)
    GROUP BY SurfaceKind, SurfaceName, ForwardSurfaceName, TenantId, AppKey, Language
    ORDER BY SurfaceKind, SurfaceName, TenantId, AppKey, Language;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_sql_compatibility_retirement_readiness
    @SinceUtc datetime2(3) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @WindowStartUtc datetime2(3) = COALESCE(@SinceUtc, DATEADD(day, -30, SYSUTCDATETIME()));

    ;WITH UsageByKind AS
    (
        SELECT
            SurfaceKind,
            SUM(UsageCount) AS UsageCount,
            MAX(ObservedAtUtc) AS LastObservedAtUtc
        FROM dbo.SqlCompatibilityUsageDaily
        WHERE LastObservedAtUtc >= @WindowStartUtc
        GROUP BY SurfaceKind
    )
    SELECT
        @WindowStartUtc AS WindowStartUtc,
        SYSUTCDATETIME() AS EvaluatedAtUtc,
        COALESCE(MAX(CASE WHEN SurfaceKind = N'legacy-procedure' THEN UsageCount END), 0) AS LegacyProcedureUsageCount,
        COALESCE(MAX(CASE WHEN SurfaceKind = N'capability-scope-wrapper' THEN UsageCount END), 0) AS CapabilityScopeWrapperUsageCount,
        MAX(CASE WHEN SurfaceKind = N'legacy-procedure' THEN LastObservedAtUtc END) AS LegacyProcedureLastObservedAtUtc,
        MAX(CASE WHEN SurfaceKind = N'capability-scope-wrapper' THEN LastObservedAtUtc END) AS CapabilityScopeWrapperLastObservedAtUtc,
        CAST(
            CASE
                WHEN COALESCE(MAX(CASE WHEN SurfaceKind = N'legacy-procedure' THEN UsageCount END), 0) = 0
                 AND COALESCE(MAX(CASE WHEN SurfaceKind = N'capability-scope-wrapper' THEN UsageCount END), 0) > 0
                THEN 1
                ELSE 0
            END AS bit
        ) AS IsDbMajorRenameCandidate
    FROM UsageByKind;
END;
GO
