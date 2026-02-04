/*******************************************************************************
* TILSOFTAI Analytics Module - Internal Stored Procedures (app_*)
* Purpose: Platform-internal operations, not model-callable
*******************************************************************************/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Save TaskFrame for audit/debugging
CREATE OR ALTER PROCEDURE dbo.app_analytics_taskframe_save
    @TenantId           NVARCHAR(50),
    @ConversationId     NVARCHAR(100),
    @RequestId          NVARCHAR(100),
    @TaskType           NVARCHAR(50),
    @Entity             NVARCHAR(200) = NULL,
    @MetricsJson        NVARCHAR(MAX) = NULL,
    @FiltersJson        NVARCHAR(MAX) = NULL,
    @BreakdownsJson     NVARCHAR(MAX) = NULL,
    @TimeRangeHint      NVARCHAR(500) = NULL,
    @NeedsVisualization BIT = 0,
    @Confidence         DECIMAL(5,4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    IF @TenantId IS NULL OR @TenantId = ''
    BEGIN
        RAISERROR('TenantId is required.', 16, 1);
        RETURN;
    END
    
    INSERT INTO dbo.AnalyticsTaskFrame
    (
        TenantId, ConversationId, RequestId, TaskType, Entity,
        MetricsJson, FiltersJson, BreakdownsJson, TimeRangeHint,
        NeedsVisualization, Confidence
    )
    VALUES
    (
        @TenantId, @ConversationId, @RequestId, @TaskType, @Entity,
        @MetricsJson, @FiltersJson, @BreakdownsJson, @TimeRangeHint,
        @NeedsVisualization, @Confidence
    );
    
    SELECT SCOPE_IDENTITY() AS Id;
END;
GO

-- Save plan validation error
CREATE OR ALTER PROCEDURE dbo.app_analytics_planvalidationerror_save
    @TenantId       NVARCHAR(50),
    @RequestId      NVARCHAR(100),
    @ErrorCode      NVARCHAR(100),
    @ErrorMessage   NVARCHAR(2000) = NULL,
    @SuggestionsJson NVARCHAR(MAX) = NULL,
    @PlanJson       NVARCHAR(MAX) = NULL,
    @Retryable      BIT = 1,
    @RetryCount     INT = 0
AS
BEGIN
    SET NOCOUNT ON;
    
    IF @TenantId IS NULL OR @TenantId = ''
    BEGIN
        RAISERROR('TenantId is required.', 16, 1);
        RETURN;
    END
    
    INSERT INTO dbo.AnalyticsPlanValidationError
    (TenantId, RequestId, ErrorCode, ErrorMessage, SuggestionsJson, PlanJson, Retryable, RetryCount)
    VALUES
    (@TenantId, @RequestId, @ErrorCode, @ErrorMessage, @SuggestionsJson, @PlanJson, @Retryable, @RetryCount);
    
    SELECT SCOPE_IDENTITY() AS Id;
END;
GO

-- Get/set insight cache
CREATE OR ALTER PROCEDURE dbo.app_analytics_insightcache_get
    @TenantId   NVARCHAR(50),
    @QueryHash  NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    
    UPDATE dbo.AnalyticsInsightCache
    SET HitCount = HitCount + 1
    OUTPUT 
        inserted.InsightJson,
        inserted.HeadlineText,
        inserted.DataFreshnessUtc,
        inserted.HitCount
    WHERE TenantId = @TenantId
      AND QueryHash = @QueryHash
      AND ExpiresAtUtc > @Now;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_analytics_insightcache_set
    @TenantId           NVARCHAR(50),
    @QueryHash          NVARCHAR(64),
    @HeadlineText       NVARCHAR(1000) = NULL,
    @InsightJson        NVARCHAR(MAX),
    @DataFreshnessUtc   DATETIME2(3),
    @TtlSeconds         INT = 300
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ExpiresAtUtc DATETIME2(3) = DATEADD(SECOND, @TtlSeconds, SYSUTCDATETIME());
    
    MERGE dbo.AnalyticsInsightCache AS target
    USING (SELECT @TenantId AS TenantId, @QueryHash AS QueryHash) AS source
    ON target.TenantId = source.TenantId AND target.QueryHash = source.QueryHash
    WHEN MATCHED THEN
        UPDATE SET
            HeadlineText = @HeadlineText,
            InsightJson = @InsightJson,
            DataFreshnessUtc = @DataFreshnessUtc,
            ExpiresAtUtc = @ExpiresAtUtc,
            HitCount = 0
    WHEN NOT MATCHED THEN
        INSERT (TenantId, QueryHash, HeadlineText, InsightJson, DataFreshnessUtc, ExpiresAtUtc)
        VALUES (@TenantId, @QueryHash, @HeadlineText, @InsightJson, @DataFreshnessUtc, @ExpiresAtUtc);
END;
GO

-- Purge expired cache entries
CREATE OR ALTER PROCEDURE dbo.app_analytics_insightcache_purge
    @MaxAgeDays INT = 7
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM dbo.AnalyticsInsightCache
    WHERE ExpiresAtUtc < DATEADD(DAY, -@MaxAgeDays, SYSUTCDATETIME());
    
    SELECT @@ROWCOUNT AS DeletedCount;
END;
GO
