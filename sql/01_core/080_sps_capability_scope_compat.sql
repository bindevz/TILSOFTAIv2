SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- SPRINT 21: Forward-facing capability-scope SQL wrappers.
-- Legacy tables/columns still use Module* names for deployed DB compatibility.
-- New callers should use CapabilityScope* views/procedures.
-- ============================================================

CREATE OR ALTER VIEW dbo.CapabilityScopeCatalog
AS
    SELECT
        ModuleKey AS CapabilityScopeKey,
        AppKey,
        IsEnabled,
        Instruction,
        Priority,
        TenantId,
        Language
    FROM dbo.ModuleCatalog;
GO

CREATE OR ALTER VIEW dbo.ToolCatalogCapabilityScope
AS
    SELECT
        ToolName,
        ModuleKey AS CapabilityScopeKey,
        AppKey,
        TenantId,
        IsEnabled
    FROM dbo.ToolCatalogScope;
GO

CREATE OR ALTER VIEW dbo.MetadataDictionaryCapabilityScope
AS
    SELECT
        MetadataKey,
        ModuleKey AS CapabilityScopeKey,
        AppKey,
        TenantId,
        IsEnabled
    FROM dbo.MetadataDictionaryScope;
GO

CREATE OR ALTER VIEW dbo.RuntimePolicyCapabilityScope
AS
    SELECT
        PolicyId,
        PolicyKey,
        TenantId,
        ModuleKey AS CapabilityScopeKey,
        AppKey,
        Environment,
        Language,
        Priority,
        IsEnabled,
        JsonValue,
        UpdatedAtUtc,
        UpdatedBy
    FROM dbo.RuntimePolicy;
GO

CREATE OR ALTER VIEW dbo.ReActFollowUpRuleCapabilityScope
AS
    SELECT
        RuleId,
        RuleKey,
        TenantId,
        ModuleKey AS CapabilityScopeKey,
        AppKey,
        ToolName,
        Priority,
        IsEnabled,
        JsonPath,
        Operator,
        CompareValue,
        FollowUpToolName,
        ArgsTemplateJson,
        PromptHint,
        UpdatedAtUtc,
        UpdatedBy
    FROM dbo.ReActFollowUpRule;
GO

CREATE OR ALTER PROCEDURE dbo.app_capabilityscope_list
    @TenantId nvarchar(50) = NULL,
    @Language nvarchar(10) = 'en'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CapabilityScopeKey, AppKey, Instruction, Priority
    FROM dbo.CapabilityScopeCatalog
    WHERE IsEnabled = 1
      AND Language = @Language
      AND (TenantId IS NULL OR TenantId = @TenantId)
    ORDER BY Priority ASC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_toolcatalog_list_by_capability_scope
    @TenantId nvarchar(50),
    @Language nvarchar(10),
    @DefaultLanguage nvarchar(10) = 'en',
    @CapabilityScopesJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    IF ISJSON(@CapabilityScopesJson) <> 1
    BEGIN
        RAISERROR('@CapabilityScopesJson must be a valid JSON array.', 16, 1);
        RETURN;
    END

    SELECT DISTINCT
        tc.ToolName, tc.SpName, tc.IsEnabled, tc.RequiredRoles,
        COALESCE(tt.JsonSchema, tc.JsonSchema) AS JsonSchema,
        COALESCE(tt.Instruction, tc.Instruction) AS Instruction,
        COALESCE(tt.Description, tc.Description) AS Description
    FROM dbo.ToolCatalog tc
    INNER JOIN dbo.ToolCatalogCapabilityScope tcs
        ON tc.ToolName = tcs.ToolName
        AND tcs.IsEnabled = 1
        AND (tcs.TenantId IS NULL OR tcs.TenantId = @TenantId)
    LEFT JOIN dbo.ToolCatalogTranslation tt
        ON tc.ToolName = tt.ToolName AND tt.Language = @Language
    WHERE tc.IsEnabled = 1
      AND tcs.CapabilityScopeKey IN (
          SELECT [value] FROM OPENJSON(@CapabilityScopesJson)
      )

    UNION

    SELECT DISTINCT
        tc.ToolName, tc.SpName, tc.IsEnabled, tc.RequiredRoles,
        COALESCE(tt.JsonSchema, tc.JsonSchema) AS JsonSchema,
        COALESCE(tt.Instruction, tc.Instruction) AS Instruction,
        COALESCE(tt.Description, tc.Description) AS Description
    FROM dbo.ToolCatalog tc
    INNER JOIN dbo.ToolCatalogCapabilityScope tcs
        ON tc.ToolName = tcs.ToolName
        AND tcs.CapabilityScopeKey = 'platform'
        AND tcs.IsEnabled = 1
        AND (tcs.TenantId IS NULL OR tcs.TenantId = @TenantId)
    LEFT JOIN dbo.ToolCatalogTranslation tt
        ON tc.ToolName = tt.ToolName AND tt.Language = @Language
    WHERE tc.IsEnabled = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_metadatadictionary_list_by_capability_scope
    @TenantId nvarchar(50) = NULL,
    @Language nvarchar(10) = 'en',
    @DefaultLanguage nvarchar(10) = 'en',
    @CapabilityScopesJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    IF ISJSON(@CapabilityScopesJson) <> 1
    BEGIN
        RAISERROR('@CapabilityScopesJson must be a valid JSON array.', 16, 1);
        RETURN;
    END

    SELECT
        md.[Key], md.DisplayName, md.Description, md.Unit, md.Examples
    FROM dbo.MetadataDictionary md
    INNER JOIN dbo.MetadataDictionaryCapabilityScope mds
        ON md.[Key] = mds.MetadataKey
        AND mds.IsEnabled = 1
        AND (mds.TenantId IS NULL OR mds.TenantId = @TenantId)
    WHERE (md.TenantId IS NULL OR md.TenantId = @TenantId)
      AND md.Language = @Language
      AND mds.CapabilityScopeKey IN (
          SELECT [value] FROM OPENJSON(@CapabilityScopesJson)
      )
    ORDER BY md.[Key];
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_policy_resolve_by_capability_scope
    @TenantId nvarchar(50),
    @CapabilityScopesJson nvarchar(max) = NULL,
    @AppKey nvarchar(50) = NULL,
    @Environment nvarchar(50) = NULL,
    @Language nvarchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CapabilityScopes TABLE (CapabilityScopeKey nvarchar(50));
    IF (@CapabilityScopesJson IS NOT NULL AND ISJSON(@CapabilityScopesJson) = 1)
    BEGIN
        INSERT INTO @CapabilityScopes (CapabilityScopeKey)
        SELECT value FROM OPENJSON(@CapabilityScopesJson);
    END

    ;WITH Candidates AS
    (
        SELECT
            PolicyId,
            PolicyKey,
            JsonValue,
            Priority,
            UpdatedAtUtc,
            (CASE WHEN TenantId = @TenantId THEN 16 ELSE 0 END) +
            (CASE WHEN CapabilityScopeKey IS NOT NULL THEN 8 ELSE 0 END) +
            (CASE WHEN AppKey IS NOT NULL THEN 4 ELSE 0 END) +
            (CASE WHEN Environment IS NOT NULL THEN 2 ELSE 0 END) +
            (CASE WHEN Language IS NOT NULL THEN 1 ELSE 0 END) AS SpecificityScore
        FROM dbo.RuntimePolicyCapabilityScope
        WHERE
            IsEnabled = 1
            AND (TenantId IS NULL OR TenantId = @TenantId)
            AND (@AppKey IS NULL OR AppKey IS NULL OR AppKey = @AppKey)
            AND (@Environment IS NULL OR Environment IS NULL OR Environment = @Environment)
            AND (@Language IS NULL OR Language IS NULL OR Language = @Language)
            AND (
                CapabilityScopeKey IS NULL
                OR EXISTS (SELECT 1 FROM @CapabilityScopes cs WHERE cs.CapabilityScopeKey = CapabilityScopeKey)
            )
    ),
    Ranked AS
    (
        SELECT
            PolicyKey,
            JsonValue,
            ROW_NUMBER() OVER
            (
                PARTITION BY PolicyKey
                ORDER BY
                    SpecificityScore DESC,
                    Priority ASC,
                    UpdatedAtUtc DESC,
                    PolicyId DESC
            ) AS rn
        FROM Candidates
    )
    SELECT PolicyKey, JsonValue
    FROM Ranked
    WHERE rn = 1
    ORDER BY PolicyKey;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_react_followup_list_by_capability_scope
    @TenantId nvarchar(50),
    @CapabilityScopesJson nvarchar(max),
    @AppKey nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF ISJSON(@CapabilityScopesJson) <> 1
    BEGIN
        RAISERROR('@CapabilityScopesJson must be a valid JSON array.', 16, 1);
        RETURN;
    END

    DECLARE @CapabilityScopes TABLE (CapabilityScopeKey nvarchar(50));
    INSERT INTO @CapabilityScopes (CapabilityScopeKey)
    SELECT value FROM OPENJSON(@CapabilityScopesJson);

    SELECT
        RuleId,
        RuleKey,
        TenantId,
        CapabilityScopeKey,
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
    FROM dbo.ReActFollowUpRuleCapabilityScope
    WHERE
        IsEnabled = 1
        AND (TenantId IS NULL OR TenantId = @TenantId)
        AND (AppKey IS NULL OR @AppKey IS NULL OR AppKey = @AppKey)
        AND EXISTS (
            SELECT 1
            FROM @CapabilityScopes cs
            WHERE cs.CapabilityScopeKey = ReActFollowUpRuleCapabilityScope.CapabilityScopeKey
        )
    ORDER BY Priority ASC, RuleId ASC;
END;
GO
