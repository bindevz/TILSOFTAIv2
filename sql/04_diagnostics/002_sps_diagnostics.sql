SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.app_diagnosticsrule_list
    @TenantId nvarchar(50),
    @Module nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    WITH RankedRules AS
    (
        SELECT
            RuleKey,
            TenantId,
            Module,
            Description,
            AiSpName,
            ROW_NUMBER() OVER (
                PARTITION BY RuleKey
                ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END,
                         UpdatedAtUtc DESC
            ) AS RowNum
        FROM dbo.DiagnosticsRule
        WHERE Module = @Module
          AND IsEnabled = 1
          AND (TenantId = @TenantId OR TenantId IS NULL)
    )
    SELECT
        RuleKey,
        TenantId,
        Module,
        Description,
        AiSpName
    FROM RankedRules
    WHERE RowNum = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_diagnostics_run
    @TenantId nvarchar(50),
    @Module nvarchar(100),
    @RuleKey nvarchar(200),
    @InputJson nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AiSpName nvarchar(200);

    SELECT TOP 1
        @AiSpName = AiSpName
    FROM dbo.DiagnosticsRule
    WHERE RuleKey = @RuleKey
      AND Module = @Module
      AND IsEnabled = 1
      AND (TenantId = @TenantId OR TenantId IS NULL)
    ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END,
             UpdatedAtUtc DESC;

    IF @AiSpName IS NULL
    BEGIN
        RAISERROR('Diagnostics rule not found or disabled.', 16, 1);
        RETURN;
    END;

    IF @AiSpName NOT LIKE 'ai[_]%'
    BEGIN
        RAISERROR('Diagnostics rule must reference an ai_ stored procedure.', 16, 1);
        RETURN;
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE SpName = @AiSpName AND IsEnabled = 1)
    BEGIN
        RAISERROR('Diagnostics rule SP is not enabled in ToolCatalog.', 16, 1);
        RETURN;
    END;

    DECLARE @Sql nvarchar(max) = N'EXEC ' + QUOTENAME(@AiSpName) + N' @TenantId=@TenantId, @InputJson=@InputJson';

    EXEC sp_executesql
        @Sql,
        N'@TenantId nvarchar(50), @InputJson nvarchar(max)',
        @TenantId = @TenantId,
        @InputJson = @InputJson;
END;
GO
