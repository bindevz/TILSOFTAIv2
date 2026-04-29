SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT N'Dropping only allowlisted TILSOFTAI-owned framework procedures/views that are recreated by sql/current.';
GO

DECLARE @FrameworkObjects TABLE
(
    SchemaName sysname NOT NULL,
    ObjectName sysname NOT NULL,
    ObjectType char(2) NOT NULL,
    DropCommand nvarchar(20) NOT NULL
);

INSERT INTO @FrameworkObjects (SchemaName, ObjectName, ObjectType, DropCommand)
VALUES
    (N'dbo', N'ai_model_compare_models', N'P', N'PROCEDURE'),
    (N'dbo', N'ai_model_compare', N'P', N'PROCEDURE'),
    (N'dbo', N'ai_model_count', N'P', N'PROCEDURE'),
    (N'dbo', N'ai_model_get_overview', N'P', N'PROCEDURE'),
    (N'dbo', N'ai_model_get_pieces', N'P', N'PROCEDURE'),
    (N'dbo', N'ai_model_get_materials', N'P', N'PROCEDURE'),
    (N'dbo', N'ai_model_get_packaging', N'P', N'PROCEDURE'),
    (N'dbo', N'app_actionrequest_create', N'P', N'PROCEDURE'),
    (N'dbo', N'app_actionrequest_get', N'P', N'PROCEDURE'),
    (N'dbo', N'app_actionrequest_get_active_for_conversation', N'P', N'PROCEDURE'),
    (N'dbo', N'app_actionrequest_confirm', N'P', N'PROCEDURE'),
    (N'dbo', N'app_actionrequest_approve', N'P', N'PROCEDURE'),
    (N'dbo', N'app_actionrequest_reject', N'P', N'PROCEDURE'),
    (N'dbo', N'app_actionrequest_mark_executed', N'P', N'PROCEDURE'),
    (N'dbo', N'app_actionrequest_expire_old', N'P', N'PROCEDURE'),
    (N'dbo', N'app_agent_trace_recent', N'P', N'PROCEDURE'),
    (N'dbo', N'app_agent_trace_by_correlation', N'P', N'PROCEDURE'),
    (N'dbo', N'app_agent_trace_failures', N'P', N'PROCEDURE'),
    (N'dbo', N'app_model_runtime_health', N'P', N'PROCEDURE'),
    (N'dbo', N'app_model_runtime_diagnostics', N'P', N'PROCEDURE'),
    (N'dbo', N'vw_ModelSemantic', N'V', N'VIEW');

DECLARE @SchemaName sysname;
DECLARE @ObjectName sysname;
DECLARE @ObjectType char(2);
DECLARE @DropCommand nvarchar(20);
DECLARE @Sql nvarchar(max);

DECLARE framework_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT SchemaName, ObjectName, ObjectType, DropCommand
FROM @FrameworkObjects
ORDER BY CASE WHEN ObjectType = N'P' THEN 0 ELSE 1 END, SchemaName, ObjectName;

OPEN framework_cursor;
FETCH NEXT FROM framework_cursor INTO @SchemaName, @ObjectName, @ObjectType, @DropCommand;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.objects o
        JOIN sys.schemas s ON s.schema_id = o.schema_id
        WHERE s.name = @SchemaName
          AND o.name = @ObjectName
          AND o.[type] = @ObjectType
    )
    BEGIN
        SET @Sql = N'DROP ' + @DropCommand + N' ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@ObjectName) + N';';
        PRINT N'Dropping framework object ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@ObjectName);
        EXEC sys.sp_executesql @Sql;
    END;

    FETCH NEXT FROM framework_cursor INTO @SchemaName, @ObjectName, @ObjectType, @DropCommand;
END;

CLOSE framework_cursor;
DEALLOCATE framework_cursor;
GO

PRINT N'Project-owned procedure/view cleanup complete. No ERP source tables were dropped.';
GO
