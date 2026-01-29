SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.app_normalizationrule_list
    @TenantId nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    WITH RankedRules AS
    (
        SELECT
            RuleKey,
            TenantId,
            Priority,
            Pattern,
            Replacement,
            ROW_NUMBER() OVER (
                PARTITION BY RuleKey
                ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END,
                         Priority ASC
            ) AS RowNum
        FROM dbo.NormalizationRule
        WHERE IsEnabled = 1
          AND (TenantId = @TenantId OR TenantId IS NULL)
    )
    SELECT
        RuleKey,
        TenantId,
        Priority,
        Pattern,
        Replacement
    FROM RankedRules
    WHERE RowNum = 1
    ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END,
             Priority ASC;
END;
GO
