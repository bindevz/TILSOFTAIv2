/*******************************************************************************
* TILSOFTAI Analytics Module - Semantic Views
* Purpose: Flattened views for catalog search and schema RAG
* 
* PATCH 28 FIX: Handle FieldCatalog without Id column using ROW_NUMBER()
*******************************************************************************/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Dataset catalog view with search-friendly fields
CREATE OR ALTER VIEW dbo.v_Analytics_DatasetCatalog
AS
SELECT
    dc.Id,
    dc.TenantId,
    dc.DatasetKey,
    ISNULL(dc.DisplayName, dc.DatasetKey) AS DisplayName,
    dc.Description,
    dc.BaseObject,
    dc.Grain,
    dc.TimeColumn,
    dc.IsEnabled,
    dc.Tags,
    ISNULL(dc.CreatedAtUtc, dc.UpdatedAtUtc) AS CreatedAtUtc,
    dc.UpdatedAtUtc,
    -- Computed search fields
    LOWER(dc.DatasetKey + ' ' + ISNULL(dc.DisplayName, '') + ' ' + ISNULL(dc.Description, '') + ' ' + ISNULL(dc.Tags, '')) AS SearchText,
    -- Field count for ranking
    (SELECT COUNT(1) FROM dbo.FieldCatalog fc WHERE fc.DatasetKey = dc.DatasetKey AND fc.IsEnabled = 1) AS EnabledFieldCount
FROM dbo.DatasetCatalog dc
WHERE dc.IsEnabled = 1;
GO

-- Field catalog view with semantic type info
-- NOTE: Uses ROW_NUMBER() for Id since FieldCatalog doesn't have identity column
CREATE OR ALTER VIEW dbo.v_Analytics_FieldCatalog
AS
SELECT
    ROW_NUMBER() OVER (ORDER BY fc.DatasetKey, fc.FieldKey) AS Id,
    fc.DatasetKey,
    fc.FieldKey,
    fc.PhysicalColumn,
    ISNULL(fc.DisplayName, fc.FieldKey) AS DisplayName,
    fc.Description,
    fc.DataType,
    fc.SemanticType,
    fc.AllowedAggregations,
    fc.IsFilterable,
    fc.IsGroupable,
    fc.IsSortable,
    fc.IsEnabled,
    fc.SecurityTag,
    -- Computed search fields
    LOWER(fc.FieldKey + ' ' + ISNULL(fc.DisplayName, '') + ' ' + ISNULL(fc.Description, '') + ' ' + ISNULL(fc.SemanticType, '')) AS SearchText
FROM dbo.FieldCatalog fc
WHERE fc.IsEnabled = 1;
GO

-- Combined catalog search view
CREATE OR ALTER VIEW dbo.v_Analytics_CatalogSearch
AS
SELECT
    'dataset' AS ItemType,
    dc.DatasetKey AS ItemKey,
    dc.DisplayName AS ItemName,
    dc.Description AS ItemDescription,
    dc.SearchText,
    dc.EnabledFieldCount AS Score,
    dc.TenantId,
    dc.Grain,
    NULL AS DatasetKeyRef
FROM dbo.v_Analytics_DatasetCatalog dc
UNION ALL
SELECT
    'field' AS ItemType,
    fc.FieldKey AS ItemKey,
    fc.DisplayName AS ItemName,
    fc.Description AS ItemDescription,
    fc.SearchText,
    CASE 
        WHEN fc.IsFilterable = 1 AND fc.IsGroupable = 1 THEN 10
        WHEN fc.IsFilterable = 1 OR fc.IsGroupable = 1 THEN 5
        ELSE 1
    END AS Score,
    NULL AS TenantId,
    NULL AS Grain,
    fc.DatasetKey AS DatasetKeyRef
FROM dbo.v_Analytics_FieldCatalog fc;
GO
