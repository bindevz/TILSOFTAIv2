SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.ai_tool_list
    @TenantId nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @RowCount int = (SELECT COUNT(1) FROM dbo.ToolCatalog WHERE IsEnabled = 1);

    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS rowCount
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            columns = (
                SELECT
                    [name],
                    [type],
                    [descriptionKey]
                FROM (VALUES
                    ('ToolName', 'nvarchar(200)', NULL),
                    ('SpName', 'nvarchar(200)', NULL),
                    ('Description', 'nvarchar(2000)', NULL),
                    ('JsonSchema', 'nvarchar(max)', NULL),
                    ('Instruction', 'nvarchar(max)', NULL),
                    ('RequiredRoles', 'nvarchar(1000)', NULL)
                ) AS columns([name], [type], [descriptionKey])
                FOR JSON PATH
            ),
            rows = (
                SELECT
                    ToolName,
                    SpName,
                    Description,
                    JsonSchema,
                    Instruction,
                    RequiredRoles
                FROM dbo.ToolCatalog
                WHERE IsEnabled = 1
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO
