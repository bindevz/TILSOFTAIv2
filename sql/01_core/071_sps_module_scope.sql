SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- SP: app_modulecatalog_list
-- Returns available modules for LLM routing
-- =============================================
CREATE OR ALTER PROCEDURE dbo.app_modulecatalog_list
    @TenantId       nvarchar(50) = NULL,
    @Language        nvarchar(10) = 'en'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ModuleKey, AppKey, Instruction, Priority
    FROM dbo.ModuleCatalog
    WHERE IsEnabled = 1
      AND Language = @Language
      AND (TenantId IS NULL OR TenantId = @TenantId)
    ORDER BY Priority ASC;
END;
GO

-- =============================================
-- SP: app_toolcatalog_list_scoped
-- Returns tools filtered by selected modules
-- Always includes 'platform' module tools (action_request_write, diagnostics_run, etc.)
-- @ModulesJson: JSON array, e.g., '["model","analytics"]'
-- =============================================
CREATE OR ALTER PROCEDURE dbo.app_toolcatalog_list_scoped
    @TenantId       nvarchar(50),
    @Language        nvarchar(10),
    @DefaultLanguage nvarchar(10) = 'en',
    @ModulesJson     nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    -- Validate JSON input
    IF ISJSON(@ModulesJson) <> 1
    BEGIN
        RAISERROR('@ModulesJson must be a valid JSON array.', 16, 1);
        RETURN;
    END

    -- Scoped tools: tools belonging to the selected modules
    SELECT DISTINCT 
        tc.ToolName, tc.SpName, tc.IsEnabled, tc.RequiredRoles,
        COALESCE(tt.JsonSchema, tc.JsonSchema) AS JsonSchema,
        COALESCE(tt.Instruction, tc.Instruction) AS Instruction,
        COALESCE(tt.Description, tc.Description) AS Description
    FROM dbo.ToolCatalog tc
    INNER JOIN dbo.ToolCatalogScope tcs 
        ON tc.ToolName = tcs.ToolName
        AND tcs.IsEnabled = 1
        AND (tcs.TenantId IS NULL OR tcs.TenantId = @TenantId)
    LEFT JOIN dbo.ToolCatalogTranslation tt 
        ON tc.ToolName = tt.ToolName AND tt.Language = @Language
    WHERE tc.IsEnabled = 1
      AND tcs.ModuleKey IN (
          SELECT [value] FROM OPENJSON(@ModulesJson)
      )

    UNION

    -- Platform tools: always available regardless of scope
    SELECT DISTINCT 
        tc.ToolName, tc.SpName, tc.IsEnabled, tc.RequiredRoles,
        COALESCE(tt.JsonSchema, tc.JsonSchema) AS JsonSchema,
        COALESCE(tt.Instruction, tc.Instruction) AS Instruction,
        COALESCE(tt.Description, tc.Description) AS Description
    FROM dbo.ToolCatalog tc
    INNER JOIN dbo.ToolCatalogScope tcs 
        ON tc.ToolName = tcs.ToolName
        AND tcs.ModuleKey = 'platform'
        AND tcs.IsEnabled = 1
        AND (tcs.TenantId IS NULL OR tcs.TenantId = @TenantId)
    LEFT JOIN dbo.ToolCatalogTranslation tt 
        ON tc.ToolName = tt.ToolName AND tt.Language = @Language
    WHERE tc.IsEnabled = 1;
END;
GO

-- =============================================
-- SP: app_metadatadictionary_list_scoped
-- Returns metadata keys filtered by selected modules
-- @ModulesJson: JSON array, e.g., '["model"]'
-- =============================================
CREATE OR ALTER PROCEDURE dbo.app_metadatadictionary_list_scoped
    @TenantId       nvarchar(50) = NULL,
    @Language        nvarchar(10) = 'en',
    @DefaultLanguage nvarchar(10) = 'en',
    @ModulesJson     nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    IF ISJSON(@ModulesJson) <> 1
    BEGIN
        RAISERROR('@ModulesJson must be a valid JSON array.', 16, 1);
        RETURN;
    END

    SELECT 
        md.[Key], md.DisplayName, md.Description, md.Unit, md.Examples
    FROM dbo.MetadataDictionary md
    INNER JOIN dbo.MetadataDictionaryScope mds 
        ON md.[Key] = mds.MetadataKey
        AND mds.IsEnabled = 1
        AND (mds.TenantId IS NULL OR mds.TenantId = @TenantId)
    WHERE (md.TenantId IS NULL OR md.TenantId = @TenantId)
      AND md.Language = @Language
      AND mds.ModuleKey IN (
          SELECT [value] FROM OPENJSON(@ModulesJson)
      )
    ORDER BY md.[Key];
END;
GO
