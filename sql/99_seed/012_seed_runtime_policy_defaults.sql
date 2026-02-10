-- ============================================================
-- Patch 35.01: Seed default RuntimePolicy entries
-- Global defaults (TenantId/ModuleKey = NULL) — can be overridden per tenant/module.
-- ============================================================

-- tool_catalog_context_pack policy
IF NOT EXISTS (SELECT 1 FROM dbo.RuntimePolicy WHERE PolicyKey = 'tool_catalog_context_pack' AND TenantId IS NULL AND ModuleKey IS NULL)
BEGIN
    INSERT INTO dbo.RuntimePolicy (PolicyKey, TenantId, ModuleKey, AppKey, Environment, Language, Priority, IsEnabled, JsonValue, UpdatedBy)
    VALUES (
        'tool_catalog_context_pack',
        NULL, NULL, NULL, NULL, NULL,
        100, 1,
        N'{"enabled":true,"maxTools":20,"maxTotalTokens":1200,"maxInstructionTokensPerTool":80,"maxDescriptionTokensPerTool":40,"orderStrategy":"core_then_scope_order"}',
        'patch_35_seed'
    );
END

-- react_nudge policy
IF NOT EXISTS (SELECT 1 FROM dbo.RuntimePolicy WHERE PolicyKey = 'react_nudge' AND TenantId IS NULL AND ModuleKey IS NULL)
BEGIN
    INSERT INTO dbo.RuntimePolicy (PolicyKey, TenantId, ModuleKey, AppKey, Environment, Language, Priority, IsEnabled, JsonValue, UpdatedBy)
    VALUES (
        'react_nudge',
        NULL, NULL, NULL, NULL, NULL,
        100, 1,
        N'{"enabled":true,"maxNudgesPerTurn":2,"maxTotalNudges":6,"dedupeWindowTurns":6}',
        'patch_35_seed'
    );
END
GO
