-- ============================================================
-- PATCH 36.04: app_policy_resolve — deterministic precedence
-- ORDER BY: SpecificityScore DESC, Priority ASC, UpdatedAtUtc DESC, PolicyId DESC
-- Specificity: TenantId=16, ModuleKey=8, AppKey=4, Environment=2, Language=1
-- @ModuleKeysJson is retained as the legacy SQL parameter for capability scopes.
-- ============================================================
IF OBJECT_ID('dbo.app_policy_resolve', 'P') IS NOT NULL
    DROP PROCEDURE dbo.app_policy_resolve;
GO

CREATE PROCEDURE dbo.app_policy_resolve
    @TenantId        nvarchar(50),
    @ModuleKeysJson  nvarchar(max) = NULL,
    @AppKey          nvarchar(50) = NULL,
    @Environment     nvarchar(50) = NULL,
    @Language        nvarchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF OBJECT_ID('dbo.app_sql_compatibility_usage_record', 'P') IS NOT NULL
        BEGIN
            EXEC dbo.app_sql_compatibility_usage_record
                @SurfaceName = N'app_policy_resolve',
                @SurfaceKind = N'legacy-procedure',
                @ForwardSurfaceName = N'app_policy_resolve_by_capability_scope',
                @TenantId = @TenantId,
                @AppKey = @AppKey,
                @Language = @Language,
                @CompatibilityNotes = N'Legacy runtime policy procedure using @ModuleKeysJson.';
        END
    END TRY
    BEGIN CATCH
        DECLARE @IgnoredSqlCompatibilityTelemetryError int = ERROR_NUMBER();
    END CATCH

    DECLARE @ModuleKeys TABLE (ModuleKey nvarchar(50));
    IF (@ModuleKeysJson IS NOT NULL AND ISJSON(@ModuleKeysJson) = 1)
    BEGIN
        INSERT INTO @ModuleKeys (ModuleKey)
        SELECT value FROM OPENJSON(@ModuleKeysJson);
    END

    ;WITH Candidates AS
    (
        SELECT
            PolicyId,
            PolicyKey,
            JsonValue,
            Priority,
            UpdatedAtUtc,
            -- specificity: higher is more specific
            (CASE WHEN TenantId = @TenantId THEN 16 ELSE 0 END) +
            (CASE WHEN ModuleKey IS NOT NULL THEN 8 ELSE 0 END) +
            (CASE WHEN AppKey IS NOT NULL THEN 4 ELSE 0 END) +
            (CASE WHEN Environment IS NOT NULL THEN 2 ELSE 0 END) +
            (CASE WHEN Language IS NOT NULL THEN 1 ELSE 0 END) AS SpecificityScore
        FROM dbo.RuntimePolicy
        WHERE
            IsEnabled = 1
            AND (TenantId IS NULL OR TenantId = @TenantId)
            AND (@AppKey IS NULL OR AppKey IS NULL OR AppKey = @AppKey)
            AND (@Environment IS NULL OR Environment IS NULL OR Environment = @Environment)
            AND (@Language IS NULL OR Language IS NULL OR Language = @Language)
            AND (
                ModuleKey IS NULL
                OR EXISTS (SELECT 1 FROM @ModuleKeys mk WHERE mk.ModuleKey = ModuleKey)
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
END
GO
