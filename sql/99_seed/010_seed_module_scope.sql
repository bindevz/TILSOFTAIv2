SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- Seed: Module Catalog
-- =============================================

-- Clear and re-seed (idempotent)
DELETE FROM dbo.ModuleCatalog;
GO

INSERT INTO dbo.ModuleCatalog (ModuleKey, AppKey, Instruction, Priority, Language)
VALUES
    ('model', '', 'Product models: dimensions, weight, CBM, pieces, materials, packaging, logistics metrics. Use for queries about specific model details, comparison, or configuration.', 10, 'en'),
    ('model', '', N'Sản phẩm model: kích thước, trọng lượng, CBM, pieces, vật liệu, đóng gói, chỉ số logistics. Dùng cho câu hỏi về chi tiết model, so sánh, cấu hình.', 10, 'vi'),
    ('analytics', '', 'Aggregate analytics: statistical queries across datasets like counts, sums, averages, grouping, filtering, time-series. Use for trend analysis, reporting, dashboards.', 20, 'en'),
    ('analytics', '', N'Phân tích tổng hợp: truy vấn thống kê trên datasets như đếm, tổng, trung bình, nhóm, lọc, thời gian. Dùng cho phân tích xu hướng, báo cáo, dashboard.', 20, 'vi'),
    ('platform', '', 'System operations: diagnostics, action requests, tool listing, write actions requiring human approval.', 0, 'en'),
    ('platform', '', N'Thao tác hệ thống: chẩn đoán, yêu cầu hành động, danh sách công cụ, ghi dữ liệu cần phê duyệt.', 0, 'vi');
GO

-- =============================================
-- Seed: ToolCatalogScope
-- Maps each tool to its owning module
-- Tool names verified from seed files:
--   002_seed_toolcatalog_core.sql: tool.list, atomic_execute_plan
--   003_seed_toolcatalog_model.sql: model_get_overview, model_get_pieces,
--       model_get_materials, model_compare_models, model_get_packaging, model_count
--   007_seed_toolcatalog_platform.sql: diagnostics_run, action_request_write
--   analytics/001_seed_toolcatalog_analytics.sql: catalog_search,
--       catalog_get_dataset, analytics_validate_plan, analytics_execute_plan
-- =============================================

DELETE FROM dbo.ToolCatalogScope;
GO

-- Model tools
INSERT INTO dbo.ToolCatalogScope (ToolName, ModuleKey, AppKey)
VALUES
    ('model_get_overview', 'model', ''),
    ('model_get_pieces', 'model', ''),
    ('model_get_materials', 'model', ''),
    ('model_compare_models', 'model', ''),
    ('model_get_packaging', 'model', ''),
    ('model_count', 'model', '');
GO

-- Platform tools (always included regardless of scope)
INSERT INTO dbo.ToolCatalogScope (ToolName, ModuleKey, AppKey)
VALUES
    ('diagnostics_run', 'platform', ''),
    ('action_request_write', 'platform', ''),
    ('tool.list', 'platform', ''),
    ('atomic_execute_plan', 'platform', '');
GO

-- Analytics tools
INSERT INTO dbo.ToolCatalogScope (ToolName, ModuleKey, AppKey)
VALUES
    ('catalog_search', 'analytics', ''),
    ('catalog_get_dataset', 'analytics', ''),
    ('analytics_validate_plan', 'analytics', ''),
    ('analytics_execute_plan', 'analytics', '');
GO

-- =============================================
-- Seed: MetadataDictionaryScope
-- Maps metadata keys to modules
-- Keys verified from seed files:
--   004_seed_metadata_model.sql: Model.*, Material.*
--   analytics/002_seed_metadata_dictionary_analytics.sql: analytics.*
-- =============================================

DELETE FROM dbo.MetadataDictionaryScope;
GO

-- Model metadata keys (from 004_seed_metadata_model.sql)
INSERT INTO dbo.MetadataDictionaryScope (MetadataKey, ModuleKey, AppKey)
VALUES
    ('Model.PieceCount', 'model', ''),
    ('Model.Cbm', 'model', ''),
    ('Model.WeightKg', 'model', ''),
    ('Model.Loadability', 'model', ''),
    ('Model.Qnt40HC', 'model', ''),
    ('Model.Density', 'model', ''),
    ('Model.CbmPer40HC', 'model', ''),
    ('Model.UnitsPerCarton', 'model', ''),
    ('Model.CartonCbm', 'model', ''),
    ('Model.LoadabilityDelta', 'model', ''),
    ('Model.WeightDelta', 'model', ''),
    ('Model.CbmDelta', 'model', ''),
    ('Material.Name', 'model', ''),
    ('Material.Category', 'model', ''),
    ('Material.Section', 'model', ''),
    ('Material.Quantity', 'model', ''),
    ('Material.WeightKg', 'model', ''),
    ('Material.Density', 'model', '');
GO

-- Analytics metadata keys (from analytics/002_seed_metadata_dictionary_analytics.sql)
INSERT INTO dbo.MetadataDictionaryScope (MetadataKey, ModuleKey, AppKey)
VALUES
    ('analytics.headline', 'analytics', ''),
    ('analytics.breakdown', 'analytics', ''),
    ('analytics.total', 'analytics', ''),
    ('analytics.notes', 'analytics', ''),
    ('analytics.filter', 'analytics', ''),
    ('analytics.limit', 'analytics', ''),
    ('analytics.freshness', 'analytics', ''),
    ('analytics.truncated', 'analytics', ''),
    ('analytics.season', 'analytics', ''),
    ('analytics.count', 'analytics', '');
GO
