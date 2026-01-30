SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =========================================================================================
-- PATCH 16.05: SQL Server 2025 AI Embeddings for Semantic Cache
-- =========================================================================================
-- This script adds optional in-database embedding generation using AI_GENERATE_EMBEDDINGS.
-- Requires SQL Server 2025 with EXTERNAL MODEL configured.
-- Falls back gracefully to C# embeddings if AI function is unavailable.
-- =========================================================================================

-- Feature Detection: Check if AI_GENERATE_EMBEDDINGS function exists
-- Note: OBJECT_ID may not work for built-in functions, so we use TRY-CATCH approach
IF TYPE_ID('vector') IS NOT NULL
BEGIN
    PRINT 'SQL Server 2025 VECTOR type detected. Attempting to create AI embedding procedures...';

    BEGIN TRY
        -- =================================================================================
        -- Procedure: dbo.app_semanticcache_embed
        -- Purpose: Generate embeddings using SQL Server 2025 AI_GENERATE_EMBEDDINGS
        -- Returns: JSON array of embedding values compatible with vector(1536) casting
        -- =================================================================================
        EXEC sp_executesql N'
        CREATE OR ALTER PROCEDURE dbo.app_semanticcache_embed
            @ModelName nvarchar(255),
            @Text nvarchar(max)
        AS
        BEGIN
            SET NOCOUNT ON;
            
            DECLARE @EmbeddingJson nvarchar(max);
            
            BEGIN TRY
                -- Generate embedding using SQL Server 2025 AI function
                -- Note: This requires EXTERNAL MODEL to be configured
                SELECT @EmbeddingJson = AI_GENERATE_EMBEDDINGS(@ModelName, @Text);
                
                -- Return as JSON array (compatible with vector(1536) casting)
                SELECT @EmbeddingJson AS EmbeddingJson;
            END TRY
            BEGIN CATCH
                -- Return NULL on error (caller will handle fallback to C#)
                -- Common errors: EXTERNAL MODEL not found, permission denied, quota exceeded
                DECLARE @ErrorMsg nvarchar(max) = ERROR_MESSAGE();
                PRINT ''AI_GENERATE_EMBEDDINGS error: '' + @ErrorMsg;
                SELECT NULL AS EmbeddingJson;
            END CATCH
        END;
        ';
        PRINT 'Created dbo.app_semanticcache_embed procedure.';

        -- =================================================================================
        -- Procedure: dbo.app_semanticcache_upsert_v2
        -- Purpose: Upsert semantic cache entry with optional in-SQL embedding generation
        -- This is an enhanced version that can generate embeddings internally
        -- =================================================================================
        EXEC sp_executesql N'
        CREATE OR ALTER PROCEDURE dbo.app_semanticcache_upsert_v2
            @TenantId nvarchar(50),
            @Module nvarchar(50),
            @QuestionHash nchar(64),
            @QuestionText nvarchar(max),
            @ToolHash nchar(64) = NULL,
            @PlanHash nchar(64) = NULL,
            @Answer nvarchar(max),
            @ModelName nvarchar(255),
            @GenerateEmbedding bit = 1
        AS
        BEGIN
            SET NOCOUNT ON;
            
            DECLARE @EmbeddingJson nvarchar(max);
            DECLARE @v vector(1536);
            
            -- Generate embedding if requested
            IF @GenerateEmbedding = 1
            BEGIN
                BEGIN TRY
                    SELECT @EmbeddingJson = AI_GENERATE_EMBEDDINGS(@ModelName, @QuestionText);
                    SET @v = CAST(@EmbeddingJson AS vector(1536));
                END TRY
                BEGIN CATCH
                    -- If embedding fails, set to NULL (cache entry will have NULL embedding)
                    DECLARE @ErrorMsg nvarchar(max) = ERROR_MESSAGE();
                    PRINT ''Embedding generation failed: '' + @ErrorMsg;
                    SET @v = NULL;
                END CATCH
            END
            
            -- Upsert semantic cache entry
            MERGE dbo.SemanticCacheVector AS target
            USING (SELECT @TenantId, @Module, @QuestionHash) AS source (TenantId, Module, QuestionHash)
            ON (target.TenantId = source.TenantId AND target.Module = source.Module AND target.QuestionHash = source.QuestionHash)
            WHEN MATCHED THEN
                UPDATE SET 
                    AnswerText = @Answer,
                    ToolHash = @ToolHash,
                    PlanHash = @PlanHash,
                    QuestionText = @QuestionText,
                    Embedding = ISNULL(@v, target.Embedding),  -- Keep existing if new is NULL
                    CreatedAtUtc = sysutcdatetime()
            WHEN NOT MATCHED THEN
                INSERT (TenantId, Module, QuestionHash, ToolHash, PlanHash, QuestionText, AnswerText, Embedding)
                VALUES (@TenantId, @Module, @QuestionHash, @ToolHash, @PlanHash, @QuestionText, @Answer, @v);
        END;
        ';
        PRINT 'Created dbo.app_semanticcache_upsert_v2 procedure.';
        
        PRINT 'Successfully created SQL Server 2025 AI embedding procedures.';
        PRINT '';
        PRINT 'PREREQUISITES:';
        PRINT '  1. Create EXTERNAL MODEL in SQL Server 2025:';
        PRINT '     CREATE EXTERNAL MODEL [text-embedding-3-small]';
        PRINT '     FROM AZURE_OPENAI';
        PRINT '     WITH (endpoint = ''https://your-instance.openai.azure.com/'', key = ''your-key'');';
        PRINT '';
        PRINT '  2. Grant permissions:';
        PRINT '     GRANT EXECUTE ON EXTERNAL MODEL::[text-embedding-3-small] TO [your_user];';
        PRINT '';
    END TRY
    BEGIN CATCH
        -- If AI_GENERATE_EMBEDDINGS is not available, the dynamic SQL will fail
        DECLARE @ErrMsg nvarchar(max) = ERROR_MESSAGE();
        PRINT 'Could not create AI embedding procedures. AI_GENERATE_EMBEDDINGS may not be available.';
        PRINT 'Error: ' + @ErrMsg;
        PRINT 'SQL embeddings will be unavailable. C# embedding fallback will be used.';
    END CATCH
END
ELSE
BEGIN
    PRINT 'SQL Server 2025 VECTOR type NOT detected. Skipping AI embedding procedures.';
    PRINT 'This is expected on SQL Server versions prior to 2025.';
END
GO
