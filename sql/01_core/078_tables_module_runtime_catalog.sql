-- ============================================================
-- PATCH 37.01: ModuleRuntimeCatalog — DB-driven module activation
-- Supports tenant/env enablement with priority-based resolution.
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

-- SP: resolve enabled modules for tenant/env
CREATE OR ALTER PROCEDURE dbo.app_module_runtime_list
    @TenantId    nvarchar(50) = NULL,
    @Environment nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

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

-- Seed baseline modules
IF NOT EXISTS (SELECT 1 FROM dbo.ModuleRuntimeCatalog WHERE ModuleKey = 'platform' AND TenantId IS NULL AND Environment IS NULL)
BEGIN
    INSERT INTO dbo.ModuleRuntimeCatalog (ModuleKey, AssemblyName, IsEnabled, Environment, TenantId, Priority)
    VALUES
        ('platform', 'TILSOFTAI.Modules.Platform', 1, NULL, NULL, 0),
        ('model',    'TILSOFTAI.Modules.Model',    1, NULL, NULL, 10),
        ('analytics','TILSOFTAI.Modules.Analytics', 1, NULL, NULL, 20);
END
GO
