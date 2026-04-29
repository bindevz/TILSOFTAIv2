SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @Missing TABLE (ObjectName sysname NOT NULL);

IF DB_ID(DB_NAME()) IS NULL INSERT INTO @Missing VALUES (N'database');

INSERT INTO @Missing (ObjectName)
SELECT required.ObjectName
FROM (VALUES
    (N'dbo.ai_model_count', 'P'),
    (N'dbo.ai_model_get_overview', 'P'),
    (N'dbo.ai_model_get_pieces', 'P'),
    (N'dbo.ai_model_get_materials', 'P'),
    (N'dbo.ai_model_compare', 'P'),
    (N'dbo.ai_model_get_packaging', 'P'),
    (N'dbo.app_actionrequest_create', 'P'),
    (N'dbo.app_actionrequest_get', 'P'),
    (N'dbo.app_actionrequest_get_active_for_conversation', 'P'),
    (N'dbo.app_actionrequest_confirm', 'P'),
    (N'dbo.app_actionrequest_reject', 'P'),
    (N'dbo.app_actionrequest_mark_executed', 'P'),
    (N'dbo.app_actionrequest_expire_old', 'P'),
    (N'dbo.ActionRequest', 'U'),
    (N'dbo.Model', 'U'),
    (N'dbo.vw_ModelSemantic', 'V')
) AS required(ObjectName, ObjectType)
WHERE OBJECT_ID(required.ObjectName, required.ObjectType) IS NULL;

IF EXISTS (SELECT 1 FROM @Missing)
BEGIN
    DECLARE @MissingList nvarchar(max) =
        STUFF((SELECT N', ' + ObjectName FROM @Missing FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, N'');
    RAISERROR('Model runtime validation failed. Missing objects: %s', 16, 1, @MissingList);
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Model)
BEGIN
    RAISERROR('Model runtime validation failed. No model data exists. Use real ERP model data or the clearly marked optional test seed.', 16, 1);
    RETURN;
END;

DECLARE @CountResult TABLE (ResultJson nvarchar(max));
INSERT INTO @CountResult
EXEC dbo.ai_model_count @TenantId = N'demo', @ArgsJson = N'{}';

IF NOT EXISTS (SELECT 1 FROM @CountResult WHERE ISJSON(ResultJson) = 1)
BEGIN
    RAISERROR('Model runtime validation failed. dbo.ai_model_count did not return valid JSON.', 16, 1);
    RETURN;
END;

SELECT
    DB_NAME() AS databaseName,
    (SELECT COUNT(1) FROM dbo.Model) AS modelRowCount,
    N'passed' AS validationResult;
GO
