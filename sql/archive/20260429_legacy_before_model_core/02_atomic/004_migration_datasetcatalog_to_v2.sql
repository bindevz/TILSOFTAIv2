SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.DatasetCatalog', 'U') IS NULL
BEGIN
    RETURN;
END;

DECLARE @hasId bit = 0;
DECLARE @pkOnId bit = 0;

SELECT @hasId = CASE WHEN EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DatasetCatalog') AND name = 'Id'
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
        WHERE kc.parent_object_id = OBJECT_ID('dbo.DatasetCatalog')
          AND kc.type = 'PK'
        GROUP BY kc.name
        HAVING COUNT(1) = 1 AND MAX(CASE WHEN c.name = 'Id' THEN 1 ELSE 0 END) = 1
    ) THEN 1 ELSE 0 END;
END;

IF @hasId = 1 AND @pkOnId = 1
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DatasetCatalog_Global_DatasetKey' AND object_id = OBJECT_ID('dbo.DatasetCatalog'))
    BEGIN
        CREATE UNIQUE INDEX UX_DatasetCatalog_Global_DatasetKey
            ON dbo.DatasetCatalog(DatasetKey)
            WHERE TenantId IS NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DatasetCatalog_Tenant_DatasetKey_Tenant' AND object_id = OBJECT_ID('dbo.DatasetCatalog'))
    BEGIN
        CREATE UNIQUE INDEX UX_DatasetCatalog_Tenant_DatasetKey_Tenant
            ON dbo.DatasetCatalog(DatasetKey, TenantId)
            WHERE TenantId IS NOT NULL;
    END;

    RETURN;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.DatasetCatalog
    WHERE TenantId IS NULL
    GROUP BY DatasetKey
    HAVING COUNT(1) > 1
)
BEGIN
    RAISERROR('DatasetCatalog duplicate keys detected for global rows (DatasetKey). Resolve duplicates before migration.', 16, 1);
    RETURN;
END;

IF EXISTS (
    SELECT 1
    FROM dbo.DatasetCatalog
    WHERE TenantId IS NOT NULL
    GROUP BY DatasetKey, TenantId
    HAVING COUNT(1) > 1
)
BEGIN
    RAISERROR('DatasetCatalog duplicate keys detected for tenant rows (DatasetKey, TenantId). Resolve duplicates before migration.', 16, 1);
    RETURN;
END;

IF OBJECT_ID('dbo.DatasetCatalog_v2', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.DatasetCatalog_v2;
END;

CREATE TABLE dbo.DatasetCatalog_v2
(
    Id bigint IDENTITY(1,1) NOT NULL,
    DatasetKey nvarchar(200) NOT NULL,
    TenantId nvarchar(50) NULL,
    BaseObject nvarchar(200) NOT NULL,
    TimeColumn nvarchar(200) NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_DatasetCatalog_v2_IsEnabled DEFAULT (1),
    UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_DatasetCatalog_v2_UpdatedAtUtc DEFAULT sysutcdatetime(),
    CONSTRAINT PK_DatasetCatalog_v2_Id PRIMARY KEY (Id)
);

DECLARE @hasUpdatedAt bit = CASE WHEN COL_LENGTH('dbo.DatasetCatalog', 'UpdatedAtUtc') IS NULL THEN 0 ELSE 1 END;

DECLARE @sql nvarchar(max) = N'INSERT INTO dbo.DatasetCatalog_v2 (DatasetKey, TenantId, BaseObject, TimeColumn, IsEnabled, UpdatedAtUtc) '
    + N'SELECT DatasetKey, TenantId, BaseObject, TimeColumn, IsEnabled, '
    + CASE WHEN @hasUpdatedAt = 1 THEN N'COALESCE(UpdatedAtUtc, SYSUTCDATETIME())' ELSE N'SYSUTCDATETIME()' END
    + N' FROM dbo.DatasetCatalog;';

EXEC sp_executesql @sql;

DECLARE @suffix nvarchar(20) = REPLACE(REPLACE(CONVERT(varchar(16), SYSUTCDATETIME(), 120), '-', ''), ':', '');
DECLARE @legacyName nvarchar(128) = CONCAT('DatasetCatalog_legacy_', @suffix);

EXEC sp_rename 'dbo.DatasetCatalog', @legacyName;
EXEC sp_rename 'dbo.DatasetCatalog_v2', 'DatasetCatalog';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DatasetCatalog_Global_DatasetKey' AND object_id = OBJECT_ID('dbo.DatasetCatalog'))
BEGIN
    CREATE UNIQUE INDEX UX_DatasetCatalog_Global_DatasetKey
        ON dbo.DatasetCatalog(DatasetKey)
        WHERE TenantId IS NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DatasetCatalog_Tenant_DatasetKey_Tenant' AND object_id = OBJECT_ID('dbo.DatasetCatalog'))
BEGIN
    CREATE UNIQUE INDEX UX_DatasetCatalog_Tenant_DatasetKey_Tenant
        ON dbo.DatasetCatalog(DatasetKey, TenantId)
        WHERE TenantId IS NOT NULL;
END;
GO
