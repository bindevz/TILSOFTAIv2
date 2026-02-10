SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- PACK 4: Module Scope Infrastructure
-- Creates: ModuleCatalog, ToolCatalogScope, MetadataDictionaryScope
-- Purpose: Enable per-module tool and metadata scoping
-- Idempotent: Safe to re-run
-- =============================================

-- 1. ModuleCatalog: Registry of available modules with LLM-readable instructions
IF OBJECT_ID('dbo.ModuleCatalog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ModuleCatalog
    (
        ModuleKey       nvarchar(50)    NOT NULL,
        AppKey          nvarchar(50)    NOT NULL DEFAULT '',
        IsEnabled       bit             NOT NULL DEFAULT 1,
        Instruction     nvarchar(500)   NOT NULL,
        Priority        int             NOT NULL DEFAULT 100,
        TenantId        nvarchar(50)    NULL,
        Language        nvarchar(10)    NOT NULL DEFAULT 'en',
        CONSTRAINT PK_ModuleCatalog PRIMARY KEY (ModuleKey, AppKey, Language)
    );

    PRINT 'Created table: dbo.ModuleCatalog';
END;
GO

-- 2. ToolCatalogScope: Maps tools to modules (many-to-many)
IF OBJECT_ID('dbo.ToolCatalogScope', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ToolCatalogScope
    (
        ToolName    nvarchar(200)   NOT NULL,
        ModuleKey   nvarchar(50)    NOT NULL,
        AppKey      nvarchar(50)    NOT NULL DEFAULT '',
        TenantId    nvarchar(50)    NULL,
        IsEnabled   bit             NOT NULL DEFAULT 1,
        CONSTRAINT PK_ToolCatalogScope PRIMARY KEY (ToolName, ModuleKey, AppKey),
        CONSTRAINT FK_ToolCatalogScope_Tool FOREIGN KEY (ToolName)
            REFERENCES dbo.ToolCatalog (ToolName)
    );

    PRINT 'Created table: dbo.ToolCatalogScope';
END;
GO

-- 3. MetadataDictionaryScope: Maps metadata keys to modules
IF OBJECT_ID('dbo.MetadataDictionaryScope', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MetadataDictionaryScope
    (
        MetadataKey nvarchar(200)   NOT NULL,
        ModuleKey   nvarchar(50)    NOT NULL,
        AppKey      nvarchar(50)    NOT NULL DEFAULT '',
        TenantId    nvarchar(50)    NULL,
        IsEnabled   bit             NOT NULL DEFAULT 1,
        CONSTRAINT PK_MetadataDictionaryScope PRIMARY KEY (MetadataKey, ModuleKey, AppKey)
    );

    PRINT 'Created table: dbo.MetadataDictionaryScope';
END;
GO
