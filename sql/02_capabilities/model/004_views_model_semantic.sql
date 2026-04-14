SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.vw_ModelSemantic', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ModelSemantic;
GO

IF OBJECT_ID('dbo.Model', 'U') IS NOT NULL
BEGIN
    EXEC('
    CREATE VIEW dbo.vw_ModelSemantic
    AS
    SELECT 
        m.TenantId,
        m.Language,
        m.ModelId,
        m.ModelCode,
        m.Name,
        m.Description,
        m.TotalCbm,
        m.TotalWeightKg,
        m.LoadabilityIndex,
        m.Qnt40HC,
        m.PieceCount,
        -- Default Packaging metrics (Adapter logic: Select first found option)
        p.PackagingOptionId,
        p.OptionName AS PackagingName,
        p.UnitsPerCarton AS BoxInSet,
        p.CartonCbm,
        p.CartonWeightKg
    FROM dbo.Model m
    LEFT JOIN (
        SELECT 
            *,
            ROW_NUMBER() OVER (PARTITION BY TenantId, ModelId ORDER BY PackagingOptionId) as Rn
        FROM dbo.ModelPackagingOption
    ) p ON m.TenantId = p.TenantId AND m.ModelId = p.ModelId AND p.Rn = 1
    ');
END
GO
