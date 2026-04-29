SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'ai') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA ai');
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_agent_trace_recent
    @TenantId nvarchar(100) = NULL,
    @Top int = 50
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Limit int = CASE WHEN @Top IS NULL OR @Top < 1 THEN 50 WHEN @Top > 500 THEN 500 ELSE @Top END;

    SELECT TOP (@Limit)
        t.TraceID,
        t.CorrelationID,
        t.TenantID,
        t.UserID,
        t.Locale,
        t.SelectedTool,
        t.SelectedFunction,
        COALESCE(c.StoredProcedure, t.SelectedFunction) AS ProcedureName,
        t.CandidateDomainCount,
        t.CandidateCapabilityCount,
        t.AdvertisedToolCount,
        t.CandidateDomainsJson,
        t.CandidateToolsJson,
        t.HardSignalsJson,
        t.AdvertisedFunctionToolsJson,
        t.ValidationResultJson,
        t.AdapterType,
        t.[RowCount],
        t.AnswerMode,
        t.LatencyMs AS DurationMs,
        t.LatencyByStageJson,
        t.ModelProvider,
        t.Success,
        t.ErrorCode,
        t.CreatedAt
    FROM ai.ToolRoutingTrace t
    LEFT JOIN ai.Capability c
        ON c.CapabilityKey = t.SelectedTool
        OR c.FunctionName = t.SelectedFunction
    WHERE @TenantId IS NULL OR t.TenantID = @TenantId
    ORDER BY t.CreatedAt DESC, t.TraceID DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_agent_trace_by_correlation
    @CorrelationId nvarchar(100),
    @TenantId nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CorrelationGuid uniqueidentifier = TRY_CONVERT(uniqueidentifier, @CorrelationId);

    IF @CorrelationGuid IS NULL
    BEGIN
        RAISERROR('@CorrelationId must be a valid uniqueidentifier.', 16, 1);
        RETURN;
    END;

    SELECT
        t.TraceID,
        t.CorrelationID,
        t.TenantID,
        t.UserID,
        t.Locale,
        t.SelectedTool,
        t.SelectedFunction,
        COALESCE(c.StoredProcedure, t.SelectedFunction) AS ProcedureName,
        t.CandidateDomainCount,
        t.CandidateCapabilityCount,
        t.AdvertisedToolCount,
        t.CandidateDomainsJson,
        t.CandidateToolsJson,
        t.HardSignalsJson,
        t.AdvertisedFunctionToolsJson,
        t.ValidationResultJson,
        t.AdapterType,
        t.[RowCount],
        t.AnswerMode,
        t.LatencyMs AS DurationMs,
        t.LatencyByStageJson,
        t.ModelProvider,
        t.Success,
        t.ErrorCode,
        t.CreatedAt
    FROM ai.ToolRoutingTrace t
    LEFT JOIN ai.Capability c
        ON c.CapabilityKey = t.SelectedTool
        OR c.FunctionName = t.SelectedFunction
    WHERE t.CorrelationID = @CorrelationGuid
      AND (@TenantId IS NULL OR t.TenantID = @TenantId)
    ORDER BY t.CreatedAt DESC, t.TraceID DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_agent_trace_failures
    @TenantId nvarchar(100) = NULL,
    @Top int = 100
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Limit int = CASE WHEN @Top IS NULL OR @Top < 1 THEN 100 WHEN @Top > 500 THEN 500 ELSE @Top END;

    SELECT TOP (@Limit)
        t.TraceID,
        t.CorrelationID,
        t.TenantID,
        t.UserID,
        t.Locale,
        t.SelectedTool,
        t.SelectedFunction,
        COALESCE(c.StoredProcedure, t.SelectedFunction) AS ProcedureName,
        t.CandidateDomainCount,
        t.CandidateCapabilityCount,
        t.AdvertisedToolCount,
        t.CandidateDomainsJson,
        t.CandidateToolsJson,
        t.HardSignalsJson,
        t.ValidationResultJson,
        t.AdapterType,
        t.[RowCount],
        t.LatencyMs AS DurationMs,
        t.LatencyByStageJson,
        t.ModelProvider,
        t.ErrorCode,
        t.CreatedAt
    FROM ai.ToolRoutingTrace t
    LEFT JOIN ai.Capability c
        ON c.CapabilityKey = t.SelectedTool
        OR c.FunctionName = t.SelectedFunction
    WHERE t.Success = 0
      AND (@TenantId IS NULL OR t.TenantID = @TenantId)
    ORDER BY t.CreatedAt DESC, t.TraceID DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_model_runtime_health
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        DB_NAME() AS DatabaseName,
        OBJECT_ID(N'dbo.ai_model_count', N'P') AS AiModelCountObjectId,
        OBJECT_ID(N'dbo.ai_model_get_overview', N'P') AS AiModelOverviewObjectId,
        OBJECT_ID(N'dbo.ai_model_get_pieces', N'P') AS AiModelPiecesObjectId,
        OBJECT_ID(N'dbo.ai_model_get_materials', N'P') AS AiModelMaterialsObjectId,
        OBJECT_ID(N'dbo.ai_model_compare', N'P') AS AiModelCompareObjectId,
        OBJECT_ID(N'dbo.ai_model_get_packaging', N'P') AS AiModelPackagingObjectId,
        OBJECT_ID(N'dbo.ActionRequest', N'U') AS ActionRequestObjectId,
        OBJECT_ID(N'ai.ToolRoutingTrace', N'U') AS ToolRoutingTraceObjectId,
        (SELECT COUNT(1) FROM dbo.Model) AS ModelRowCount,
        CASE WHEN OBJECT_ID(N'ai.ToolRoutingTrace', N'U') IS NULL THEN NULL ELSE (SELECT COUNT(1) FROM ai.ToolRoutingTrace) END AS ToolRoutingTraceRowCount;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_model_runtime_diagnostics
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.app_model_runtime_health;
END;
GO

PRINT N'Model runtime diagnostics and agent trace query procedures are present. Sensitive row payloads are not projected.';
GO
