SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT N'Validating TILSOFTAI SQL schema contract.';

DECLARE @Failures TABLE
(
    Failure nvarchar(4000) NOT NULL
);

INSERT INTO @Failures (Failure)
SELECT N'Missing required object: ' + required.ObjectName + N' (' + required.ObjectType + N')'
FROM (VALUES
    (N'dbo.Model', N'U'),
    (N'dbo.ModelPiece', N'U'),
    (N'dbo.Material', N'U'),
    (N'dbo.ModelMaterial', N'U'),
    (N'dbo.ModelPackagingOption', N'U'),
    (N'dbo.ActionRequest', N'U'),
    (N'ai.ToolRoutingTrace', N'U'),
    (N'dbo.vw_ModelSemantic', N'V'),
    (N'dbo.ai_model_count', N'P'),
    (N'dbo.ai_model_get_overview', N'P'),
    (N'dbo.ai_model_get_pieces', N'P'),
    (N'dbo.ai_model_get_materials', N'P'),
    (N'dbo.ai_model_compare', N'P'),
    (N'dbo.ai_model_get_packaging', N'P'),
    (N'dbo.app_actionrequest_create', N'P'),
    (N'dbo.app_actionrequest_get', N'P'),
    (N'dbo.app_actionrequest_get_active_for_conversation', N'P'),
    (N'dbo.app_actionrequest_confirm', N'P'),
    (N'dbo.app_actionrequest_approve', N'P'),
    (N'dbo.app_actionrequest_reject', N'P'),
    (N'dbo.app_actionrequest_mark_executed', N'P'),
    (N'dbo.app_actionrequest_expire_old', N'P'),
    (N'dbo.app_agent_trace_recent', N'P'),
    (N'dbo.app_agent_trace_by_correlation', N'P'),
    (N'dbo.app_agent_trace_failures', N'P'),
    (N'dbo.app_model_runtime_health', N'P')
) AS required(ObjectName, ObjectType)
WHERE OBJECT_ID(required.ObjectName, required.ObjectType) IS NULL;

DECLARE @ExpectedColumns TABLE
(
    ObjectName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    TypeName sysname NOT NULL,
    MaxLength smallint NULL,
    [Precision] tinyint NULL,
    Scale tinyint NULL,
    IsNullable bit NULL
);

INSERT INTO @ExpectedColumns (ObjectName, ColumnName, TypeName, MaxLength, [Precision], Scale, IsNullable)
VALUES
    (N'dbo.Model', N'ModelId', N'int', 4, 10, 0, 0),
    (N'dbo.Model', N'TenantId', N'nvarchar', 100, 0, 0, 0),
    (N'dbo.Model', N'Language', N'nvarchar', 20, 0, 0, 0),
    (N'dbo.Model', N'ModelCode', N'nvarchar', 100, 0, 0, 0),
    (N'dbo.Model', N'Name', N'nvarchar', 400, 0, 0, 0),
    (N'dbo.Model', N'TotalCbm', N'decimal', 9, 18, 4, 0),
    (N'dbo.Model', N'TotalWeightKg', N'decimal', 9, 18, 4, 0),
    (N'dbo.Model', N'PieceCount', N'int', 4, 10, 0, 0),
    (N'dbo.ModelPiece', N'ModelPieceId', N'int', 4, 10, 0, 0),
    (N'dbo.ModelPiece', N'TenantId', N'nvarchar', 100, 0, 0, 0),
    (N'dbo.ModelPiece', N'ModelId', N'int', 4, 10, 0, 0),
    (N'dbo.ModelPiece', N'PieceName', N'nvarchar', 400, 0, 0, 0),
    (N'dbo.ModelPiece', N'Quantity', N'int', 4, 10, 0, 0),
    (N'dbo.Material', N'MaterialId', N'int', 4, 10, 0, 0),
    (N'dbo.Material', N'TenantId', N'nvarchar', 100, 0, 0, 0),
    (N'dbo.Material', N'MaterialCode', N'nvarchar', 100, 0, 0, 0),
    (N'dbo.Material', N'Name', N'nvarchar', 400, 0, 0, 0),
    (N'dbo.ModelMaterial', N'ModelMaterialId', N'int', 4, 10, 0, 0),
    (N'dbo.ModelMaterial', N'TenantId', N'nvarchar', 100, 0, 0, 0),
    (N'dbo.ModelMaterial', N'ModelId', N'int', 4, 10, 0, 0),
    (N'dbo.ModelMaterial', N'MaterialId', N'int', 4, 10, 0, 0),
    (N'dbo.ModelMaterial', N'Quantity', N'decimal', 9, 18, 4, 0),
    (N'dbo.ModelPackagingOption', N'PackagingOptionId', N'int', 4, 10, 0, 0),
    (N'dbo.ModelPackagingOption', N'TenantId', N'nvarchar', 100, 0, 0, 0),
    (N'dbo.ModelPackagingOption', N'ModelId', N'int', 4, 10, 0, 0),
    (N'dbo.ModelPackagingOption', N'OptionName', N'nvarchar', 400, 0, 0, 0),
    (N'dbo.ActionRequest', N'ActionId', N'nvarchar', 128, 0, 0, 0),
    (N'dbo.ActionRequest', N'TenantId', N'nvarchar', 100, 0, 0, 0),
    (N'dbo.ActionRequest', N'UserId', N'nvarchar', 100, 0, 0, 1),
    (N'dbo.ActionRequest', N'ConversationId', N'nvarchar', 128, 0, 0, 0),
    (N'dbo.ActionRequest', N'Status', N'nvarchar', 40, 0, 0, 0),
    (N'dbo.ActionRequest', N'CapabilityKey', N'nvarchar', 400, 0, 0, 1),
    (N'dbo.ActionRequest', N'FunctionName', N'nvarchar', 400, 0, 0, 1),
    (N'dbo.ActionRequest', N'ProposedToolName', N'nvarchar', 400, 0, 0, 0),
    (N'dbo.ActionRequest', N'ProposedSpName', N'nvarchar', 400, 0, 0, 0),
    (N'dbo.ActionRequest', N'ArgsJson', N'nvarchar', -1, 0, 0, 0),
    (N'ai.ToolRoutingTrace', N'TraceID', N'bigint', 8, 19, 0, 0),
    (N'ai.ToolRoutingTrace', N'CorrelationID', N'uniqueidentifier', 16, 0, 0, 0),
    (N'ai.ToolRoutingTrace', N'TenantID', N'nvarchar', 200, 0, 0, 0),
    (N'ai.ToolRoutingTrace', N'UserID', N'nvarchar', 200, 0, 0, 0),
    (N'ai.ToolRoutingTrace', N'CandidateToolsJson', N'nvarchar', -1, 0, 0, 1),
    (N'ai.ToolRoutingTrace', N'HardSignalsJson', N'nvarchar', -1, 0, 0, 1),
    (N'ai.ToolRoutingTrace', N'SelectedTool', N'nvarchar', 400, 0, 0, 1),
    (N'ai.ToolRoutingTrace', N'SelectedFunction', N'nvarchar', 400, 0, 0, 1),
    (N'ai.ToolRoutingTrace', N'ArgumentsJson', N'nvarchar', -1, 0, 0, 1),
    (N'ai.ToolRoutingTrace', N'AdapterType', N'nvarchar', 200, 0, 0, 1),
    (N'ai.ToolRoutingTrace', N'RowCount', N'int', 4, 10, 0, 1),
    (N'ai.ToolRoutingTrace', N'LatencyMs', N'int', 4, 10, 0, 1),
    (N'ai.ToolRoutingTrace', N'Success', N'bit', 1, 1, 0, 0),
    (N'ai.ToolRoutingTrace', N'ErrorCode', N'nvarchar', 200, 0, 0, 1),
    (N'ai.ToolRoutingTrace', N'CreatedAt', N'datetime2', 8, 27, 7, 0);

INSERT INTO @Failures (Failure)
SELECT N'Missing required column: ' + expected.ObjectName + N'.' + expected.ColumnName
FROM @ExpectedColumns expected
LEFT JOIN sys.columns c
    ON c.object_id = OBJECT_ID(expected.ObjectName)
   AND c.name = expected.ColumnName
WHERE c.column_id IS NULL;

INSERT INTO @Failures (Failure)
SELECT N'Column contract drift: ' + expected.ObjectName + N'.' + expected.ColumnName
    + N' expected ' + expected.TypeName
    + COALESCE(N' max_length=' + CONVERT(nvarchar(20), expected.MaxLength), N'')
    + COALESCE(N' precision=' + CONVERT(nvarchar(20), expected.[Precision]), N'')
    + COALESCE(N' scale=' + CONVERT(nvarchar(20), expected.Scale), N'')
    + COALESCE(N' nullable=' + CONVERT(nvarchar(20), expected.IsNullable), N'')
FROM @ExpectedColumns expected
JOIN sys.columns c
    ON c.object_id = OBJECT_ID(expected.ObjectName)
   AND c.name = expected.ColumnName
JOIN sys.types t
    ON t.user_type_id = c.user_type_id
WHERE t.name <> expected.TypeName
   OR (expected.MaxLength IS NOT NULL AND c.max_length <> expected.MaxLength)
   OR (expected.[Precision] IS NOT NULL AND c.[precision] <> expected.[Precision])
   OR (expected.Scale IS NOT NULL AND c.scale <> expected.Scale)
   OR (expected.IsNullable IS NOT NULL AND c.is_nullable <> expected.IsNullable);

DECLARE @ExpectedProcParams TABLE
(
    ProcName sysname NOT NULL,
    ParamName sysname NOT NULL,
    TypeName sysname NOT NULL,
    MaxLength smallint NULL,
    ParamOrder int NOT NULL
);

INSERT INTO @ExpectedProcParams (ProcName, ParamName, TypeName, MaxLength, ParamOrder)
VALUES
    (N'dbo.ai_model_count', N'@TenantId', N'nvarchar', 100, 1),
    (N'dbo.ai_model_count', N'@ArgsJson', N'nvarchar', -1, 2),
    (N'dbo.ai_model_get_overview', N'@TenantId', N'nvarchar', 100, 1),
    (N'dbo.ai_model_get_overview', N'@ArgsJson', N'nvarchar', -1, 2),
    (N'dbo.ai_model_get_pieces', N'@TenantId', N'nvarchar', 100, 1),
    (N'dbo.ai_model_get_pieces', N'@ArgsJson', N'nvarchar', -1, 2),
    (N'dbo.ai_model_get_materials', N'@TenantId', N'nvarchar', 100, 1),
    (N'dbo.ai_model_get_materials', N'@ArgsJson', N'nvarchar', -1, 2),
    (N'dbo.ai_model_compare', N'@TenantId', N'nvarchar', 100, 1),
    (N'dbo.ai_model_compare', N'@ArgsJson', N'nvarchar', -1, 2),
    (N'dbo.ai_model_get_packaging', N'@TenantId', N'nvarchar', 100, 1),
    (N'dbo.ai_model_get_packaging', N'@ArgsJson', N'nvarchar', -1, 2);

INSERT INTO @Failures (Failure)
SELECT N'Missing/changed proc parameter: ' + expected.ProcName + N' ' + expected.ParamName
FROM @ExpectedProcParams expected
LEFT JOIN sys.parameters p
    ON p.object_id = OBJECT_ID(expected.ProcName)
   AND p.name = expected.ParamName
   AND p.parameter_id = expected.ParamOrder
LEFT JOIN sys.types t
    ON t.user_type_id = p.user_type_id
WHERE p.parameter_id IS NULL
   OR t.name <> expected.TypeName
   OR (expected.MaxLength IS NOT NULL AND p.max_length <> expected.MaxLength);

IF EXISTS (SELECT 1 FROM @Failures)
BEGIN
    SELECT Failure FROM @Failures ORDER BY Failure;
    DECLARE @FailureCount int = (SELECT COUNT(1) FROM @Failures);
    RAISERROR('Schema contract validation failed with %d issue(s). See result set for details.', 16, 1, @FailureCount);
    RETURN;
END;

SELECT
    DB_NAME() AS databaseName,
    N'passed' AS schemaContractValidation,
    (SELECT COUNT(1) FROM @ExpectedColumns) AS checkedColumnCount,
    (SELECT COUNT(1) FROM @ExpectedProcParams) AS checkedProcedureParameterCount;
GO
