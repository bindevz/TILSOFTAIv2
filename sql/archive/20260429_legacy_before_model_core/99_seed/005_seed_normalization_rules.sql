SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

MERGE dbo.NormalizationRule AS target
USING (VALUES
    (N'season_two_digit_year', CAST(NULL AS nvarchar(50)), 10, N'(?<!\d)(\d{2})\s*/\s*(\d{2})(?!\d)', N'__SEASON_2DIGIT__', N'Marks patterns like 24/25 to be expanded by C# post-processor (not simple regex replace).'),
    (N'collapse_whitespace', CAST(NULL AS nvarchar(50)), 100, N'\s+', N' ', N'Normalize whitespace.')
) AS source (RuleKey, TenantId, Priority, Pattern, Replacement, Description)
ON target.RuleKey = source.RuleKey
   AND ((target.TenantId IS NULL AND source.TenantId IS NULL) OR target.TenantId = source.TenantId)
WHEN MATCHED THEN
    UPDATE SET
        target.Priority = source.Priority,
        target.Pattern = source.Pattern,
        target.Replacement = source.Replacement,
        target.Description = source.Description,
        target.IsEnabled = 1,
        target.UpdatedAtUtc = sysutcdatetime()
WHEN NOT MATCHED THEN
    INSERT (RuleKey, TenantId, Priority, Pattern, Replacement, Description, IsEnabled)
    VALUES (source.RuleKey, source.TenantId, source.Priority, source.Pattern, source.Replacement, source.Description, 1);
GO
