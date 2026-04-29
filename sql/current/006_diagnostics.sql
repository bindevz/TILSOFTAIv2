SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.app_model_runtime_diagnostics
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        DB_NAME() AS DatabaseName,
        OBJECT_ID('dbo.ai_model_count', 'P') AS AiModelCountObjectId,
        OBJECT_ID('dbo.ai_model_get_overview', 'P') AS AiModelOverviewObjectId,
        OBJECT_ID('dbo.ai_model_get_pieces', 'P') AS AiModelPiecesObjectId,
        OBJECT_ID('dbo.ai_model_get_materials', 'P') AS AiModelMaterialsObjectId,
        OBJECT_ID('dbo.ai_model_compare', 'P') AS AiModelCompareObjectId,
        OBJECT_ID('dbo.ai_model_get_packaging', 'P') AS AiModelPackagingObjectId,
        OBJECT_ID('dbo.ActionRequest', 'U') AS ActionRequestObjectId,
        (SELECT COUNT(1) FROM dbo.Model) AS ModelRowCount;
END;
GO

PRINT N'Model runtime diagnostics procedure is present.';
GO
