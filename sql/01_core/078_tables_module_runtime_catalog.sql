-- ============================================================
-- PATCH 37.01 / SPRINT 20: ModuleRuntimeCatalog legacy compatibility surface
-- SPRINT 20: LEGACY COMPATIBILITY ONLY.
-- Production API startup no longer registers a module loader or consumes this catalog.
-- ============================================================

-- Table
IF OBJECT_ID('dbo.ModuleRuntimeCatalog','U') IS NULL
BEGIN
    CREATE TABLE dbo.ModuleRuntimeCatalog(
        ModuleKey      nvarchar(50)  NOT NULL,
        AssemblyName   nvarchar(200) NOT NULL,
        IsEnabled      bit           NOT NULL DEFAULT 1,
        Environment    nvarchar(50)  NULL,
        TenantId       nvarchar(50)  NULL,
        Priority       int           NOT NULL DEFAULT 100,
        CONSTRAINT PK_ModuleRuntimeCatalog
            PRIMARY KEY CLUSTERED (ModuleKey, AssemblyName)
    );

    CREATE NONCLUSTERED INDEX IX_ModuleRuntimeCatalog_Env
        ON dbo.ModuleRuntimeCatalog (Environment, TenantId)
        INCLUDE (IsEnabled, Priority);
END
GO

-- Legacy SP: resolve enabled modules for tenant/env for historical tooling.
CREATE OR ALTER PROCEDURE dbo.app_module_runtime_list
    @TenantId    nvarchar(50) = NULL,
    @Environment nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF OBJECT_ID('dbo.app_sql_compatibility_usage_record', 'P') IS NOT NULL
        BEGIN
            EXEC dbo.app_sql_compatibility_usage_record
                @SurfaceName = N'app_module_runtime_list',
                @SurfaceKind = N'legacy-procedure',
                @TenantId = @TenantId,
                @CompatibilityNotes = N'Legacy package-runtime diagnostic procedure.';
        END
    END TRY
    BEGIN CATCH
        DECLARE @IgnoredSqlCompatibilityTelemetryError int = ERROR_NUMBER();
    END CATCH

    ;WITH cte AS (
        SELECT
            ModuleKey, AssemblyName, IsEnabled, Priority,
            ROW_NUMBER() OVER (
                PARTITION BY ModuleKey, AssemblyName
                ORDER BY
                    CASE WHEN TenantId IS NOT NULL THEN 0 ELSE 1 END,
                    CASE WHEN Environment IS NOT NULL THEN 0 ELSE 1 END,
                    Priority ASC
            ) AS rn
        FROM dbo.ModuleRuntimeCatalog
        WHERE IsEnabled = 1
            AND (@TenantId IS NULL OR TenantId IS NULL OR TenantId = @TenantId)
            AND (@Environment IS NULL OR Environment IS NULL OR Environment = @Environment)
    )
    SELECT ModuleKey, AssemblyName, Priority
    FROM cte
    WHERE rn = 1
    ORDER BY Priority ASC;
END
GO

-- No baseline rows are seeded. Legacy rows may exist in upgraded databases,
-- but new deployments must not activate package projects as runtime modules.

