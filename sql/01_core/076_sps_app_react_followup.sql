-- ============================================================
-- Patch 35.01: app_react_followup_list_scoped stored procedure
-- Returns enabled follow-up rules for a given tenant/module scope.
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.app_react_followup_list_scoped
    @TenantId        nvarchar(50),
    @ModuleKeysJson  nvarchar(max),             -- JSON array e.g. '["model"]'
    @AppKey          nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ModuleKeys TABLE (ModuleKey nvarchar(50));
    INSERT INTO @ModuleKeys (ModuleKey)
    SELECT value FROM OPENJSON(@ModuleKeysJson);

    SELECT
        RuleId,
        RuleKey,
        TenantId,
        ModuleKey,
        AppKey,
        ToolName,
        Priority,
        JsonPath,
        Operator,
        CompareValue,
        FollowUpToolName,
        ArgsTemplateJson,
        PromptHint,
        UpdatedAtUtc
    FROM dbo.ReActFollowUpRule
    WHERE
        IsEnabled = 1
        AND (TenantId IS NULL OR TenantId = @TenantId)
        AND (AppKey IS NULL OR @AppKey IS NULL OR AppKey = @AppKey)
        AND EXISTS (SELECT 1 FROM @ModuleKeys mk WHERE mk.ModuleKey = ReActFollowUpRule.ModuleKey)
    ORDER BY Priority ASC, RuleId ASC;
END
GO
