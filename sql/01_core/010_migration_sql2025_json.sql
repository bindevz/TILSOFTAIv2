SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @MajorVersion int = TRY_CONVERT(int, SERVERPROPERTY('ProductMajorVersion'));

IF @MajorVersion IS NULL OR @MajorVersion < 17
BEGIN
    PRINT 'SQL 2025 JSON type not available. Skipping JSON column migration.';
    RETURN;
END;

BEGIN TRY
    IF COL_LENGTH('dbo.ToolExecution', 'ArgumentsJson') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.ToolExecution')
              AND c.name = 'ArgumentsJson'
              AND t.name = 'json')
        BEGIN
            EXEC('ALTER TABLE dbo.ToolExecution ALTER COLUMN ArgumentsJson json NOT NULL');
        END
    END
END TRY
BEGIN CATCH
    PRINT 'Failed to alter ToolExecution.ArgumentsJson to json.';
END CATCH;

BEGIN TRY
    IF COL_LENGTH('dbo.ToolExecution', 'ResultJson') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.ToolExecution')
              AND c.name = 'ResultJson'
              AND t.name = 'json')
        BEGIN
            EXEC('ALTER TABLE dbo.ToolExecution ALTER COLUMN ResultJson json NULL');
        END
    END
END TRY
BEGIN CATCH
    PRINT 'Failed to alter ToolExecution.ResultJson to json.';
END CATCH;

BEGIN TRY
    IF COL_LENGTH('dbo.ToolExecution', 'CompactedResultJson') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.ToolExecution')
              AND c.name = 'CompactedResultJson'
              AND t.name = 'json')
        BEGIN
            EXEC('ALTER TABLE dbo.ToolExecution ALTER COLUMN CompactedResultJson json NULL');
        END
    END
END TRY
BEGIN CATCH
    PRINT 'Failed to alter ToolExecution.CompactedResultJson to json.';
END CATCH;

BEGIN TRY
    IF COL_LENGTH('dbo.ErrorLog', 'DetailJson') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.ErrorLog')
              AND c.name = 'DetailJson'
              AND t.name = 'json')
        BEGIN
            EXEC('ALTER TABLE dbo.ErrorLog ALTER COLUMN DetailJson json NULL');
        END
    END
END TRY
BEGIN CATCH
    PRINT 'Failed to alter ErrorLog.DetailJson to json.';
END CATCH;

BEGIN TRY
    IF COL_LENGTH('dbo.ActionRequest', 'ArgsJson') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.ActionRequest')
              AND c.name = 'ArgsJson'
              AND t.name = 'json')
        BEGIN
            EXEC('ALTER TABLE dbo.ActionRequest ALTER COLUMN ArgsJson json NOT NULL');
        END
    END
END TRY
BEGIN CATCH
    PRINT 'Failed to alter ActionRequest.ArgsJson to json.';
END CATCH;

BEGIN TRY
    IF COL_LENGTH('dbo.ActionRequest', 'ExecutionResultCompactJson') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.ActionRequest')
              AND c.name = 'ExecutionResultCompactJson'
              AND t.name = 'json')
        BEGIN
            EXEC('ALTER TABLE dbo.ActionRequest ALTER COLUMN ExecutionResultCompactJson json NULL');
        END
    END
END TRY
BEGIN CATCH
    PRINT 'Failed to alter ActionRequest.ExecutionResultCompactJson to json.';
END CATCH;

BEGIN TRY
    IF COL_LENGTH('dbo.ToolExecution', 'ToolNameJson') IS NULL
    BEGIN
        ALTER TABLE dbo.ToolExecution
            ADD ToolNameJson AS JSON_VALUE(ArgumentsJson, '$.toolName') PERSISTED;
    END
END TRY
BEGIN CATCH
    PRINT 'Failed to add ToolExecution.ToolNameJson computed column.';
END CATCH;

BEGIN TRY
    IF COL_LENGTH('dbo.ErrorLog', 'ErrorCodeJson') IS NULL
    BEGIN
        ALTER TABLE dbo.ErrorLog
            ADD ErrorCodeJson AS JSON_VALUE(DetailJson, '$.errorCode') PERSISTED;
    END
END TRY
BEGIN CATCH
    PRINT 'Failed to add ErrorLog.ErrorCodeJson computed column.';
END CATCH;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ToolExecution_Tenant_ToolNameJson' AND object_id = OBJECT_ID('dbo.ToolExecution'))
BEGIN
    CREATE INDEX IX_ToolExecution_Tenant_ToolNameJson
        ON dbo.ToolExecution (TenantId, ToolNameJson, CreatedAtUtc);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ErrorLog_Tenant_ErrorCodeJson' AND object_id = OBJECT_ID('dbo.ErrorLog'))
BEGIN
    CREATE INDEX IX_ErrorLog_Tenant_ErrorCodeJson
        ON dbo.ErrorLog (TenantId, ErrorCodeJson, CreatedAtUtc);
END;
GO
