-- ============================================================================
-- TEMPLATE FILE - DO NOT DEPLOY
-- This file is a template for creating new AI stored procedures. Copy this file
-- to a new module folder and replace <module> with your module name.
-- ============================================================================

PRINT 'Template file: 90_template_module.002_sps_ai_template.sql - skipping (template only)';
GO

/*
-- Template Example - Uncomment and replace <module> when ready

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.ai_<module>_search
    @TenantId nvarchar(50),
    @Query nvarchar(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();

    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    0 AS [rowCount]
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            columns = (
                SELECT
                    [name],
                    [type],
                    [descriptionKey]
                FROM (VALUES
                    ('ItemCode', 'nvarchar(50)', NULL),
                    ('ItemName', 'nvarchar(200)', NULL)
                ) AS columns([name], [type], [descriptionKey])
                FOR JSON PATH
            ),
            rows = (
                SELECT
                    CAST(NULL AS nvarchar(50)) AS ItemCode,
                    CAST(NULL AS nvarchar(200)) AS ItemName
                WHERE 1 = 0
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO
*/
