SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @MajorVersion int = TRY_CONVERT(int, SERVERPROPERTY('ProductMajorVersion'));

IF @MajorVersion IS NULL OR @MajorVersion < 17
BEGIN
    PRINT 'SQL 2025 VECTOR type not available. Skipping SemanticCache table creation.';
    RETURN;
END;

IF OBJECT_ID('dbo.SemanticCache', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SemanticCache
    (
        TenantId nvarchar(50) NOT NULL,
        Module nvarchar(100) NOT NULL,
        CacheKey nvarchar(128) NOT NULL,
        QuestionHash nvarchar(64) NOT NULL,
        ToolsHash nvarchar(64) NULL,
        PlanHash nvarchar(64) NULL,
        Answer nvarchar(max) NOT NULL,
        Embedding vector(1536) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SemanticCache_CreatedAtUtc DEFAULT sysutcdatetime(),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SemanticCache_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_SemanticCache PRIMARY KEY (TenantId, Module, CacheKey)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SemanticCache_Tenant_Key' AND object_id = OBJECT_ID('dbo.SemanticCache'))
BEGIN
    CREATE INDEX IX_SemanticCache_Tenant_Key
        ON dbo.SemanticCache (TenantId, CacheKey, UpdatedAtUtc);
END;
GO
