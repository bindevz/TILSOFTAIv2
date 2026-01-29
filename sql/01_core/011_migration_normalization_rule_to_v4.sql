SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.NormalizationRule', 'U') IS NULL
BEGIN
    RETURN;
END;

DECLARE @hasId bit = 0;
DECLARE @pkOnId bit = 0;

SELECT @hasId = CASE WHEN EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.NormalizationRule') AND name = 'Id'
) THEN 1 ELSE 0 END;

IF @hasId = 1
BEGIN
    SELECT @pkOnId = CASE WHEN EXISTS (
        SELECT 1
        FROM sys.key_constraints kc
        JOIN sys.index_columns ic
            ON kc.parent_object_id = ic.object_id
           AND kc.unique_index_id = ic.index_id
        JOIN sys.columns c
            ON ic.object_id = c.object_id
           AND ic.column_id = c.column_id
        WHERE kc.parent_object_id = OBJECT_ID('dbo.NormalizationRule')
          AND kc.type = 'PK'
        GROUP BY kc.name
        HAVING COUNT(1) = 1 AND MAX(CASE WHEN c.name = 'Id' THEN 1 ELSE 0 END) = 1
    ) THEN 1 ELSE 0 END;
END;

IF EXISTS (
    SELECT 1 FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.NormalizationRule')
      AND name = 'UQ_NormalizationRule_RuleKey_Tenant'
)
BEGIN
    ALTER TABLE dbo.NormalizationRule
        DROP CONSTRAINT UQ_NormalizationRule_RuleKey_Tenant;
END;

IF @hasId = 1 AND @pkOnId = 1
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NormalizationRule_Global_RuleKey' AND object_id = OBJECT_ID('dbo.NormalizationRule'))
    BEGIN
        CREATE UNIQUE INDEX UX_NormalizationRule_Global_RuleKey
            ON dbo.NormalizationRule(RuleKey)
            WHERE TenantId IS NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NormalizationRule_Tenant_RuleKey_Tenant' AND object_id = OBJECT_ID('dbo.NormalizationRule'))
    BEGIN
        CREATE UNIQUE INDEX UX_NormalizationRule_Tenant_RuleKey_Tenant
            ON dbo.NormalizationRule(RuleKey, TenantId)
            WHERE TenantId IS NOT NULL;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_NormalizationRule_Tenant_Priority'
          AND object_id = OBJECT_ID('dbo.NormalizationRule')
    )
    BEGIN
        CREATE INDEX IX_NormalizationRule_Tenant_Priority
            ON dbo.NormalizationRule (TenantId, Priority);
    END;

    RETURN;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.NormalizationRule
    WHERE TenantId IS NULL
    GROUP BY RuleKey
    HAVING COUNT(1) > 1
)
BEGIN
    RAISERROR('NormalizationRule duplicate keys detected for global rows (RuleKey). Resolve duplicates before migration.', 16, 1);
    RETURN;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.NormalizationRule
    WHERE TenantId IS NOT NULL
    GROUP BY RuleKey, TenantId
    HAVING COUNT(1) > 1
)
BEGIN
    RAISERROR('NormalizationRule duplicate keys detected for tenant rows (RuleKey, TenantId). Resolve duplicates before migration.', 16, 1);
    RETURN;
END;

IF OBJECT_ID('dbo.NormalizationRule_v4', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.NormalizationRule_v4;
END;

CREATE TABLE dbo.NormalizationRule_v4
(
    Id bigint IDENTITY(1,1) NOT NULL,
    RuleKey nvarchar(200) NOT NULL,
    TenantId nvarchar(50) NULL,
    Priority int NOT NULL,
    Pattern nvarchar(1000) NOT NULL,
    Replacement nvarchar(1000) NOT NULL,
    Description nvarchar(2000) NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_NormalizationRule_v4_IsEnabled DEFAULT(1),
    UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_NormalizationRule_v4_UpdatedAtUtc DEFAULT sysutcdatetime(),
    CONSTRAINT PK_NormalizationRule_v4_Id PRIMARY KEY (Id)
);

DECLARE @hasUpdatedAt bit = CASE WHEN COL_LENGTH('dbo.NormalizationRule', 'UpdatedAtUtc') IS NULL THEN 0 ELSE 1 END;

DECLARE @sql nvarchar(max) = N'INSERT INTO dbo.NormalizationRule_v4 (RuleKey, TenantId, Priority, Pattern, Replacement, Description, IsEnabled, UpdatedAtUtc) '
    + N'SELECT RuleKey, TenantId, Priority, Pattern, Replacement, Description, IsEnabled, '
    + CASE WHEN @hasUpdatedAt = 1 THEN N'COALESCE(UpdatedAtUtc, SYSUTCDATETIME())' ELSE N'SYSUTCDATETIME()' END
    + N' FROM dbo.NormalizationRule;';

EXEC sp_executesql @sql;

DECLARE @suffix nvarchar(20) = REPLACE(REPLACE(CONVERT(varchar(16), SYSUTCDATETIME(), 120), '-', ''), ':', '');
DECLARE @legacyName nvarchar(128) = CONCAT('NormalizationRule_legacy_', @suffix);

EXEC sp_rename 'dbo.NormalizationRule', @legacyName;
EXEC sp_rename 'dbo.NormalizationRule_v4', 'NormalizationRule';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NormalizationRule_Global_RuleKey' AND object_id = OBJECT_ID('dbo.NormalizationRule'))
BEGIN
    CREATE UNIQUE INDEX UX_NormalizationRule_Global_RuleKey
        ON dbo.NormalizationRule(RuleKey)
        WHERE TenantId IS NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_NormalizationRule_Tenant_RuleKey_Tenant' AND object_id = OBJECT_ID('dbo.NormalizationRule'))
BEGIN
    CREATE UNIQUE INDEX UX_NormalizationRule_Tenant_RuleKey_Tenant
        ON dbo.NormalizationRule(RuleKey, TenantId)
        WHERE TenantId IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_NormalizationRule_Tenant_Priority'
      AND object_id = OBJECT_ID('dbo.NormalizationRule')
)
BEGIN
    CREATE INDEX IX_NormalizationRule_Tenant_Priority
        ON dbo.NormalizationRule (TenantId, Priority);
END;
GO
