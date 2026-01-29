SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. ai_model_get_overview (Uses Adapter View)
IF OBJECT_ID('dbo.ai_model_get_overview', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_get_overview;
GO

CREATE PROCEDURE dbo.ai_model_get_overview
    @modelId int
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Read from Adapter View
    SELECT 
        TenantId,
        Language,
        ModelId,
        ModelCode,
        Name,
        Description,
        TotalCbm,
        TotalWeightKg,
        LoadabilityIndex,
        Qnt40HC,
        PieceCount,
        BoxInSet,
        PackagingName,
        CartonCbm,
        CartonWeightKg
    FROM dbo.vw_ModelSemantic
    WHERE ModelId = @modelId;
END;
GO

-- 2. ai_model_compare_models (Uses Adapter View)
IF OBJECT_ID('dbo.ai_model_compare_models', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_compare_models;
GO

CREATE PROCEDURE dbo.ai_model_compare_models
    @modelIdsJson nvarchar(max) -- Expecting JSON array of integers, or handled via list if supported? 
    -- The tool def says "modelIds": {"type":"array"}. 
    -- Application layer might pass comma separated string or JSON. 
    -- Standard TILSOFT pattern typically passes JSON or TVP. 
    -- Since SQL 2016+ supports OPENJSON, we'll assume JSON array input.
AS
BEGIN
    SET NOCOUNT ON;

    -- If the input is just comma separated numbers (legacy), handling might be needed. 
    -- But tools define it as JSON array.
    
    SELECT 
        v.ModelId,
        v.ModelCode,
        v.Name,
        v.TotalCbm,
        v.TotalWeightKg,
        v.LoadabilityIndex,
        v.Qnt40HC,
        v.PieceCount,
        v.BoxInSet,
        v.PackagingName
    FROM dbo.vw_ModelSemantic v
    WHERE v.ModelId IN (SELECT value FROM OPENJSON(@modelIdsJson));
END;
GO

-- 3. ai_model_get_pieces (Direct Table Access for Detail)
IF OBJECT_ID('dbo.ai_model_get_pieces', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_get_pieces;
GO

CREATE PROCEDURE dbo.ai_model_get_pieces
    @modelId int
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Recursive CTE for hierarchy if needed, but ModelPiece table is flat with ChildModelId?
    -- "Use ChildModelId to detect nested sets and recursively call...". 
    -- This SP just lists the immediate pieces. The LLM or Agent handles recursion via tool updates.
    -- Or does the SP recurse? "recursively call model_get_pieces for child model ids". 
    -- This implies the LLM does the recursion. The SP just checks one level.
    
    SELECT 
        mp.ModelPieceId,
        mp.TenantId,
        mp.ModelId,
        mp.PieceName,
        mp.Quantity,
        mp.ChildModelId,
        mp.Sequence
    FROM dbo.ModelPiece mp
    WHERE mp.ModelId = @modelId
    ORDER BY mp.Sequence;
END;
GO

-- 4. ai_model_get_materials
IF OBJECT_ID('dbo.ai_model_get_materials', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_get_materials;
GO

CREATE PROCEDURE dbo.ai_model_get_materials
    @modelId int
AS
BEGIN
    SET NOCOUNT ON;
    
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
    WHERE mm.ModelId = @modelId;
END;
GO

-- 5. ai_model_get_packaging
IF OBJECT_ID('dbo.ai_model_get_packaging', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_get_packaging;
GO

CREATE PROCEDURE dbo.ai_model_get_packaging
    @modelId int
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        PackagingOptionId,
        OptionName,
        PackagingType,
        UnitsPerCarton,
        CartonCbm,
        CartonWeightKg,
        LoadabilityIndex,
        Qnt40HC
    FROM dbo.ModelPackagingOption
    WHERE ModelId = @modelId;
END;
GO

-- 6. ai_model_count
IF OBJECT_ID('dbo.ai_model_count', 'P') IS NOT NULL DROP PROCEDURE dbo.ai_model_count;
GO

CREATE PROCEDURE dbo.ai_model_count
    @season nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Demo schema doesn't have Season column in Model table.
    -- We'll just return total count, ignoring season or returning "No Season" group.
    
    SELECT 
        'All' as Season,
        COUNT(*) as Count
    FROM dbo.Model;
END;
GO
