SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- LEGACY SP: app_modulecatalog_list
-- Returns compatibility capability scopes from the legacy ModuleCatalog table.
-- New callers should use app_capabilityscope_list.
-- =============================================
CREATE OR ALTER PROCEDURE dbo.app_modulecatalog_list
    @TenantId       nvarchar(50) = NULL,
    @Language        nvarchar(10) = 'en'
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF OBJECT_ID('dbo.app_sql_compatibility_usage_record', 'P') IS NOT NULL
        BEGIN
            EXEC dbo.app_sql_compatibility_usage_record
                @SurfaceName = N'app_modulecatalog_list',
                @SurfaceKind = N'legacy-procedure',
                @ForwardSurfaceName = N'app_capabilityscope_list',
                @TenantId = @TenantId,
                @Language = @Language,
                @CompatibilityNotes = N'Legacy capability-scope catalog procedure.';
        END
    END TRY
    BEGIN CATCH
        DECLARE @IgnoredSqlCompatibilityTelemetryError int = ERROR_NUMBER();
    END CATCH

    SELECT ModuleKey, AppKey, Instruction, Priority
    FROM dbo.ModuleCatalog
    WHERE IsEnabled = 1
      AND Language = @Language
      AND (TenantId IS NULL OR TenantId = @TenantId)
    ORDER BY Priority ASC;
END;
GO

-- =============================================
-- LEGACY SP: app_toolcatalog_list_scoped
-- Returns tools filtered by selected capability scopes.
-- New callers should use app_toolcatalog_list_by_capability_scope.
-- @ModulesJson is retained for deployed caller compatibility.
-- =============================================
CREATE OR ALTER PROCEDURE dbo.app_toolcatalog_list_scoped
    @TenantId       nvarchar(50),
    @Language        nvarchar(10),
    @DefaultLanguage nvarchar(10) = 'en',
    @ModulesJson     nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF OBJECT_ID('dbo.app_sql_compatibility_usage_record', 'P') IS NOT NULL
        BEGIN
            EXEC dbo.app_sql_compatibility_usage_record
                @SurfaceName = N'app_toolcatalog_list_scoped',
                @SurfaceKind = N'legacy-procedure',
                @ForwardSurfaceName = N'app_toolcatalog_list_by_capability_scope',
                @TenantId = @TenantId,
                @Language = @Language,
                @CompatibilityNotes = N'Legacy tool catalog scope procedure using @ModulesJson.';
        END
    END TRY
    BEGIN CATCH
        DECLARE @IgnoredSqlCompatibilityTelemetryError int = ERROR_NUMBER();
    END CATCH

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
-- LEGACY SP: app_metadatadictionary_list_scoped
-- Returns metadata keys filtered by selected capability scopes.
-- New callers should use app_metadatadictionary_list_by_capability_scope.
-- @ModulesJson is retained for deployed caller compatibility.
-- =============================================
CREATE OR ALTER PROCEDURE dbo.app_metadatadictionary_list_scoped
    @TenantId       nvarchar(50) = NULL,
    @Language        nvarchar(10) = 'en',
    @DefaultLanguage nvarchar(10) = 'en',
    @ModulesJson     nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF OBJECT_ID('dbo.app_sql_compatibility_usage_record', 'P') IS NOT NULL
        BEGIN
            EXEC dbo.app_sql_compatibility_usage_record
                @SurfaceName = N'app_metadatadictionary_list_scoped',
                @SurfaceKind = N'legacy-procedure',
                @ForwardSurfaceName = N'app_metadatadictionary_list_by_capability_scope',
                @TenantId = @TenantId,
                @Language = @Language,
                @CompatibilityNotes = N'Legacy metadata dictionary scope procedure using @ModulesJson.';
        END
    END TRY
    BEGIN CATCH
        DECLARE @IgnoredSqlCompatibilityTelemetryError int = ERROR_NUMBER();
    END CATCH

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
