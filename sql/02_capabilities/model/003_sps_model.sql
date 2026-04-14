SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- Patch 26.01: Model Module SP Signature Fixes
-- All ai_model_* SPs now accept standard contract:
-- @TenantId nvarchar(50), @ArgsJson nvarchar(max)
-- =============================================

-- 1. ai_model_get_overview
IF OBJECT_ID('dbo.ai_model_get_overview', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_get_overview;
GO

CREATE PROCEDURE dbo.ai_model_get_overview
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Parse arguments from JSON
    IF ISJSON(@ArgsJson) <> 1
    BEGIN
        RAISERROR('@ArgsJson must be valid JSON.', 16, 1);
        RETURN;
    END
    
    DECLARE @modelId int = TRY_CONVERT(int, JSON_VALUE(@ArgsJson, '$.modelId'));
    
    IF @modelId IS NULL
    BEGIN
        RAISERROR('modelId is required.', 16, 1);
        RETURN;
    END
    
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    
    -- Return JSON response with meta/columns/rows
    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    (SELECT COUNT(1) FROM dbo.vw_ModelSemantic WHERE ModelId = @modelId) AS [rowCount]
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
            ),
            columns = (
                SELECT [name], [type], [descriptionKey]
                FROM (VALUES
                    ('TenantId', 'nvarchar(50)', NULL),
                    ('ModelId', 'int', 'model.id'),
                    ('ModelCode', 'nvarchar(50)', 'model.code'),
                    ('Name', 'nvarchar(200)', 'model.name'),
                    ('Description', 'nvarchar(max)', 'model.description'),
                    ('TotalCbm', 'decimal(18,6)', 'model.cbm'),
                    ('TotalWeightKg', 'decimal(18,6)', 'model.weight'),
                    ('LoadabilityIndex', 'decimal(10,4)', 'model.loadability'),
                    ('Qnt40HC', 'int', 'model.qnt40hc'),
                    ('PieceCount', 'int', 'model.pieceCount'),
                    ('BoxInSet', 'int', 'model.boxInSet'),
                    ('PackagingName', 'nvarchar(100)', 'model.packaging'),
                    ('CartonCbm', 'decimal(18,6)', 'model.cartonCbm'),
                    ('CartonWeightKg', 'decimal(18,6)', 'model.cartonWeight')
                ) AS cols([name], [type], [descriptionKey])
                FOR JSON PATH, INCLUDE_NULL_VALUES
            ),
            rows = (
                SELECT 
                    TenantId, ModelId, ModelCode, Name, Description,
                    TotalCbm, TotalWeightKg, LoadabilityIndex, Qnt40HC,
                    PieceCount, BoxInSet, PackagingName, CartonCbm, CartonWeightKg
                FROM dbo.vw_ModelSemantic
                WHERE ModelId = @modelId
                  AND (TenantId = @TenantId OR TenantId IS NULL)
                FOR JSON PATH, INCLUDE_NULL_VALUES
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

-- 2. ai_model_compare_models
IF OBJECT_ID('dbo.ai_model_compare_models', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_compare_models;
GO

CREATE PROCEDURE dbo.ai_model_compare_models
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    
    IF ISJSON(@ArgsJson) <> 1
    BEGIN
        RAISERROR('@ArgsJson must be valid JSON.', 16, 1);
        RETURN;
    END
    
    -- Extract modelIds array from ArgsJson
    DECLARE @modelIdsJson nvarchar(max) = JSON_QUERY(@ArgsJson, '$.modelIds');
    
    IF @modelIdsJson IS NULL OR ISJSON(@modelIdsJson) <> 1
    BEGIN
        RAISERROR('modelIds must be a valid JSON array.', 16, 1);
        RETURN;
    END
    
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @RowCount int = (
        SELECT COUNT(1) FROM dbo.vw_ModelSemantic v
        WHERE v.ModelId IN (SELECT TRY_CONVERT(int, value) FROM OPENJSON(@modelIdsJson))
          AND (v.TenantId = @TenantId OR v.TenantId IS NULL)
    );
    
    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS [rowCount]
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
            ),
            columns = (
                SELECT [name], [type], [descriptionKey]
                FROM (VALUES
                    ('ModelId', 'int', 'model.id'),
                    ('ModelCode', 'nvarchar(50)', 'model.code'),
                    ('Name', 'nvarchar(200)', 'model.name'),
                    ('TotalCbm', 'decimal(18,6)', 'model.cbm'),
                    ('TotalWeightKg', 'decimal(18,6)', 'model.weight'),
                    ('LoadabilityIndex', 'decimal(10,4)', 'model.loadability'),
                    ('Qnt40HC', 'int', 'model.qnt40hc'),
                    ('PieceCount', 'int', 'model.pieceCount'),
                    ('BoxInSet', 'int', 'model.boxInSet'),
                    ('PackagingName', 'nvarchar(100)', 'model.packaging')
                ) AS cols([name], [type], [descriptionKey])
                FOR JSON PATH, INCLUDE_NULL_VALUES
            ),
            rows = (
                SELECT 
                    ModelId, ModelCode, Name, TotalCbm, TotalWeightKg,
                    LoadabilityIndex, Qnt40HC, PieceCount, BoxInSet, PackagingName
                FROM dbo.vw_ModelSemantic v
                WHERE v.ModelId IN (SELECT TRY_CONVERT(int, value) FROM OPENJSON(@modelIdsJson))
                  AND (v.TenantId = @TenantId OR v.TenantId IS NULL)
                FOR JSON PATH, INCLUDE_NULL_VALUES
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

-- 3. ai_model_get_pieces
IF OBJECT_ID('dbo.ai_model_get_pieces', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_get_pieces;
GO

CREATE PROCEDURE dbo.ai_model_get_pieces
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    
    IF ISJSON(@ArgsJson) <> 1
    BEGIN
        RAISERROR('@ArgsJson must be valid JSON.', 16, 1);
        RETURN;
    END
    
    DECLARE @modelId int = TRY_CONVERT(int, JSON_VALUE(@ArgsJson, '$.modelId'));
    
    IF @modelId IS NULL
    BEGIN
        RAISERROR('modelId is required.', 16, 1);
        RETURN;
    END
    
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @RowCount int = (
        SELECT COUNT(1) FROM dbo.ModelPiece mp
        WHERE mp.ModelId = @modelId
          AND (mp.TenantId = @TenantId OR mp.TenantId IS NULL)
    );
    
    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS [rowCount]
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
            ),
            columns = (
                SELECT [name], [type], [descriptionKey]
                FROM (VALUES
                    ('ModelPieceId', 'int', NULL),
                    ('ModelId', 'int', 'model.id'),
                    ('PieceName', 'nvarchar(200)', 'piece.name'),
                    ('Quantity', 'int', 'piece.quantity'),
                    ('ChildModelId', 'int', 'piece.childModel'),
                    ('Sequence', 'int', 'piece.sequence')
                ) AS cols([name], [type], [descriptionKey])
                FOR JSON PATH, INCLUDE_NULL_VALUES
            ),
            rows = (
                SELECT 
                    ModelPieceId, ModelId, PieceName, Quantity, ChildModelId, Sequence
                FROM dbo.ModelPiece mp
                WHERE mp.ModelId = @modelId
                  AND (mp.TenantId = @TenantId OR mp.TenantId IS NULL)
                ORDER BY mp.Sequence
                FOR JSON PATH, INCLUDE_NULL_VALUES
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

-- 4. ai_model_get_materials
IF OBJECT_ID('dbo.ai_model_get_materials', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_get_materials;
GO

CREATE PROCEDURE dbo.ai_model_get_materials
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    
    IF ISJSON(@ArgsJson) <> 1
    BEGIN
        RAISERROR('@ArgsJson must be valid JSON.', 16, 1);
        RETURN;
    END
    
    DECLARE @modelId int = TRY_CONVERT(int, JSON_VALUE(@ArgsJson, '$.modelId'));
    
    IF @modelId IS NULL
    BEGIN
        RAISERROR('modelId is required.', 16, 1);
        RETURN;
    END
    
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @RowCount int = (
        SELECT COUNT(1) FROM dbo.ModelMaterial mm WHERE mm.ModelId = @modelId
    );
    
    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS [rowCount]
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
            ),
            columns = (
                SELECT [name], [type], [descriptionKey]
                FROM (VALUES
                    ('ModelMaterialId', 'int', NULL),
                    ('Section', 'nvarchar(100)', 'material.section'),
                    ('Quantity', 'decimal(18,4)', 'material.quantity'),
                    ('Unit', 'nvarchar(50)', 'material.unit'),
                    ('WeightKg', 'decimal(18,6)', 'material.weight'),
                    ('MaterialCode', 'nvarchar(50)', 'material.code'),
                    ('MaterialName', 'nvarchar(200)', 'material.name'),
                    ('Category', 'nvarchar(100)', 'material.category'),
                    ('DensityKgPerM3', 'decimal(18,4)', 'material.density')
                ) AS cols([name], [type], [descriptionKey])
                FOR JSON PATH, INCLUDE_NULL_VALUES
            ),
            rows = (
                SELECT 
                    mm.ModelMaterialId,
                    mm.Section,
                    mm.Quantity,
                    mm.Unit,
                    mm.WeightKg,
                    mat.MaterialCode,
                    mat.Name AS MaterialName,
                    mat.Category,
                    mat.DensityKgPerM3
                FROM dbo.ModelMaterial mm
                JOIN dbo.Material mat ON mm.MaterialId = mat.MaterialId
                WHERE mm.ModelId = @modelId
                FOR JSON PATH, INCLUDE_NULL_VALUES
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

-- 5. ai_model_get_packaging
IF OBJECT_ID('dbo.ai_model_get_packaging', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_get_packaging;
GO

CREATE PROCEDURE dbo.ai_model_get_packaging
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    
    IF ISJSON(@ArgsJson) <> 1
    BEGIN
        RAISERROR('@ArgsJson must be valid JSON.', 16, 1);
        RETURN;
    END
    
    DECLARE @modelId int = TRY_CONVERT(int, JSON_VALUE(@ArgsJson, '$.modelId'));
    
    IF @modelId IS NULL
    BEGIN
        RAISERROR('modelId is required.', 16, 1);
        RETURN;
    END
    
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @RowCount int = (
        SELECT COUNT(1) FROM dbo.ModelPackagingOption WHERE ModelId = @modelId
    );
    
    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS [rowCount]
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
            ),
            columns = (
                SELECT [name], [type], [descriptionKey]
                FROM (VALUES
                    ('PackagingOptionId', 'int', NULL),
                    ('OptionName', 'nvarchar(100)', 'packaging.optionName'),
                    ('PackagingType', 'nvarchar(50)', 'packaging.type'),
                    ('UnitsPerCarton', 'int', 'packaging.unitsPerCarton'),
                    ('CartonCbm', 'decimal(18,6)', 'packaging.cartonCbm'),
                    ('CartonWeightKg', 'decimal(18,6)', 'packaging.cartonWeight'),
                    ('LoadabilityIndex', 'decimal(10,4)', 'packaging.loadability'),
                    ('Qnt40HC', 'int', 'packaging.qnt40hc')
                ) AS cols([name], [type], [descriptionKey])
                FOR JSON PATH, INCLUDE_NULL_VALUES
            ),
            rows = (
                SELECT 
                    PackagingOptionId, OptionName, PackagingType,
                    UnitsPerCarton, CartonCbm, CartonWeightKg,
                    LoadabilityIndex, Qnt40HC
                FROM dbo.ModelPackagingOption
                WHERE ModelId = @modelId
                FOR JSON PATH, INCLUDE_NULL_VALUES
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

-- 6. ai_model_count
IF OBJECT_ID('dbo.ai_model_count', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_count;
GO

CREATE PROCEDURE dbo.ai_model_count
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- @ArgsJson may contain optional "season" filter
    DECLARE @season nvarchar(50) = NULL;
    
    IF ISJSON(@ArgsJson) = 1
    BEGIN
        SET @season = JSON_VALUE(@ArgsJson, '$.season');
    END
    
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    
    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    1 AS [rowCount]
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
            ),
            columns = (
                SELECT [name], [type], [descriptionKey]
                FROM (VALUES
                    ('Season', 'nvarchar(50)', 'model.season'),
                    ('Count', 'int', 'model.count')
                ) AS cols([name], [type], [descriptionKey])
                FOR JSON PATH, INCLUDE_NULL_VALUES
            ),
            rows = (
                SELECT 
                    ISNULL(@season, 'All') as Season,
                    COUNT(*) as [Count]
                FROM dbo.Model m
                WHERE (m.TenantId = @TenantId OR m.TenantId IS NULL)
                -- Season filter would go here if Model table had Season column
                FOR JSON PATH, INCLUDE_NULL_VALUES
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO
