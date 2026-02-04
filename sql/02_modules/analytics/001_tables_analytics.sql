/*******************************************************************************
* TILSOFTAI Analytics Module - Tables
* Purpose: Analytics workflow state và task frame persistence
* 
* IMPORTANT: All tables follow tenant isolation pattern
*******************************************************************************/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- TaskFrame persistence (optional, for audit/debugging)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AnalyticsTaskFrame' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.AnalyticsTaskFrame
    (
        Id                  BIGINT IDENTITY(1,1) NOT NULL,
        TenantId            NVARCHAR(50) NOT NULL,
        ConversationId      NVARCHAR(100) NOT NULL,
        RequestId           NVARCHAR(100) NOT NULL,
        TaskType            NVARCHAR(50) NOT NULL, -- analytics, lookup, explain, mixed
        Entity              NVARCHAR(200) NULL,
        MetricsJson         NVARCHAR(MAX) NULL,
        FiltersJson         NVARCHAR(MAX) NULL,
        BreakdownsJson      NVARCHAR(MAX) NULL,
        TimeRangeHint       NVARCHAR(500) NULL,
        NeedsVisualization  BIT NOT NULL DEFAULT 0,
        Confidence          DECIMAL(5,4) NULL,
        CreatedAtUtc        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_AnalyticsTaskFrame PRIMARY KEY CLUSTERED (Id)
    );
    
    CREATE NONCLUSTERED INDEX IX_AnalyticsTaskFrame_Tenant_Conv 
        ON dbo.AnalyticsTaskFrame (TenantId, ConversationId);
        
    CREATE NONCLUSTERED INDEX IX_AnalyticsTaskFrame_RequestId
        ON dbo.AnalyticsTaskFrame (RequestId);
END;
GO

-- Plan Validation Errors (for retry/learning)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AnalyticsPlanValidationError' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.AnalyticsPlanValidationError
    (
        Id                  BIGINT IDENTITY(1,1) NOT NULL,
        TenantId            NVARCHAR(50) NOT NULL,
        RequestId           NVARCHAR(100) NOT NULL,
        ErrorCode           NVARCHAR(100) NOT NULL,
        ErrorMessage        NVARCHAR(2000) NULL,
        SuggestionsJson     NVARCHAR(MAX) NULL,
        PlanJson            NVARCHAR(MAX) NULL,
        Retryable           BIT NOT NULL DEFAULT 1,
        RetryCount          INT NOT NULL DEFAULT 0,
        CreatedAtUtc        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_AnalyticsPlanValidationError PRIMARY KEY CLUSTERED (Id)
    );
    
    CREATE NONCLUSTERED INDEX IX_AnalyticsPlanValidationError_Request
        ON dbo.AnalyticsPlanValidationError (RequestId);
END;
GO

-- Insight Cache (semantic cache extension)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AnalyticsInsightCache' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.AnalyticsInsightCache
    (
        Id                  BIGINT IDENTITY(1,1) NOT NULL,
        TenantId            NVARCHAR(50) NOT NULL,
        QueryHash           NVARCHAR(64) NOT NULL, -- SHA256 of normalized query + plan
        HeadlineText        NVARCHAR(1000) NULL,
        InsightJson         NVARCHAR(MAX) NOT NULL,
        DataFreshnessUtc    DATETIME2(3) NOT NULL,
        ExpiresAtUtc        DATETIME2(3) NOT NULL,
        HitCount            INT NOT NULL DEFAULT 0,
        CreatedAtUtc        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_AnalyticsInsightCache PRIMARY KEY CLUSTERED (Id)
    );
    
    CREATE UNIQUE NONCLUSTERED INDEX UX_AnalyticsInsightCache_TenantQuery
        ON dbo.AnalyticsInsightCache (TenantId, QueryHash);
END;
GO
