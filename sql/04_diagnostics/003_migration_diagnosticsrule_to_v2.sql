SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.DiagnosticsRule', 'U') IS NULL
BEGIN
    RETURN;
END;

DECLARE @hasId bit = 0;
DECLARE @pkOnId bit = 0;

SELECT @hasId = CASE WHEN EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DiagnosticsRule') AND name = 'Id'
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
        WHERE kc.parent_object_id = OBJECT_ID('dbo.DiagnosticsRule')
          AND kc.type = 'PK'
        GROUP BY kc.name
        HAVING COUNT(1) = 1 AND MAX(CASE WHEN c.name = 'Id' THEN 1 ELSE 0 END) = 1
    ) THEN 1 ELSE 0 END;
END;

IF COL_LENGTH('dbo.DiagnosticsRule', 'Module') IS NULL
BEGIN
    ALTER TABLE dbo.DiagnosticsRule
        ADD Module nvarchar(100) NOT NULL CONSTRAINT DF_DiagnosticsRule_Module DEFAULT('core');
END;

IF @hasId = 1 AND @pkOnId = 1
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE name = 'CK_DiagnosticsRule_AiSpNamePrefix'
          AND parent_object_id = OBJECT_ID('dbo.DiagnosticsRule'))
    BEGIN
        ALTER TABLE dbo.DiagnosticsRule
        ADD CONSTRAINT CK_DiagnosticsRule_AiSpNamePrefix CHECK (AiSpName LIKE 'ai[_]%');
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DiagnosticsRule_Global_Module_RuleKey' AND object_id = OBJECT_ID('dbo.DiagnosticsRule'))
    BEGIN
        CREATE UNIQUE INDEX UX_DiagnosticsRule_Global_Module_RuleKey
            ON dbo.DiagnosticsRule(Module, RuleKey)
            WHERE TenantId IS NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DiagnosticsRule_Tenant_Module_RuleKey_Tenant' AND object_id = OBJECT_ID('dbo.DiagnosticsRule'))
    BEGIN
        CREATE UNIQUE INDEX UX_DiagnosticsRule_Tenant_Module_RuleKey_Tenant
            ON dbo.DiagnosticsRule(Module, RuleKey, TenantId)
            WHERE TenantId IS NOT NULL;
    END;

    RETURN;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.DiagnosticsRule
    WHERE TenantId IS NULL
    GROUP BY Module, RuleKey
    HAVING COUNT(1) > 1
)
BEGIN
    RAISERROR('DiagnosticsRule duplicate keys detected for global rows (Module, RuleKey). Resolve duplicates before migration.', 16, 1);
    RETURN;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.DiagnosticsRule
    WHERE TenantId IS NOT NULL
    GROUP BY Module, RuleKey, TenantId
    HAVING COUNT(1) > 1
)
BEGIN
    RAISERROR('DiagnosticsRule duplicate keys detected for tenant rows (Module, RuleKey, TenantId). Resolve duplicates before migration.', 16, 1);
    RETURN;
END;

IF OBJECT_ID('dbo.DiagnosticsRule_v2', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.DiagnosticsRule_v2;
END;

CREATE TABLE dbo.DiagnosticsRule_v2
(
    Id bigint IDENTITY(1,1) NOT NULL,
    RuleKey nvarchar(200) NOT NULL,
    TenantId nvarchar(50) NULL,
    Module nvarchar(100) NOT NULL,
    Description nvarchar(2000) NOT NULL,
    AiSpName nvarchar(200) NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_DiagnosticsRule_v2_IsEnabled DEFAULT(1),
    UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_DiagnosticsRule_v2_UpdatedAtUtc DEFAULT sysutcdatetime(),
    CONSTRAINT PK_DiagnosticsRule_v2_Id PRIMARY KEY (Id),
    CONSTRAINT CK_DiagnosticsRule_v2_AiSpNamePrefix CHECK (AiSpName LIKE 'ai[_]%')
);

DECLARE @hasUpdatedAt bit = CASE WHEN COL_LENGTH('dbo.DiagnosticsRule', 'UpdatedAtUtc') IS NULL THEN 0 ELSE 1 END;

DECLARE @sql nvarchar(max) = N'INSERT INTO dbo.DiagnosticsRule_v2 (RuleKey, TenantId, Module, Description, AiSpName, IsEnabled, UpdatedAtUtc) '
    + N'SELECT RuleKey, TenantId, Module, Description, AiSpName, IsEnabled, '
    + CASE WHEN @hasUpdatedAt = 1 THEN N'COALESCE(UpdatedAtUtc, SYSUTCDATETIME())' ELSE N'SYSUTCDATETIME()' END
    + N' FROM dbo.DiagnosticsRule;';

EXEC sp_executesql @sql;

DECLARE @suffix nvarchar(20) = REPLACE(REPLACE(CONVERT(varchar(16), SYSUTCDATETIME(), 120), '-', ''), ':', '');
DECLARE @legacyName nvarchar(128) = CONCAT('DiagnosticsRule_legacy_', @suffix);

EXEC sp_rename 'dbo.DiagnosticsRule', @legacyName;
EXEC sp_rename 'dbo.DiagnosticsRule_v2', 'DiagnosticsRule';

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_DiagnosticsRule_AiSpNamePrefix'
      AND parent_object_id = OBJECT_ID('dbo.DiagnosticsRule'))
BEGIN
    ALTER TABLE dbo.DiagnosticsRule
    ADD CONSTRAINT CK_DiagnosticsRule_AiSpNamePrefix CHECK (AiSpName LIKE 'ai[_]%');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DiagnosticsRule_Global_Module_RuleKey' AND object_id = OBJECT_ID('dbo.DiagnosticsRule'))
BEGIN
    CREATE UNIQUE INDEX UX_DiagnosticsRule_Global_Module_RuleKey
        ON dbo.DiagnosticsRule(Module, RuleKey)
        WHERE TenantId IS NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DiagnosticsRule_Tenant_Module_RuleKey_Tenant' AND object_id = OBJECT_ID('dbo.DiagnosticsRule'))
BEGIN
    CREATE UNIQUE INDEX UX_DiagnosticsRule_Tenant_Module_RuleKey_Tenant
        ON dbo.DiagnosticsRule(Module, RuleKey, TenantId)
        WHERE TenantId IS NOT NULL;
END;
GO
