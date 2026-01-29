SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =========================================================================================
-- PATCH 15.07: SQL 2025 JSON and Vector Support
-- =========================================================================================

-- 1. JSON Type Migration
-- Only runs if native 'json' type is available (SQL Server 2025 / Azure SQL)
IF TYPE_ID('json') IS NOT NULL
BEGIN
    PRINT 'SQL Server 2025 JSON type detected. Attempting to migrate columns to native JSON...';

    BEGIN TRY
        -- Migrate ToolExecution.ArgumentsJson (Valid JSON expected)
        IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ToolExecution') AND name = 'ArgumentsJson' AND system_type_id <> TYPE_ID('json'))
        BEGIN
            EXEC sp_executesql N'ALTER TABLE dbo.ToolExecution ALTER COLUMN ArgumentsJson json NOT NULL';
            PRINT 'Migrated dbo.ToolExecution.ArgumentsJson to native JSON.';
        END

        -- Migrate ToolExecution.ResultJson
        IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ToolExecution') AND name = 'ResultJson' AND system_type_id <> TYPE_ID('json'))
        BEGIN
            EXEC sp_executesql N'ALTER TABLE dbo.ToolExecution ALTER COLUMN ResultJson json NULL';
            PRINT 'Migrated dbo.ToolExecution.ResultJson to native JSON.';
        END

        -- Migrate ToolExecution.CompactedResultJson
        IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ToolExecution') AND name = 'CompactedResultJson' AND system_type_id <> TYPE_ID('json'))
        BEGIN
            EXEC sp_executesql N'ALTER TABLE dbo.ToolExecution ALTER COLUMN CompactedResultJson json NULL';
            PRINT 'Migrated dbo.ToolExecution.CompactedResultJson to native JSON.';
        END

        -- Migrate ConversationMessage.Content (Maybe? Content is usually text, not always JSON. Skip for now to be safe.)
        -- Spec said "tool args/results, diagnostics".
        
        -- Migrate ErrorLog.DetailJson
        IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ErrorLog') AND name = 'DetailJson' AND system_type_id <> TYPE_ID('json'))
        BEGIN
            EXEC sp_executesql N'ALTER TABLE dbo.ErrorLog ALTER COLUMN DetailJson json NULL';
            PRINT 'Migrated dbo.ErrorLog.DetailJson to native JSON.';
        END
    END TRY
    BEGIN CATCH
        PRINT 'Error migrating to native JSON type. Keeping nvarchar(max). Error: ' + ERROR_MESSAGE();
    END CATCH
END
ELSE
BEGIN
    PRINT 'SQL Server 2025 JSON type NOT detected. Skipping JSON migration.';
END
GO

-- 2. Semantic Cache Vector Support
-- Only runs if native 'vector' type is available
IF TYPE_ID('vector') IS NOT NULL
BEGIN
    PRINT 'SQL Server 2025 VECTOR type detected. Setting up SemanticCacheVector...';

    IF OBJECT_ID('dbo.SemanticCacheVector', 'U') IS NULL
    BEGIN
        EXEC sp_executesql N'
        CREATE TABLE dbo.SemanticCacheVector
        (
            TenantId nvarchar(50) NOT NULL,
            Module nvarchar(50) NOT NULL,
            QuestionHash nchar(64) NOT NULL,
            ToolHash nchar(64) NULL,
            PlanHash nchar(64) NULL,
            QuestionText nvarchar(max) NOT NULL,
            AnswerText nvarchar(max) NOT NULL,
            Embedding vector(1536) NULL, -- Nullable to allow delayed embedding calculation if needed, though app ensures it
            CreatedAtUtc datetime2(3) NOT NULL DEFAULT sysutcdatetime(),
            CONSTRAINT PK_SemanticCacheVector PRIMARY KEY (TenantId, Module, QuestionHash)
        );
        
        CREATE INDEX IX_SemanticCacheVector_Tenant_Module 
            ON dbo.SemanticCacheVector(TenantId, Module);
        ';
        PRINT 'Created dbo.SemanticCacheVector table.';
    END

    -- SP: Upsert
    EXEC sp_executesql N'
    CREATE OR ALTER PROCEDURE dbo.app_semanticcache_upsert
        @TenantId nvarchar(50),
        @Module nvarchar(50),
        @QuestionHash nchar(64),
        @QuestionText nvarchar(max),
        @ToolHash nchar(64) = NULL,
        @PlanHash nchar(64) = NULL,
        @Answer nvarchar(max),
        @EmbeddingJson nvarchar(max) 
    AS
    BEGIN
        SET NOCOUNT ON;
        DECLARE @v vector(1536) = CAST(@EmbeddingJson AS vector(1536));
        
        MERGE dbo.SemanticCacheVector AS target
        USING (SELECT @TenantId, @Module, @QuestionHash) AS source (TenantId, Module, QuestionHash)
        ON (target.TenantId = source.TenantId AND target.Module = source.Module AND target.QuestionHash = source.QuestionHash)
        WHEN MATCHED THEN
            UPDATE SET 
                AnswerText = @Answer,
                ToolHash = @ToolHash,
                PlanHash = @PlanHash,
                QuestionText = @QuestionText,
                Embedding = @v,
                CreatedAtUtc = sysutcdatetime()
        WHEN NOT MATCHED THEN
            INSERT (TenantId, Module, QuestionHash, ToolHash, PlanHash, QuestionText, AnswerText, Embedding)
            VALUES (@TenantId, @Module, @QuestionHash, @ToolHash, @PlanHash, @QuestionText, @Answer, @v);
    END;
    ';
    PRINT 'Created/Updated dbo.app_semanticcache_upsert.';

    -- SP: Search
    EXEC sp_executesql N'
    CREATE OR ALTER PROCEDURE dbo.app_semanticcache_search
        @TenantId nvarchar(50),
        @Module nvarchar(50),
        @ToolHash nchar(64) = NULL,
        @PlanHash nchar(64) = NULL,
        @EmbeddingJson nvarchar(max),
        @TopK int = 1,
        @MinSimilarity float = 0.9
    AS
    BEGIN
        SET NOCOUNT ON;
        DECLARE @v vector(1536) = CAST(@EmbeddingJson AS vector(1536));
        
        -- logic: 1 - cosine_similarity = cosine_distance
        -- If we want similarity > 0.9, we want distance < 0.1
        DECLARE @MaxDistance float = 1.0 - @MinSimilarity;

        SELECT TOP(@TopK) AnswerText
        FROM dbo.SemanticCacheVector
        WHERE TenantId = @TenantId 
          AND Module = @Module
          AND (ISNULL(ToolHash, '''') = ISNULL(@ToolHash, ''''))
          AND (ISNULL(PlanHash, '''') = ISNULL(@PlanHash, ''''))
          AND VECTOR_DISTANCE(''cosine'', Embedding, @v) <= @MaxDistance
        ORDER BY VECTOR_DISTANCE(''cosine'', Embedding, @v) ASC;
    END;
    ';
    PRINT 'Created/Updated dbo.app_semanticcache_search.';
END
ELSE
BEGIN
    PRINT 'SQL Server 2025 VECTOR type NOT detected. Support for vector cache will be disabled.';
END
GO
