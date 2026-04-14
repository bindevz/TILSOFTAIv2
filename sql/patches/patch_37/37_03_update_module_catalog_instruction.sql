-- ============================================================
-- PATCH 37.03: Legacy capability-scope data update for product-model totals.
-- ModuleCatalog/ModuleKey are historical storage names retained for compatibility.
-- ============================================================

UPDATE dbo.ModuleCatalog
SET Instruction = 'Product models: dimensions, weight, CBM, pieces, materials, packaging, logistics metrics.
ALSO use this capability scope for totals/counts of models (e.g., total models in season) using dedicated model tools.'
WHERE ModuleKey='model' AND Language='en';

UPDATE dbo.ModuleCatalog
SET Instruction = N'Sáº£n pháº©m model: kÃ­ch thÆ°á»›c, trá»ng lÆ°á»£ng, CBM, pieces, váº­t liá»‡u, Ä‘Ã³ng gÃ³i, chá»‰ sá»‘ logistics.
DÃ™NG capability scope nÃ y cáº£ cho truy váº¥n tá»•ng/Ä‘áº¿m sá»‘ lÆ°á»£ng model (vÃ­ dá»¥: tá»•ng sá»‘ model trong mÃ¹a) báº±ng tool chuyÃªn biá»‡t.'
WHERE ModuleKey='model' AND Language='vi';

