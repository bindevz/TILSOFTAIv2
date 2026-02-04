SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @MajorVersion int = TRY_CONVERT(int, SERVERPROPERTY('ProductMajorVersion'));

IF @MajorVersion IS NULL OR @MajorVersion < 17
BEGIN
    PRINT 'SQL 2025 VECTOR type not available. Skipping SemanticCache procedures.';
    RETURN;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_semantic_cache_get
    @TenantId nvarchar(50),
    @Module nvarchar(100),
    @CacheKey nvarchar(128)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        Answer
    FROM dbo.SemanticCache
    WHERE TenantId = @TenantId
      AND Module = @Module
      AND CacheKey = @CacheKey;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_semantic_cache_put
    @TenantId nvarchar(50),
    @Module nvarchar(100),
    @CacheKey nvarchar(128),
    @QuestionHash nvarchar(64),
    @ToolsHash nvarchar(64) = NULL,
    @PlanHash nvarchar(64) = NULL,
    @Answer nvarchar(max),
    @Embedding vector(1536) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.SemanticCache AS target
    USING (SELECT @TenantId AS TenantId, @Module AS Module, @CacheKey AS CacheKey) AS src
    ON target.TenantId = src.TenantId
        AND target.Module = src.Module
        AND target.CacheKey = src.CacheKey
    WHEN MATCHED THEN
        UPDATE SET
            QuestionHash = @QuestionHash,
            ToolsHash = @ToolsHash,
            PlanHash = @PlanHash,
            Answer = @Answer,
            Embedding = @Embedding,
            UpdatedAtUtc = sysutcdatetime()
    WHEN NOT MATCHED THEN
        INSERT
        (
            TenantId,
            Module,
            CacheKey,
            QuestionHash,
            ToolsHash,
            PlanHash,
            Answer,
            Embedding
        )
        VALUES
        (
            @TenantId,
            @Module,
            @CacheKey,
            @QuestionHash,
            @ToolsHash,
            @PlanHash,
            @Answer,
            @Embedding
        );
END;
GO
