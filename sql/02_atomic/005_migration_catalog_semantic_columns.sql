/*******************************************************************************
* TILSOFTAI Patch 28 - Migration: Add semantic columns to catalog tables
* 
* Purpose: Fix schema mismatch between catalog tables and analytics views/SPs
* Dependencies: sql/02_atomic/001_tables_catalog.sql
* 
* CRITICAL: Idempotent - safe to run multiple times
*******************************************************************************/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT 'Patch 28 - Phase 1: Adding semantic columns to DatasetCatalog...';
GO

-- DatasetCatalog: Add DisplayName
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DatasetCatalog') AND name = 'DisplayName')
BEGIN
    ALTER TABLE dbo.DatasetCatalog ADD DisplayName NVARCHAR(500) NULL;
    PRINT '  Added DatasetCatalog.DisplayName';
END
GO

-- DatasetCatalog: Add Description
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DatasetCatalog') AND name = 'Description')
BEGIN
    ALTER TABLE dbo.DatasetCatalog ADD Description NVARCHAR(2000) NULL;
    PRINT '  Added DatasetCatalog.Description';
END
GO

-- DatasetCatalog: Add Grain
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DatasetCatalog') AND name = 'Grain')
BEGIN
    ALTER TABLE dbo.DatasetCatalog ADD Grain NVARCHAR(200) NULL;
    PRINT '  Added DatasetCatalog.Grain';
END
GO

-- DatasetCatalog: Add Tags
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DatasetCatalog') AND name = 'Tags')
BEGIN
    ALTER TABLE dbo.DatasetCatalog ADD Tags NVARCHAR(1000) NULL;
    PRINT '  Added DatasetCatalog.Tags';
END
GO

-- DatasetCatalog: Add CreatedAtUtc
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DatasetCatalog') AND name = 'CreatedAtUtc')
BEGIN
    ALTER TABLE dbo.DatasetCatalog ADD CreatedAtUtc DATETIME2(3) NOT NULL 
        CONSTRAINT DF_DatasetCatalog_CreatedAtUtc DEFAULT SYSUTCDATETIME();
    PRINT '  Added DatasetCatalog.CreatedAtUtc';
END
GO

PRINT 'Patch 28 - Phase 1: Adding semantic columns to FieldCatalog...';
GO

-- FieldCatalog: Add Id (identity for views)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FieldCatalog') AND name = 'Id')
BEGIN
    -- Cannot add IDENTITY to existing table easily, use computed column workaround
    -- For views, we'll use ROW_NUMBER() instead. Skip this.
    PRINT '  FieldCatalog.Id skipped - will use ROW_NUMBER in views';
END
GO

-- FieldCatalog: Add DisplayName
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FieldCatalog') AND name = 'DisplayName')
BEGIN
    ALTER TABLE dbo.FieldCatalog ADD DisplayName NVARCHAR(500) NULL;
    PRINT '  Added FieldCatalog.DisplayName';
END
GO

-- FieldCatalog: Add Description
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FieldCatalog') AND name = 'Description')
BEGIN
    ALTER TABLE dbo.FieldCatalog ADD Description NVARCHAR(2000) NULL;
    PRINT '  Added FieldCatalog.Description';
END
GO

-- FieldCatalog: Add SemanticType
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FieldCatalog') AND name = 'SemanticType')
BEGIN
    ALTER TABLE dbo.FieldCatalog ADD SemanticType NVARCHAR(100) NULL;
    PRINT '  Added FieldCatalog.SemanticType';
END
GO

-- FieldCatalog: Add SecurityTag
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FieldCatalog') AND name = 'SecurityTag')
BEGIN
    ALTER TABLE dbo.FieldCatalog ADD SecurityTag NVARCHAR(50) NULL;
    PRINT '  Added FieldCatalog.SecurityTag';
END
GO

PRINT 'Patch 28 - Phase 1: Fixing EntityGraphCatalog schema...';
GO

-- EntityGraphCatalog: Add SourceDatasetKey (alias for FromDatasetKey)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.EntityGraphCatalog') AND name = 'SourceDatasetKey')
BEGIN
    ALTER TABLE dbo.EntityGraphCatalog ADD SourceDatasetKey AS FromDatasetKey PERSISTED;
    PRINT '  Added EntityGraphCatalog.SourceDatasetKey (computed from FromDatasetKey)';
END
GO

-- EntityGraphCatalog: Add TargetDatasetKey (alias for ToDatasetKey)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.EntityGraphCatalog') AND name = 'TargetDatasetKey')
BEGIN
    ALTER TABLE dbo.EntityGraphCatalog ADD TargetDatasetKey AS ToDatasetKey PERSISTED;
    PRINT '  Added EntityGraphCatalog.TargetDatasetKey (computed from ToDatasetKey)';
END
GO

-- EntityGraphCatalog: Add SourceFields
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.EntityGraphCatalog') AND name = 'SourceFields')
BEGIN
    ALTER TABLE dbo.EntityGraphCatalog ADD SourceFields NVARCHAR(500) NULL;
    PRINT '  Added EntityGraphCatalog.SourceFields';
END
GO

-- EntityGraphCatalog: Add TargetFields
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.EntityGraphCatalog') AND name = 'TargetFields')
BEGIN
    ALTER TABLE dbo.EntityGraphCatalog ADD TargetFields NVARCHAR(500) NULL;
    PRINT '  Added EntityGraphCatalog.TargetFields';
END
GO

PRINT 'Patch 28 - Phase 1: Schema migration complete.';
GO
