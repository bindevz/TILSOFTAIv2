SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.MetadataDictionary', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MetadataDictionary
    (
        Id bigint IDENTITY(1,1) NOT NULL,
        [Key] nvarchar(200) NOT NULL,
        TenantId nvarchar(50) NULL,
        Language nvarchar(10) NOT NULL,
        DisplayName nvarchar(200) NOT NULL,
        Description nvarchar(2000) NULL,
        Unit nvarchar(50) NULL,
        Examples nvarchar(2000) NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MetadataDictionary_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_MetadataDictionary_Id PRIMARY KEY (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MetadataDictionary_Key_Lang' AND object_id = OBJECT_ID('dbo.MetadataDictionary'))
BEGIN
    CREATE INDEX IX_MetadataDictionary_Key_Lang
        ON dbo.MetadataDictionary ([Key], Language);
END;
GO

IF OBJECT_ID('dbo.ToolCatalog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ToolCatalog
    (
        ToolName nvarchar(200) NOT NULL,
        SpName nvarchar(200) NOT NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_ToolCatalog_IsEnabled DEFAULT (1),
        RequiredRoles nvarchar(1000) NULL,
        JsonSchema nvarchar(max) NULL,
        Instruction nvarchar(max) NULL,
        Description nvarchar(2000) NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ToolCatalog_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_ToolCatalog PRIMARY KEY (ToolName),
        CONSTRAINT CK_ToolCatalog_SpNamePrefix CHECK (SpName LIKE 'ai[_]%')
    );
END;
GO

IF OBJECT_ID('dbo.ToolCatalogTranslation', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ToolCatalogTranslation
    (
        ToolName nvarchar(200) NOT NULL,
        Language nvarchar(10) NOT NULL,
        Instruction nvarchar(max) NULL,
        Description nvarchar(2000) NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ToolCatalogTranslation_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_ToolCatalogTranslation PRIMARY KEY (ToolName, Language),
        CONSTRAINT FK_ToolCatalogTranslation_ToolCatalog FOREIGN KEY (ToolName)
            REFERENCES dbo.ToolCatalog (ToolName)
    );
END;
GO

IF OBJECT_ID('dbo.v_Model_Overview', 'V') IS NULL
BEGIN
    EXEC ('CREATE VIEW dbo.v_Model_Overview AS SELECT CAST(NULL AS nvarchar(50)) AS ModelId, CAST(NULL AS nvarchar(200)) AS Name, CAST(NULL AS nvarchar(50)) AS TenantId WHERE 1 = 0;');
END;
GO
