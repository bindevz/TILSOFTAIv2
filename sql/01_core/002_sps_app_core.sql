SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.app_toolcatalog_list
    @TenantId nvarchar(50),
    @Language nvarchar(10),
    @DefaultLanguage nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tool.ToolName,
        tool.SpName,
        tool.IsEnabled,
        tool.RequiredRoles,
        tool.JsonSchema,
        COALESCE(lang.Instruction, fallback.Instruction, tool.Instruction) AS Instruction,
        COALESCE(lang.Description, fallback.Description, tool.Description) AS Description,
        tool.UpdatedAtUtc
    FROM dbo.ToolCatalog AS tool
    LEFT JOIN dbo.ToolCatalogTranslation AS lang
        ON lang.ToolName = tool.ToolName
        AND lang.Language = @Language
    LEFT JOIN dbo.ToolCatalogTranslation AS fallback
        ON fallback.ToolName = tool.ToolName
        AND fallback.Language = @DefaultLanguage
    WHERE tool.IsEnabled = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_metadatadictionary_list
    @TenantId nvarchar(50),
    @Language nvarchar(10),
    @DefaultLanguage nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON;

    WITH Ranked AS
    (
        SELECT
            [Key],
            TenantId,
            Language,
            DisplayName,
            Description,
            Unit,
            Examples,
            UpdatedAtUtc,
            ROW_NUMBER() OVER (
                PARTITION BY [Key]
                ORDER BY CASE
                    WHEN TenantId = @TenantId AND Language = @Language THEN 1
                    WHEN TenantId = @TenantId AND Language = @DefaultLanguage THEN 2
                    WHEN TenantId IS NULL AND Language = @Language THEN 3
                    WHEN TenantId IS NULL AND Language = @DefaultLanguage THEN 4
                    ELSE 5
                END
            ) AS RowRank
        FROM dbo.MetadataDictionary
        WHERE
            (TenantId = @TenantId OR TenantId IS NULL)
            AND (Language = @Language OR Language = @DefaultLanguage)
    )
    SELECT
        [Key],
        TenantId,
        Language,
        DisplayName,
        Description,
        Unit,
        Examples,
        UpdatedAtUtc
    FROM Ranked
    WHERE RowRank = 1
    ORDER BY [Key];
END;
GO
