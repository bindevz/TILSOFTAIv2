-- ============================================================
-- Patch 35.01: Seed ReActFollowUpRules for Model module
-- ============================================================

-- Rule 1: model_get_overview → model_get_pieces (when PieceCount > 0)
IF NOT EXISTS (SELECT 1 FROM dbo.ReActFollowUpRule WHERE RuleKey = 'model.overview.pieces')
BEGIN
    INSERT INTO dbo.ReActFollowUpRule
        (RuleKey, TenantId, ModuleKey, AppKey, ToolName, Priority, IsEnabled,
         JsonPath, Operator, CompareValue, FollowUpToolName, ArgsTemplateJson, PromptHint, UpdatedBy)
    VALUES
        ('model.overview.pieces', NULL, 'model', NULL, 'model_get_overview', 10, 1,
         '$.PieceCount', '>', '0', 'model_get_pieces',
         N'{"modelId":"{{$.ModelId}}"}',
         'If PieceCount > 0, call model_get_pieces(modelId) before final answer to list pieces summary.',
         'patch_35_seed');
END

-- Rule 2: model_get_overview → model_get_materials (when HasMaterials == true)
IF NOT EXISTS (SELECT 1 FROM dbo.ReActFollowUpRule WHERE RuleKey = 'model.overview.materials')
BEGIN
    INSERT INTO dbo.ReActFollowUpRule
        (RuleKey, TenantId, ModuleKey, AppKey, ToolName, Priority, IsEnabled,
         JsonPath, Operator, CompareValue, FollowUpToolName, ArgsTemplateJson, PromptHint, UpdatedBy)
    VALUES
        ('model.overview.materials', NULL, 'model', NULL, 'model_get_overview', 20, 1,
         '$.HasMaterials', '==', 'true', 'model_get_materials',
         N'{"modelId":"{{$.ModelId}}"}',
         'If materials exist, call model_get_materials(modelId) and include materials in analysis.',
         'patch_35_seed');
END

-- Rule 3: model_get_overview → model_get_packaging (when PackagingMethodId exists)
IF NOT EXISTS (SELECT 1 FROM dbo.ReActFollowUpRule WHERE RuleKey = 'model.overview.packaging')
BEGIN
    INSERT INTO dbo.ReActFollowUpRule
        (RuleKey, TenantId, ModuleKey, AppKey, ToolName, Priority, IsEnabled,
         JsonPath, Operator, CompareValue, FollowUpToolName, ArgsTemplateJson, PromptHint, UpdatedBy)
    VALUES
        ('model.overview.packaging', NULL, 'model', NULL, 'model_get_overview', 30, 1,
         '$.PackagingMethodId', 'exists', NULL, 'model_get_packaging',
         N'{"packagingMethodId":"{{$.PackagingMethodId}}"}',
         'If PackagingMethodId is present, call model_get_packaging(packagingMethodId) and include packaging details.',
         'patch_35_seed');
END
GO
