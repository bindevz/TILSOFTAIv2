SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.MetadataDictionary', 'U') IS NULL
BEGIN
    RETURN;
END;

DECLARE @hasId bit = 0;
DECLARE @pkOnId bit = 0;

SELECT @hasId = CASE WHEN EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MetadataDictionary') AND name = 'Id'
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
        WHERE kc.parent_object_id = OBJECT_ID('dbo.MetadataDictionary')
          AND kc.type = 'PK'
        GROUP BY kc.name
        HAVING COUNT(1) = 1 AND MAX(CASE WHEN c.name = 'Id' THEN 1 ELSE 0 END) = 1
    ) THEN 1 ELSE 0 END;
END;

IF EXISTS (
    SELECT 1 FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.MetadataDictionary')
      AND name = 'UQ_MetadataDictionary_Key_Tenant_Lang'
)
BEGIN
    ALTER TABLE dbo.MetadataDictionary
        DROP CONSTRAINT UQ_MetadataDictionary_Key_Tenant_Lang;
END;

IF @hasId = 1 AND @pkOnId = 1
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_MetadataDictionary_Global_Key_Lang' AND object_id = OBJECT_ID('dbo.MetadataDictionary'))
    BEGIN
        CREATE UNIQUE INDEX UX_MetadataDictionary_Global_Key_Lang
            ON dbo.MetadataDictionary([Key], Language)
            WHERE TenantId IS NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_MetadataDictionary_Tenant_Key_Tenant_Lang' AND object_id = OBJECT_ID('dbo.MetadataDictionary'))
    BEGIN
        CREATE UNIQUE INDEX UX_MetadataDictionary_Tenant_Key_Tenant_Lang
            ON dbo.MetadataDictionary([Key], TenantId, Language)
            WHERE TenantId IS NOT NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MetadataDictionary_Key_Lang' AND object_id = OBJECT_ID('dbo.MetadataDictionary'))
    BEGIN
        CREATE INDEX IX_MetadataDictionary_Key_Lang
            ON dbo.MetadataDictionary([Key], Language);
    END;

    RETURN;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.MetadataDictionary
    WHERE TenantId IS NULL
    GROUP BY [Key], Language
    HAVING COUNT(1) > 1
)
BEGIN
    RAISERROR('MetadataDictionary duplicate keys detected for global rows ([Key], Language). Resolve duplicates before migration.', 16, 1);
    RETURN;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.MetadataDictionary
    WHERE TenantId IS NOT NULL
    GROUP BY [Key], TenantId, Language
    HAVING COUNT(1) > 1
)
BEGIN
    RAISERROR('MetadataDictionary duplicate keys detected for tenant rows ([Key], TenantId, Language). Resolve duplicates before migration.', 16, 1);
    RETURN;
END;

IF OBJECT_ID('dbo.MetadataDictionary_v4', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.MetadataDictionary_v4;
END;

CREATE TABLE dbo.MetadataDictionary_v4
(
    Id bigint IDENTITY(1,1) NOT NULL,
    [Key] nvarchar(200) NOT NULL,
    TenantId nvarchar(50) NULL,
    Language nvarchar(10) NOT NULL,
    DisplayName nvarchar(200) NOT NULL,
    Description nvarchar(2000) NULL,
    Unit nvarchar(50) NULL,
    Examples nvarchar(2000) NULL,
    UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MetadataDictionary_v4_UpdatedAtUtc DEFAULT sysutcdatetime(),
    CONSTRAINT PK_MetadataDictionary_v4_Id PRIMARY KEY (Id)
);

DECLARE @hasLanguage bit = CASE WHEN COL_LENGTH('dbo.MetadataDictionary', 'Language') IS NULL THEN 0 ELSE 1 END;
DECLARE @hasUpdatedAt bit = CASE WHEN COL_LENGTH('dbo.MetadataDictionary', 'UpdatedAtUtc') IS NULL THEN 0 ELSE 1 END;

DECLARE @sql nvarchar(max) = N'INSERT INTO dbo.MetadataDictionary_v4 ([Key], TenantId, Language, DisplayName, Description, Unit, Examples, UpdatedAtUtc) '
    + N'SELECT [Key], TenantId, '
    + CASE WHEN @hasLanguage = 1 THEN N'COALESCE(NULLIF(LTRIM(RTRIM(Language)), ''''), ''en'')' ELSE N'''en''' END
    + N', DisplayName, Description, Unit, Examples, '
    + CASE WHEN @hasUpdatedAt = 1 THEN N'COALESCE(UpdatedAtUtc, SYSUTCDATETIME())' ELSE N'SYSUTCDATETIME()' END
    + N' FROM dbo.MetadataDictionary;';

EXEC sp_executesql @sql;

DECLARE @suffix nvarchar(20) = REPLACE(REPLACE(CONVERT(varchar(16), SYSUTCDATETIME(), 120), '-', ''), ':', '');
DECLARE @legacyName nvarchar(128) = CONCAT('MetadataDictionary_legacy_', @suffix);

EXEC sp_rename 'dbo.MetadataDictionary', @legacyName;
EXEC sp_rename 'dbo.MetadataDictionary_v4', 'MetadataDictionary';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_MetadataDictionary_Global_Key_Lang' AND object_id = OBJECT_ID('dbo.MetadataDictionary'))
BEGIN
    CREATE UNIQUE INDEX UX_MetadataDictionary_Global_Key_Lang
        ON dbo.MetadataDictionary([Key], Language)
        WHERE TenantId IS NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_MetadataDictionary_Tenant_Key_Tenant_Lang' AND object_id = OBJECT_ID('dbo.MetadataDictionary'))
BEGIN
    CREATE UNIQUE INDEX UX_MetadataDictionary_Tenant_Key_Tenant_Lang
        ON dbo.MetadataDictionary([Key], TenantId, Language)
        WHERE TenantId IS NOT NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MetadataDictionary_Key_Lang' AND object_id = OBJECT_ID('dbo.MetadataDictionary'))
BEGIN
    CREATE INDEX IX_MetadataDictionary_Key_Lang
        ON dbo.MetadataDictionary([Key], Language);
END;
GO
