-- ============================================================
-- Patch 35.01: app_policy_resolve stored procedure
-- Returns best-priority policy per PolicyKey for a given scope.
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.app_policy_resolve
    @TenantId        nvarchar(50),
    @ModuleKeysJson  nvarchar(max) = NULL,      -- JSON array e.g. '["model","analytics"]'
    @AppKey          nvarchar(50) = NULL,
    @Environment     nvarchar(50) = NULL,
    @Language        nvarchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ModuleKeys TABLE (ModuleKey nvarchar(50));
    IF (@ModuleKeysJson IS NOT NULL AND ISJSON(@ModuleKeysJson) = 1)
    BEGIN
        INSERT INTO @ModuleKeys (ModuleKey)
        SELECT value FROM OPENJSON(@ModuleKeysJson);
    END

    ;WITH Candidates AS
    (
        SELECT
            PolicyKey,
            JsonValue,
            Priority,
            ROW_NUMBER() OVER (
                PARTITION BY PolicyKey
                ORDER BY Priority ASC
            ) AS rn
        FROM dbo.RuntimePolicy
        WHERE
            IsEnabled = 1
            AND (TenantId IS NULL OR TenantId = @TenantId)
            AND (@AppKey IS NULL OR AppKey IS NULL OR AppKey = @AppKey)
            AND (@Environment IS NULL OR Environment IS NULL OR Environment = @Environment)
            AND (@Language IS NULL OR Language IS NULL OR Language = @Language)
            AND (
                ModuleKey IS NULL
                OR EXISTS (SELECT 1 FROM @ModuleKeys mk WHERE mk.ModuleKey = RuntimePolicy.ModuleKey)
            )
    )
    SELECT PolicyKey, JsonValue
    FROM Candidates
    WHERE rn = 1
    ORDER BY PolicyKey;
END
GO
