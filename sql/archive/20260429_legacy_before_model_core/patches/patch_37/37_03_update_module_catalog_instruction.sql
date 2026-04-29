-- ============================================================
-- PATCH 37.03: Legacy capability-scope data update for product-model totals.
-- ModuleCatalog/ModuleKey are historical storage names retained for compatibility.
-- ============================================================

UPDATE dbo.ModuleCatalog
SET Instruction = 'Product models: dimensions, weight, CBM, pieces, materials, packaging, logistics metrics.
ALSO use this capability scope for totals/counts of models (e.g., total models in season) using dedicated model tools.'
WHERE ModuleKey='model' AND Language='en';

UPDATE dbo.ModuleCatalog
SET Instruction = N'Sản phẩm model: kích thước, trọng lượng, CBM, pieces, vật liệu, đóng gói, chỉ số logistics.
Dùng capability scope này cả cho truy vấn tổng/đếm số lượng model (ví dụ: tổng số model trong mùa) bằng tool chuyên biệt.'
WHERE ModuleKey='model' AND Language='vi';

