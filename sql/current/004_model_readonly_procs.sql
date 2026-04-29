SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_count
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CapabilityKey nvarchar(200) = N'model.count';
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @NormalizedArgsJson nvarchar(max) = COALESCE(NULLIF(@ArgsJson, N''), N'{}');

    IF ISJSON(@NormalizedArgsJson) <> 1
    BEGIN
        SELECT (
            SELECT
                JSON_QUERY((SELECT @CapabilityKey AS capabilityKey, @TenantId AS tenantId, 0 AS [rowCount], N'validation_error' AS [status], N'@ArgsJson must be valid JSON.' AS [message], @GeneratedAtUtc AS generatedAtUtc FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES)) AS meta,
                JSON_QUERY((SELECT N'Count' AS [name], N'Count' AS [label], N'int' AS [type] FOR JSON PATH, INCLUDE_NULL_VALUES)) AS columns,
                JSON_QUERY(N'[]') AS [rows]
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
        ) AS ResultJson;
        RETURN;
    END;

    DECLARE @RowCount int = (SELECT COUNT(1) FROM dbo.Model WHERE TenantId = @TenantId OR TenantId IS NULL);

    SELECT (
        SELECT
            JSON_QUERY((SELECT @CapabilityKey AS capabilityKey, @TenantId AS tenantId, @RowCount AS [rowCount], N'ok' AS [status], @GeneratedAtUtc AS generatedAtUtc FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES)) AS meta,
            JSON_QUERY((SELECT N'Count' AS [name], N'Count' AS [label], N'int' AS [type] FOR JSON PATH, INCLUDE_NULL_VALUES)) AS columns,
            JSON_QUERY((SELECT @RowCount AS [Count] FOR JSON PATH, INCLUDE_NULL_VALUES)) AS [rows]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
    ) AS ResultJson;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_get_overview
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CapabilityKey nvarchar(200) = N'model.overview.by-code';
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @NormalizedArgsJson nvarchar(max) = COALESCE(NULLIF(@ArgsJson, N''), N'{}');
    DECLARE @Status nvarchar(30) = N'ok';
    DECLARE @Message nvarchar(400) = NULL;
    DECLARE @RowCount int = 0;
    DECLARE @modelCode nvarchar(50) = NULL;
    DECLARE @modelId int = NULL;

    IF ISJSON(@NormalizedArgsJson) <> 1
    BEGIN
        SET @Status = N'validation_error';
        SET @Message = N'@ArgsJson must be valid JSON.';
    END
    ELSE
    BEGIN
        SET @modelCode = NULLIF(LTRIM(RTRIM(JSON_VALUE(@NormalizedArgsJson, N'$.modelCode'))), N'');
        IF @modelCode IS NULL
        BEGIN
            SET @Status = N'validation_error';
            SET @Message = N'modelCode is required.';
        END
        ELSE IF (
            SELECT COUNT(DISTINCT ModelId)
            FROM dbo.vw_ModelSemantic
            WHERE ModelCode = @modelCode
              AND (TenantId = @TenantId OR TenantId IS NULL)
        ) > 1
        BEGIN
            SET @Status = N'validation_error';
            SET @Message = N'modelCode is ambiguous.';
        END
        ELSE
        BEGIN
            SET @modelId = (
                SELECT MAX(ModelId)
                FROM dbo.vw_ModelSemantic
                WHERE ModelCode = @modelCode
                  AND (TenantId = @TenantId OR TenantId IS NULL)
            );
            SET @RowCount = (SELECT COUNT(1) FROM dbo.vw_ModelSemantic WHERE ModelId = @modelId AND (TenantId = @TenantId OR TenantId IS NULL));
            IF @RowCount = 0
            BEGIN
                SET @Status = N'no_data';
                SET @Message = N'Model was not found.';
            END;
        END;
    END;

    SELECT (
        SELECT
            JSON_QUERY((SELECT @CapabilityKey AS capabilityKey, @TenantId AS tenantId, @modelCode AS modelCode, @RowCount AS [rowCount], @Status AS [status], @Message AS [message], @GeneratedAtUtc AS generatedAtUtc FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES)) AS meta,
            JSON_QUERY((SELECT [name], [label], [type] FROM (VALUES
                (N'TenantId', N'Tenant', N'string'),
                (N'ModelId', N'Model ID', N'int'),
                (N'ModelCode', N'Model Code', N'string'),
                (N'Name', N'Name', N'string'),
                (N'Description', N'Description', N'string'),
                (N'TotalCbm', N'Total CBM', N'decimal'),
                (N'TotalWeightKg', N'Total Weight KG', N'decimal'),
                (N'LoadabilityIndex', N'Loadability Index', N'decimal'),
                (N'Qnt40HC', N'40HC Quantity', N'int'),
                (N'PieceCount', N'Piece Count', N'int'),
                (N'BoxInSet', N'Box In Set', N'int'),
                (N'PackagingName', N'Packaging Name', N'string'),
                (N'CartonCbm', N'Carton CBM', N'decimal'),
                (N'CartonWeightKg', N'Carton Weight KG', N'decimal')
            ) AS cols([name], [label], [type]) FOR JSON PATH, INCLUDE_NULL_VALUES)) AS columns,
            JSON_QUERY(COALESCE((SELECT TenantId, ModelId, ModelCode, Name, Description, TotalCbm, TotalWeightKg, LoadabilityIndex, Qnt40HC, PieceCount, BoxInSet, PackagingName, CartonCbm, CartonWeightKg FROM dbo.vw_ModelSemantic WHERE ModelId = @modelId AND (TenantId = @TenantId OR TenantId IS NULL) FOR JSON PATH, INCLUDE_NULL_VALUES), N'[]')) AS [rows]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
    ) AS ResultJson;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_get_pieces
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CapabilityKey nvarchar(200) = N'model.pieces.by-code';
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @NormalizedArgsJson nvarchar(max) = COALESCE(NULLIF(@ArgsJson, N''), N'{}');
    DECLARE @Status nvarchar(30) = N'ok';
    DECLARE @Message nvarchar(400) = NULL;
    DECLARE @RowCount int = 0;
    DECLARE @modelCode nvarchar(50) = NULL;
    DECLARE @modelId int = NULL;

    IF ISJSON(@NormalizedArgsJson) <> 1
    BEGIN
        SET @Status = N'validation_error';
        SET @Message = N'@ArgsJson must be valid JSON.';
    END
    ELSE
    BEGIN
        SET @modelCode = NULLIF(LTRIM(RTRIM(JSON_VALUE(@NormalizedArgsJson, N'$.modelCode'))), N'');
        IF @modelCode IS NULL
        BEGIN
            SET @Status = N'validation_error';
            SET @Message = N'modelCode is required.';
        END
        ELSE
        BEGIN
            SET @modelId = (SELECT MAX(ModelId) FROM dbo.vw_ModelSemantic WHERE ModelCode = @modelCode AND (TenantId = @TenantId OR TenantId IS NULL));
            IF @modelId IS NULL
            BEGIN
                SET @Status = N'no_data';
                SET @Message = N'Model was not found.';
            END
            ELSE
            BEGIN
                SET @RowCount = (SELECT COUNT(1) FROM dbo.ModelPiece WHERE ModelId = @modelId AND (TenantId = @TenantId OR TenantId IS NULL));
            END;
        END;
    END;

    SELECT (
        SELECT
            JSON_QUERY((SELECT @CapabilityKey AS capabilityKey, @TenantId AS tenantId, @modelCode AS modelCode, @RowCount AS [rowCount], @Status AS [status], @Message AS [message], @GeneratedAtUtc AS generatedAtUtc FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES)) AS meta,
            JSON_QUERY((SELECT [name], [label], [type] FROM (VALUES
                (N'ModelPieceId', N'Model Piece ID', N'int'),
                (N'ModelId', N'Model ID', N'int'),
                (N'PieceName', N'Piece Name', N'string'),
                (N'Quantity', N'Quantity', N'int'),
                (N'ChildModelId', N'Child Model ID', N'int'),
                (N'Sequence', N'Sequence', N'int')
            ) AS cols([name], [label], [type]) FOR JSON PATH, INCLUDE_NULL_VALUES)) AS columns,
            JSON_QUERY(COALESCE((SELECT ModelPieceId, ModelId, PieceName, Quantity, ChildModelId, Sequence FROM dbo.ModelPiece WHERE ModelId = @modelId AND (TenantId = @TenantId OR TenantId IS NULL) ORDER BY Sequence FOR JSON PATH, INCLUDE_NULL_VALUES), N'[]')) AS [rows]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
    ) AS ResultJson;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_get_materials
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CapabilityKey nvarchar(200) = N'model.materials.by-code';
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @NormalizedArgsJson nvarchar(max) = COALESCE(NULLIF(@ArgsJson, N''), N'{}');
    DECLARE @Status nvarchar(30) = N'ok';
    DECLARE @Message nvarchar(400) = NULL;
    DECLARE @RowCount int = 0;
    DECLARE @modelCode nvarchar(50) = NULL;
    DECLARE @modelId int = NULL;

    IF ISJSON(@NormalizedArgsJson) <> 1
    BEGIN
        SET @Status = N'validation_error';
        SET @Message = N'@ArgsJson must be valid JSON.';
    END
    ELSE
    BEGIN
        SET @modelCode = NULLIF(LTRIM(RTRIM(JSON_VALUE(@NormalizedArgsJson, N'$.modelCode'))), N'');
        IF @modelCode IS NULL
        BEGIN
            SET @Status = N'validation_error';
            SET @Message = N'modelCode is required.';
        END
        ELSE
        BEGIN
            SET @modelId = (SELECT MAX(ModelId) FROM dbo.vw_ModelSemantic WHERE ModelCode = @modelCode AND (TenantId = @TenantId OR TenantId IS NULL));
            IF @modelId IS NULL
            BEGIN
                SET @Status = N'no_data';
                SET @Message = N'Model was not found.';
            END
            ELSE
            BEGIN
                SET @RowCount = (SELECT COUNT(1) FROM dbo.ModelMaterial WHERE ModelId = @modelId AND (TenantId = @TenantId OR TenantId IS NULL));
            END;
        END;
    END;

    SELECT (
        SELECT
            JSON_QUERY((SELECT @CapabilityKey AS capabilityKey, @TenantId AS tenantId, @modelCode AS modelCode, @RowCount AS [rowCount], @Status AS [status], @Message AS [message], @GeneratedAtUtc AS generatedAtUtc FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES)) AS meta,
            JSON_QUERY((SELECT [name], [label], [type] FROM (VALUES
                (N'ModelMaterialId', N'Model Material ID', N'int'),
                (N'Section', N'Section', N'string'),
                (N'Quantity', N'Quantity', N'decimal'),
                (N'Unit', N'Unit', N'string'),
                (N'WeightKg', N'Weight KG', N'decimal'),
                (N'MaterialCode', N'Material Code', N'string'),
                (N'MaterialName', N'Material Name', N'string'),
                (N'Category', N'Category', N'string')
            ) AS cols([name], [label], [type]) FOR JSON PATH, INCLUDE_NULL_VALUES)) AS columns,
            JSON_QUERY(COALESCE((SELECT mm.ModelMaterialId, mm.Section, mm.Quantity, mm.Unit, mm.WeightKg, mat.MaterialCode, mat.Name AS MaterialName, mat.Category FROM dbo.ModelMaterial mm JOIN dbo.Material mat ON mat.MaterialId = mm.MaterialId WHERE mm.ModelId = @modelId AND (mm.TenantId = @TenantId OR mm.TenantId IS NULL) FOR JSON PATH, INCLUDE_NULL_VALUES), N'[]')) AS [rows]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
    ) AS ResultJson;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_get_packaging
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CapabilityKey nvarchar(200) = N'model.packaging.by-code';
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @NormalizedArgsJson nvarchar(max) = COALESCE(NULLIF(@ArgsJson, N''), N'{}');
    DECLARE @Status nvarchar(30) = N'ok';
    DECLARE @Message nvarchar(400) = NULL;
    DECLARE @RowCount int = 0;
    DECLARE @modelCode nvarchar(50) = NULL;
    DECLARE @modelId int = NULL;

    IF ISJSON(@NormalizedArgsJson) <> 1
    BEGIN
        SET @Status = N'validation_error';
        SET @Message = N'@ArgsJson must be valid JSON.';
    END
    ELSE
    BEGIN
        SET @modelCode = NULLIF(LTRIM(RTRIM(JSON_VALUE(@NormalizedArgsJson, N'$.modelCode'))), N'');
        IF @modelCode IS NULL
        BEGIN
            SET @Status = N'validation_error';
            SET @Message = N'modelCode is required.';
        END
        ELSE
        BEGIN
            SET @modelId = (SELECT MAX(ModelId) FROM dbo.vw_ModelSemantic WHERE ModelCode = @modelCode AND (TenantId = @TenantId OR TenantId IS NULL));
            IF @modelId IS NULL
            BEGIN
                SET @Status = N'no_data';
                SET @Message = N'Model was not found.';
            END
            ELSE
            BEGIN
                SET @RowCount = (SELECT COUNT(1) FROM dbo.ModelPackagingOption WHERE ModelId = @modelId AND (TenantId = @TenantId OR TenantId IS NULL));
            END;
        END;
    END;

    SELECT (
        SELECT
            JSON_QUERY((SELECT @CapabilityKey AS capabilityKey, @TenantId AS tenantId, @modelCode AS modelCode, @RowCount AS [rowCount], @Status AS [status], @Message AS [message], @GeneratedAtUtc AS generatedAtUtc FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES)) AS meta,
            JSON_QUERY((SELECT [name], [label], [type] FROM (VALUES
                (N'PackagingOptionId', N'Packaging Option ID', N'int'),
                (N'OptionName', N'Option Name', N'string'),
                (N'PackagingType', N'Packaging Type', N'string'),
                (N'UnitsPerCarton', N'Units Per Carton', N'int'),
                (N'CartonCbm', N'Carton CBM', N'decimal'),
                (N'CartonWeightKg', N'Carton Weight KG', N'decimal'),
                (N'LoadabilityIndex', N'Loadability Index', N'decimal'),
                (N'Qnt40HC', N'40HC Quantity', N'int')
            ) AS cols([name], [label], [type]) FOR JSON PATH, INCLUDE_NULL_VALUES)) AS columns,
            JSON_QUERY(COALESCE((SELECT PackagingOptionId, OptionName, PackagingType, UnitsPerCarton, CartonCbm, CartonWeightKg, LoadabilityIndex, Qnt40HC FROM dbo.ModelPackagingOption WHERE ModelId = @modelId AND (TenantId = @TenantId OR TenantId IS NULL) FOR JSON PATH, INCLUDE_NULL_VALUES), N'[]')) AS [rows]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
    ) AS ResultJson;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_compare
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CapabilityKey nvarchar(200) = N'model.compare';
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @NormalizedArgsJson nvarchar(max) = COALESCE(NULLIF(@ArgsJson, N''), N'{}');
    DECLARE @Status nvarchar(30) = N'ok';
    DECLARE @Message nvarchar(400) = NULL;
    DECLARE @RowCount int = 0;

    DECLARE @RequestedCodes TABLE (ModelCode nvarchar(50) NOT NULL PRIMARY KEY);

    IF ISJSON(@NormalizedArgsJson) <> 1
    BEGIN
        SET @Status = N'validation_error';
        SET @Message = N'@ArgsJson must be valid JSON.';
    END
    ELSE IF JSON_QUERY(@NormalizedArgsJson, N'$.modelCodes') IS NULL OR ISJSON(JSON_QUERY(@NormalizedArgsJson, N'$.modelCodes')) <> 1
    BEGIN
        SET @Status = N'validation_error';
        SET @Message = N'modelCodes must be a valid JSON array.';
    END
    ELSE
    BEGIN
        INSERT INTO @RequestedCodes (ModelCode)
        SELECT DISTINCT NULLIF(LTRIM(RTRIM([value])), N'')
        FROM OPENJSON(JSON_QUERY(@NormalizedArgsJson, N'$.modelCodes'))
        WHERE [type] IN (1, 2)
          AND NULLIF(LTRIM(RTRIM([value])), N'') IS NOT NULL;

        IF (SELECT COUNT(1) FROM @RequestedCodes) < 2
        BEGIN
            SET @Status = N'validation_error';
            SET @Message = N'modelCodes must contain at least two model codes.';
        END
        ELSE
        BEGIN
            SET @RowCount = (
                SELECT COUNT(1)
                FROM dbo.vw_ModelSemantic v
                JOIN @RequestedCodes requested ON requested.ModelCode = v.ModelCode
                WHERE v.TenantId = @TenantId OR v.TenantId IS NULL
            );
            IF @RowCount = 0
            BEGIN
                SET @Status = N'no_data';
                SET @Message = N'No requested models were found.';
            END;
        END;
    END;

    SELECT (
        SELECT
            JSON_QUERY((SELECT @CapabilityKey AS capabilityKey, @TenantId AS tenantId, @RowCount AS [rowCount], @Status AS [status], @Message AS [message], @GeneratedAtUtc AS generatedAtUtc FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES)) AS meta,
            JSON_QUERY((SELECT [name], [label], [type] FROM (VALUES
                (N'ModelId', N'Model ID', N'int'),
                (N'ModelCode', N'Model Code', N'string'),
                (N'Name', N'Name', N'string'),
                (N'TotalCbm', N'Total CBM', N'decimal'),
                (N'TotalWeightKg', N'Total Weight KG', N'decimal'),
                (N'LoadabilityIndex', N'Loadability Index', N'decimal'),
                (N'Qnt40HC', N'40HC Quantity', N'int'),
                (N'PieceCount', N'Piece Count', N'int'),
                (N'BoxInSet', N'Box In Set', N'int'),
                (N'PackagingName', N'Packaging Name', N'string')
            ) AS cols([name], [label], [type]) FOR JSON PATH, INCLUDE_NULL_VALUES)) AS columns,
            JSON_QUERY(COALESCE((SELECT v.ModelId, v.ModelCode, v.Name, v.TotalCbm, v.TotalWeightKg, v.LoadabilityIndex, v.Qnt40HC, v.PieceCount, v.BoxInSet, v.PackagingName FROM dbo.vw_ModelSemantic v JOIN @RequestedCodes requested ON requested.ModelCode = v.ModelCode WHERE v.TenantId = @TenantId OR v.TenantId IS NULL ORDER BY v.ModelCode FOR JSON PATH, INCLUDE_NULL_VALUES), N'[]')) AS [rows]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
    ) AS ResultJson;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_compare_models
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.ai_model_compare @TenantId = @TenantId, @ArgsJson = @ArgsJson;
END;
GO

PRINT N'Read-only model ai_model_* procedures are present with consistent JSON envelopes.';
GO
