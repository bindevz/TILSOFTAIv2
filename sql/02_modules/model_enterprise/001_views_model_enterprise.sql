SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER VIEW dbo.v_Model_Overview
AS
SELECT
    CAST(m.ClientID AS nvarchar(50)) AS TenantId,
    m.ModelID AS ModelId,
    m.ModelUD AS ModelCode,
    m.ModelNM AS Name,
    m.Season,
    ISNULL(p.PieceCount, 0) AS PieceCount,
    pkg.Cbm AS DefaultCbm,
    pkg.Qnt40HC,
    pkg.BoxInSet,
    CAST(CASE WHEN flags.HasFsc = 1 THEN 1 ELSE 0 END AS bit) AS HasFsc,
    CAST(CASE WHEN flags.HasRcs = 1 THEN 1 ELSE 0 END AS bit) AS HasRcs
FROM dbo.Model AS m
LEFT JOIN (
    SELECT
        ModelID,
        COUNT(1) AS PieceCount
    FROM dbo.ModelPiece
    GROUP BY ModelID
) AS p
    ON p.ModelID = m.ModelID
OUTER APPLY (
    SELECT TOP (1)
        TRY_CONVERT(decimal(18,4), NULLIF(LTRIM(RTRIM(CBM)), '')) AS Cbm,
        TRY_CONVERT(int, Qnt40HC) AS Qnt40HC,
        TRY_CONVERT(int, BoxInSet) AS BoxInSet
    FROM dbo.ModelPackagingMethodOption
    WHERE ModelID = m.ModelID
    ORDER BY
        CASE WHEN IsDefault = 1 THEN 0 ELSE 1 END,
        UpdatedDate DESC,
        ModelPackagingMethodOptionID DESC
) AS pkg
LEFT JOIN (
    SELECT
        mmc.ModelID,
        MAX(CASE WHEN pws.IsFSCEnabled = 1 THEN 1 ELSE 0 END) AS HasFsc,
        MAX(CASE WHEN pws.IsRCSEnabled = 1 THEN 1 ELSE 0 END) AS HasRcs
    FROM dbo.ModelMaterialConfig AS mmc
    INNER JOIN dbo.ProductWizardSection AS pws
        ON pws.ProductWizardSectionID = mmc.ProductWizardSectionID
    GROUP BY mmc.ModelID
) AS flags
    ON flags.ModelID = m.ModelID;
GO

CREATE OR ALTER VIEW dbo.v_Model_Pieces
AS
SELECT
    CAST(parent.ClientID AS nvarchar(50)) AS TenantId,
    mp.ModelPieceID AS ModelPieceId,
    mp.ModelID AS ParentModelId,
    mp.PieceModelID AS ChildModelId,
    child.ModelUD AS ChildModelCode,
    child.ModelNM AS ChildModelName,
    mp.Quantity,
    mp.RowIndex
FROM dbo.ModelPiece AS mp
INNER JOIN dbo.Model AS parent
    ON parent.ModelID = mp.ModelID
LEFT JOIN dbo.Model AS child
    ON child.ModelID = mp.PieceModelID;
GO

CREATE OR ALTER VIEW dbo.v_Model_Packaging_Default
AS
SELECT
    CAST(m.ClientID AS nvarchar(50)) AS TenantId,
    m.ModelID AS ModelId,
    pkg.MethodCode,
    TRY_CONVERT(int, pkg.BoxInSet) AS BoxInSet,
    TRY_CONVERT(decimal(18,4), NULLIF(LTRIM(RTRIM(pkg.CBM)), '')) AS Cbm,
    TRY_CONVERT(int, pkg.Qnt20DC) AS Qnt20DC,
    TRY_CONVERT(int, pkg.Qnt40DC) AS Qnt40DC,
    TRY_CONVERT(int, pkg.Qnt40HC) AS Qnt40HC,
    TRY_CONVERT(decimal(18,4), NULLIF(LTRIM(RTRIM(pkg.NetWeight)), '')) AS NetWeight,
    TRY_CONVERT(decimal(18,4), NULLIF(LTRIM(RTRIM(pkg.GrossWeight)), '')) AS GrossWeight,
    pkg.CartonBoxDimL,
    pkg.CartonBoxDimW,
    pkg.CartonBoxDimH,
    pkg.PackingRemark,
    pkg.PackagingMethodDescription
FROM dbo.Model AS m
OUTER APPLY (
    SELECT TOP (1) *
    FROM dbo.ModelPackagingMethodOption
    WHERE ModelID = m.ModelID
    ORDER BY
        CASE WHEN IsDefault = 1 THEN 0 ELSE 1 END,
        UpdatedDate DESC,
        ModelPackagingMethodOptionID DESC
) AS pkg;
GO

CREATE OR ALTER VIEW dbo.v_Model_Materials
AS
SELECT
    CAST(m.ClientID AS nvarchar(50)) AS TenantId,
    mmc.ModelID AS ModelId,
    mmc.ProductWizardSectionID,
    pws.ProductWizardSectionNM,
    pws.IsFSCEnabled,
    pws.IsRCSEnabled,
    pws.MaterialGroupID,
    pws.DisplayOrder
FROM dbo.ModelMaterialConfig AS mmc
INNER JOIN dbo.Model AS m
    ON m.ModelID = mmc.ModelID
LEFT JOIN dbo.ProductWizardSection AS pws
    ON pws.ProductWizardSectionID = mmc.ProductWizardSectionID;
GO
