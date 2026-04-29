SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

MERGE dbo.MetadataDictionary AS target
USING (VALUES
    ('Model.Name', CAST(NULL AS nvarchar(50)), 'en', 'Model Name', 'Human-friendly name of a model record.', NULL, 'Example: Alpha-100'),
    ('Model.Description', CAST(NULL AS nvarchar(50)), 'en', 'Model Description', 'Short description of a model record.', NULL, 'Example: Lightweight packaging model')
) AS source([Key], TenantId, Language, DisplayName, Description, Unit, Examples)
ON target.[Key] = source.[Key]
   AND ((target.TenantId = source.TenantId) OR (target.TenantId IS NULL AND source.TenantId IS NULL))
   AND target.Language = source.Language
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Key], TenantId, Language, DisplayName, Description, Unit, Examples)
    VALUES (source.[Key], source.TenantId, source.Language, source.DisplayName, source.Description, source.Unit, source.Examples);
GO
