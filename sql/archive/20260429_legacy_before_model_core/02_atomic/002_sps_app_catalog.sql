SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.app_catalog_dataset_list
    @TenantId nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        DatasetKey,
        TenantId,
        BaseObject,
        TimeColumn,
        IsEnabled,
        UpdatedAtUtc
    FROM dbo.DatasetCatalog
    WHERE TenantId IS NULL OR TenantId = @TenantId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_catalog_field_list
    @TenantId nvarchar(50),
    @DatasetKey nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        f.DatasetKey,
        f.FieldKey,
        f.PhysicalColumn,
        f.DataType,
        f.IsMetric,
        f.IsDimension,
        f.AllowedAggregations,
        f.IsFilterable,
        f.IsGroupable,
        f.IsSortable,
        f.IsEnabled
    FROM dbo.FieldCatalog f
    INNER JOIN dbo.DatasetCatalog d ON d.DatasetKey = f.DatasetKey
    WHERE f.DatasetKey = @DatasetKey
      AND (d.TenantId IS NULL OR d.TenantId = @TenantId);
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_catalog_entitygraph_list
    @TenantId nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        g.GraphKey,
        g.FromDatasetKey,
        g.ToDatasetKey,
        g.JoinType,
        g.JoinConditionTemplate,
        g.IsEnabled
    FROM dbo.EntityGraphCatalog g
    INNER JOIN dbo.DatasetCatalog d ON d.DatasetKey = g.FromDatasetKey
    WHERE d.TenantId IS NULL OR d.TenantId = @TenantId;
END;
GO
