SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

MERGE dbo.MetadataDictionary AS target
USING (VALUES
    ('Model.PieceCount', CAST(NULL AS nvarchar(50)), 'en', 'Piece Count', 'Number of pieces associated with the model.', NULL, 'Example: 12'),
    ('Model.Cbm', CAST(NULL AS nvarchar(50)), 'en', 'Total CBM', 'Total cubic meters for the model packaging.', 'm3', 'Example: 2.35'),
    ('Model.WeightKg', CAST(NULL AS nvarchar(50)), 'en', 'Total Weight', 'Total weight of the model packaging.', 'kg', 'Example: 125.5'),
    ('Model.Loadability', CAST(NULL AS nvarchar(50)), 'en', 'Loadability Index', 'Relative loadability metric for the model packaging.', NULL, 'Example: 0.82'),
    ('Model.Qnt40HC', CAST(NULL AS nvarchar(50)), 'en', 'Qty per 40HC', 'Quantity that fits in a 40HC container.', 'units', 'Example: 240'),
    ('Model.Density', CAST(NULL AS nvarchar(50)), 'en', 'Density', 'Weight per cubic meter for the model.', 'kg/m3', 'Example: 54.3'),
    ('Model.CbmPer40HC', CAST(NULL AS nvarchar(50)), 'en', 'CBM per 40HC', 'Cubic meters per 40HC container (lower is better).', 'm3', 'Example: 0.012'),
    ('Model.UnitsPerCarton', CAST(NULL AS nvarchar(50)), 'en', 'Units per Carton', 'Units packed per carton for a packaging option.', 'units', 'Example: 6'),
    ('Model.CartonCbm', CAST(NULL AS nvarchar(50)), 'en', 'Carton CBM', 'Cubic meters per carton for a packaging option.', 'm3', 'Example: 0.15'),
    ('Model.LoadabilityDelta', CAST(NULL AS nvarchar(50)), 'en', 'Loadability Delta', 'Difference from the best loadability in the comparison.', NULL, 'Example: -0.12'),
    ('Model.WeightDelta', CAST(NULL AS nvarchar(50)), 'en', 'Weight Delta', 'Difference from the heaviest model in the comparison.', 'kg', 'Example: -12.5'),
    ('Model.CbmDelta', CAST(NULL AS nvarchar(50)), 'en', 'CBM Delta', 'Difference from the largest CBM model in the comparison.', 'm3', 'Example: -0.25'),
    ('Material.Name', CAST(NULL AS nvarchar(50)), 'en', 'Material Name', 'Name of the material used in the model.', NULL, 'Example: Recycled Cardboard'),
    ('Material.Category', CAST(NULL AS nvarchar(50)), 'en', 'Material Category', 'Category grouping for materials.', NULL, 'Example: Packaging'),
    ('Material.Section', CAST(NULL AS nvarchar(50)), 'en', 'Material Section', 'Section or area of the product where the material applies.', NULL, 'Example: Outer Carton'),
    ('Material.Quantity', CAST(NULL AS nvarchar(50)), 'en', 'Material Quantity', 'Quantity of material used.', NULL, 'Example: 24'),
    ('Material.WeightKg', CAST(NULL AS nvarchar(50)), 'en', 'Material Weight', 'Weight contribution of the material.', 'kg', 'Example: 3.2'),
    ('Material.Density', CAST(NULL AS nvarchar(50)), 'en', 'Material Density', 'Density of the material.', 'kg/m3', 'Example: 620')
) AS source([Key], TenantId, Language, DisplayName, Description, Unit, Examples)
ON target.[Key] = source.[Key]
   AND ((target.TenantId = source.TenantId) OR (target.TenantId IS NULL AND source.TenantId IS NULL))
   AND target.Language = source.Language
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Key], TenantId, Language, DisplayName, Description, Unit, Examples)
    VALUES (source.[Key], source.TenantId, source.Language, source.DisplayName, source.Description, source.Unit, source.Examples);
GO
