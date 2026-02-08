SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.app_normalizationrule_lint
    @TenantId nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH Candidate AS
    (
        SELECT
            Id, RuleKey, TenantId, Priority, Pattern, Replacement, IsEnabled, UpdatedAtUtc,
            CASE
                WHEN IsEnabled = 1
                 AND (Replacement IS NULL OR LTRIM(RTRIM(Replacement)) = N'')
                 AND (
                      Pattern LIKE N'%\s%' ESCAPE N'\'
                   OR Pattern LIKE N'%\p{Z%' ESCAPE N'\'
                   OR Pattern LIKE N'%[\s%' ESCAPE N'\'
                   OR Pattern LIKE N'%\t%' ESCAPE N'\'
                   OR Pattern LIKE N'%\r%' ESCAPE N'\'
                   OR Pattern LIKE N'%\n%' ESCAPE N'\'
                 )
                THEN 1 ELSE 0
            END AS IsUnsafeWhitespaceStrip
        FROM dbo.NormalizationRule
        WHERE IsEnabled = 1 AND (TenantId = @TenantId OR TenantId IS NULL)
    )
    SELECT
        Id, RuleKey, TenantId, Priority, Pattern, Replacement, UpdatedAtUtc,
        IsUnsafeWhitespaceStrip
    FROM Candidate
    WHERE IsUnsafeWhitespaceStrip = 1
    ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END, Priority ASC;
END;
GO
