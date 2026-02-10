SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- Patch 34.10: Seed Atomic Catalogs for Model module
-- Idempotent: Uses MERGE pattern
-- =============================================

-- ===================
-- DatasetCatalog
-- ===================

MERGE dbo.DatasetCatalog AS tgt
USING (VALUES
    ('model_overview',  'dbo.vw_ModelSemantic',     NULL, 1, NULL),
    ('model_pieces',    'dbo.ModelPiece',            NULL, 1, NULL),
    ('model_materials', 'dbo.ModelMaterial',         NULL, 1, NULL),
    ('model_packaging', 'dbo.ModelPackagingOption',  NULL, 1, NULL)
) AS src (DatasetKey, BaseObject, TimeColumn, IsEnabled, TenantId)
ON tgt.DatasetKey = src.DatasetKey
    AND (tgt.TenantId = src.TenantId OR (tgt.TenantId IS NULL AND src.TenantId IS NULL))
WHEN MATCHED THEN
    UPDATE SET
        BaseObject = src.BaseObject,
        TimeColumn = src.TimeColumn,
        IsEnabled  = src.IsEnabled,
        UpdatedAtUtc = sysutcdatetime()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (DatasetKey, BaseObject, TimeColumn, IsEnabled, TenantId)
    VALUES (src.DatasetKey, src.BaseObject, src.TimeColumn, src.IsEnabled, src.TenantId);
GO

-- ===================
-- FieldCatalog — model_overview  (vw_ModelSemantic columns)
-- ===================

MERGE dbo.FieldCatalog AS tgt
USING (VALUES
    -- DatasetKey,       FieldKey,           PhysicalColumn,     DataType,           IsMetric, IsDimension, AllowedAggregations,                IsFilterable, IsGroupable, IsSortable, IsEnabled
    ('model_overview', 'TenantId',          'TenantId',          'nvarchar(50)',      0, 1, NULL,                                                1, 1, 1, 1),
    ('model_overview', 'Language',          'Language',          'nvarchar(10)',      0, 1, NULL,                                                1, 1, 1, 1),
    ('model_overview', 'ModelId',           'ModelId',           'int',               0, 1, 'count,countDistinct',                               1, 1, 1, 1),
    ('model_overview', 'ModelCode',         'ModelCode',         'nvarchar(50)',      0, 1, NULL,                                                1, 1, 1, 1),
    ('model_overview', 'Name',             'Name',              'nvarchar(200)',     0, 1, NULL,                                                1, 0, 1, 1),
    ('model_overview', 'Description',      'Description',       'nvarchar(max)',     0, 0, NULL,                                                0, 0, 0, 1),
    ('model_overview', 'TotalCbm',         'TotalCbm',          'decimal(18,6)',     1, 0, 'sum,avg,min,max',                                   1, 0, 1, 1),
    ('model_overview', 'TotalWeightKg',    'TotalWeightKg',     'decimal(18,6)',     1, 0, 'sum,avg,min,max',                                   1, 0, 1, 1),
    ('model_overview', 'LoadabilityIndex', 'LoadabilityIndex',  'decimal(10,4)',     1, 0, 'avg,min,max',                                       1, 0, 1, 1),
    ('model_overview', 'Qnt40HC',          'Qnt40HC',           'int',               1, 0, 'sum,avg,min,max',                                   1, 0, 1, 1),
    ('model_overview', 'PieceCount',       'PieceCount',        'int',               1, 0, 'sum,avg,min,max',                                   1, 0, 1, 1),
    ('model_overview', 'PackagingName',    'PackagingName',     'nvarchar(100)',     0, 1, NULL,                                                1, 1, 1, 1),
    ('model_overview', 'BoxInSet',         'BoxInSet',          'int',               1, 0, 'sum,avg,min,max',                                   1, 0, 1, 1),
    ('model_overview', 'CartonCbm',        'CartonCbm',         'decimal(18,6)',     1, 0, 'sum,avg,min,max',                                   1, 0, 1, 1),
    ('model_overview', 'CartonWeightKg',   'CartonWeightKg',    'decimal(18,6)',     1, 0, 'sum,avg,min,max',                                   1, 0, 1, 1)
) AS src (DatasetKey, FieldKey, PhysicalColumn, DataType, IsMetric, IsDimension, AllowedAggregations, IsFilterable, IsGroupable, IsSortable, IsEnabled)
ON tgt.DatasetKey = src.DatasetKey AND tgt.FieldKey = src.FieldKey
WHEN MATCHED THEN
    UPDATE SET
        PhysicalColumn      = src.PhysicalColumn,
        DataType            = src.DataType,
        IsMetric            = src.IsMetric,
        IsDimension         = src.IsDimension,
        AllowedAggregations = src.AllowedAggregations,
        IsFilterable        = src.IsFilterable,
        IsGroupable         = src.IsGroupable,
        IsSortable          = src.IsSortable,
        IsEnabled           = src.IsEnabled
WHEN NOT MATCHED BY TARGET THEN
    INSERT (DatasetKey, FieldKey, PhysicalColumn, DataType, IsMetric, IsDimension, AllowedAggregations, IsFilterable, IsGroupable, IsSortable, IsEnabled)
    VALUES (src.DatasetKey, src.FieldKey, src.PhysicalColumn, src.DataType, src.IsMetric, src.IsDimension, src.AllowedAggregations, src.IsFilterable, src.IsGroupable, src.IsSortable, src.IsEnabled);
GO

-- ===================
-- FieldCatalog — model_pieces  (ModelPiece columns from ai_model_get_pieces SP)
-- ===================

MERGE dbo.FieldCatalog AS tgt
USING (VALUES
    ('model_pieces', 'ModelPieceId',  'ModelPieceId',  'int',            0, 1, 'count,countDistinct', 1, 0, 1, 1),
    ('model_pieces', 'ModelId',       'ModelId',       'int',            0, 1, 'count,countDistinct', 1, 1, 1, 1),
    ('model_pieces', 'PieceName',     'PieceName',     'nvarchar(200)',  0, 1, NULL,                  1, 1, 1, 1),
    ('model_pieces', 'Quantity',      'Quantity',      'int',            1, 0, 'sum,avg,min,max',     1, 0, 1, 1),
    ('model_pieces', 'ChildModelId',  'ChildModelId',  'int',            0, 1, NULL,                  1, 1, 1, 1),
    ('model_pieces', 'Sequence',      'Sequence',      'int',            0, 0, NULL,                  0, 0, 1, 1)
) AS src (DatasetKey, FieldKey, PhysicalColumn, DataType, IsMetric, IsDimension, AllowedAggregations, IsFilterable, IsGroupable, IsSortable, IsEnabled)
ON tgt.DatasetKey = src.DatasetKey AND tgt.FieldKey = src.FieldKey
WHEN MATCHED THEN
    UPDATE SET
        PhysicalColumn      = src.PhysicalColumn,
        DataType            = src.DataType,
        IsMetric            = src.IsMetric,
        IsDimension         = src.IsDimension,
        AllowedAggregations = src.AllowedAggregations,
        IsFilterable        = src.IsFilterable,
        IsGroupable         = src.IsGroupable,
        IsSortable          = src.IsSortable,
        IsEnabled           = src.IsEnabled
WHEN NOT MATCHED BY TARGET THEN
    INSERT (DatasetKey, FieldKey, PhysicalColumn, DataType, IsMetric, IsDimension, AllowedAggregations, IsFilterable, IsGroupable, IsSortable, IsEnabled)
    VALUES (src.DatasetKey, src.FieldKey, src.PhysicalColumn, src.DataType, src.IsMetric, src.IsDimension, src.AllowedAggregations, src.IsFilterable, src.IsGroupable, src.IsSortable, src.IsEnabled);
GO

-- ===================
-- FieldCatalog — model_materials  (ModelMaterial + Material columns from ai_model_get_materials SP)
-- ===================

MERGE dbo.FieldCatalog AS tgt
USING (VALUES
    ('model_materials', 'ModelMaterialId', 'ModelMaterialId', 'int',            0, 1, 'count,countDistinct', 1, 0, 1, 1),
    ('model_materials', 'Section',         'Section',         'nvarchar(100)',  0, 1, NULL,                  1, 1, 1, 1),
    ('model_materials', 'Quantity',        'Quantity',        'decimal(18,4)', 1, 0, 'sum,avg,min,max',     1, 0, 1, 1),
    ('model_materials', 'Unit',            'Unit',            'nvarchar(50)',   0, 1, NULL,                  1, 1, 1, 1),
    ('model_materials', 'WeightKg',        'WeightKg',        'decimal(18,6)', 1, 0, 'sum,avg,min,max',     1, 0, 1, 1),
    ('model_materials', 'MaterialCode',    'MaterialCode',    'nvarchar(50)',   0, 1, NULL,                  1, 1, 1, 1),
    ('model_materials', 'MaterialName',    'MaterialName',    'nvarchar(200)',  0, 1, NULL,                  1, 1, 1, 1),
    ('model_materials', 'Category',        'Category',        'nvarchar(100)',  0, 1, NULL,                  1, 1, 1, 1),
    ('model_materials', 'DensityKgPerM3',  'DensityKgPerM3',  'decimal(18,4)', 1, 0, 'avg,min,max',         1, 0, 1, 1)
) AS src (DatasetKey, FieldKey, PhysicalColumn, DataType, IsMetric, IsDimension, AllowedAggregations, IsFilterable, IsGroupable, IsSortable, IsEnabled)
ON tgt.DatasetKey = src.DatasetKey AND tgt.FieldKey = src.FieldKey
WHEN MATCHED THEN
    UPDATE SET
        PhysicalColumn      = src.PhysicalColumn,
        DataType            = src.DataType,
        IsMetric            = src.IsMetric,
        IsDimension         = src.IsDimension,
        AllowedAggregations = src.AllowedAggregations,
        IsFilterable        = src.IsFilterable,
        IsGroupable         = src.IsGroupable,
        IsSortable          = src.IsSortable,
        IsEnabled           = src.IsEnabled
WHEN NOT MATCHED BY TARGET THEN
    INSERT (DatasetKey, FieldKey, PhysicalColumn, DataType, IsMetric, IsDimension, AllowedAggregations, IsFilterable, IsGroupable, IsSortable, IsEnabled)
    VALUES (src.DatasetKey, src.FieldKey, src.PhysicalColumn, src.DataType, src.IsMetric, src.IsDimension, src.AllowedAggregations, src.IsFilterable, src.IsGroupable, src.IsSortable, src.IsEnabled);
GO

-- ===================
-- FieldCatalog — model_packaging  (ModelPackagingOption columns from ai_model_get_packaging SP)
-- ===================

MERGE dbo.FieldCatalog AS tgt
USING (VALUES
    ('model_packaging', 'PackagingOptionId', 'PackagingOptionId', 'int',            0, 1, 'count,countDistinct', 1, 0, 1, 1),
    ('model_packaging', 'OptionName',        'OptionName',        'nvarchar(100)',  0, 1, NULL,                  1, 1, 1, 1),
    ('model_packaging', 'PackagingType',     'PackagingType',     'nvarchar(50)',   0, 1, NULL,                  1, 1, 1, 1),
    ('model_packaging', 'UnitsPerCarton',    'UnitsPerCarton',    'int',            1, 0, 'sum,avg,min,max',     1, 0, 1, 1),
    ('model_packaging', 'CartonCbm',         'CartonCbm',         'decimal(18,6)', 1, 0, 'sum,avg,min,max',     1, 0, 1, 1),
    ('model_packaging', 'CartonWeightKg',    'CartonWeightKg',    'decimal(18,6)', 1, 0, 'sum,avg,min,max',     1, 0, 1, 1),
    ('model_packaging', 'LoadabilityIndex',  'LoadabilityIndex',  'decimal(10,4)', 1, 0, 'avg,min,max',         1, 0, 1, 1),
    ('model_packaging', 'Qnt40HC',           'Qnt40HC',           'int',            1, 0, 'sum,avg,min,max',     1, 0, 1, 1)
) AS src (DatasetKey, FieldKey, PhysicalColumn, DataType, IsMetric, IsDimension, AllowedAggregations, IsFilterable, IsGroupable, IsSortable, IsEnabled)
ON tgt.DatasetKey = src.DatasetKey AND tgt.FieldKey = src.FieldKey
WHEN MATCHED THEN
    UPDATE SET
        PhysicalColumn      = src.PhysicalColumn,
        DataType            = src.DataType,
        IsMetric            = src.IsMetric,
        IsDimension         = src.IsDimension,
        AllowedAggregations = src.AllowedAggregations,
        IsFilterable        = src.IsFilterable,
        IsGroupable         = src.IsGroupable,
        IsSortable          = src.IsSortable,
        IsEnabled           = src.IsEnabled
WHEN NOT MATCHED BY TARGET THEN
    INSERT (DatasetKey, FieldKey, PhysicalColumn, DataType, IsMetric, IsDimension, AllowedAggregations, IsFilterable, IsGroupable, IsSortable, IsEnabled)
    VALUES (src.DatasetKey, src.FieldKey, src.PhysicalColumn, src.DataType, src.IsMetric, src.IsDimension, src.AllowedAggregations, src.IsFilterable, src.IsGroupable, src.IsSortable, src.IsEnabled);
GO

-- ===================
-- EntityGraphCatalog — join relationships between model datasets
-- ===================

MERGE dbo.EntityGraphCatalog AS tgt
USING (VALUES
    ('model_overview_to_pieces',    'model_overview', 'model_pieces',    'LEFT', 'model_overview.ModelId = model_pieces.ModelId',    1),
    ('model_overview_to_materials', 'model_overview', 'model_materials', 'LEFT', 'model_overview.ModelId = model_materials.ModelId', 1),
    ('model_overview_to_packaging', 'model_overview', 'model_packaging', 'LEFT', 'model_overview.ModelId = model_packaging.ModelId', 1)
) AS src (GraphKey, FromDatasetKey, ToDatasetKey, JoinType, JoinConditionTemplate, IsEnabled)
ON tgt.GraphKey = src.GraphKey
WHEN MATCHED THEN
    UPDATE SET
        FromDatasetKey        = src.FromDatasetKey,
        ToDatasetKey          = src.ToDatasetKey,
        JoinType              = src.JoinType,
        JoinConditionTemplate = src.JoinConditionTemplate,
        IsEnabled             = src.IsEnabled
WHEN NOT MATCHED BY TARGET THEN
    INSERT (GraphKey, FromDatasetKey, ToDatasetKey, JoinType, JoinConditionTemplate, IsEnabled)
    VALUES (src.GraphKey, src.FromDatasetKey, src.ToDatasetKey, src.JoinType, src.JoinConditionTemplate, src.IsEnabled);
GO

PRINT 'Atomic catalog seed for Model module completed.';
GO
