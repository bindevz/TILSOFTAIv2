SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.app_observability_purge
    @TenantId nvarchar(50) = NULL,
    @OlderThanDays int = 30
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CutoffDate datetime2(3) = DATEADD(day, -@OlderThanDays, sysutcdatetime());
    DECLARE @DeletedMessages int = 0;
    DECLARE @DeletedToolExecutions int = 0;
    DECLARE @DeletedConversations int = 0;
    DECLARE @DeletedErrors int = 0;
    
    IF @TenantId IS NOT NULL
    BEGIN
        -- Delete messages older than cutoff date for specific tenant
        DELETE FROM dbo.ConversationMessage 
        WHERE TenantId = @TenantId AND CreatedAtUtc < @CutoffDate;
        SET @DeletedMessages = @@ROWCOUNT;
        
        -- Delete tool executions older than cutoff date for specific tenant
        DELETE FROM dbo.ToolExecution 
        WHERE TenantId = @TenantId AND CreatedAtUtc < @CutoffDate;
        SET @DeletedToolExecutions = @@ROWCOUNT;
        
        -- Delete conversations older than cutoff date for specific tenant
        DELETE FROM dbo.Conversation 
        WHERE TenantId = @TenantId AND CreatedAtUtc < @CutoffDate;
        SET @DeletedConversations = @@ROWCOUNT;
        
        -- Delete error logs older than cutoff date for specific tenant
        DELETE FROM dbo.ErrorLog 
        WHERE TenantId = @TenantId AND CreatedAtUtc < @CutoffDate;
        SET @DeletedErrors = @@ROWCOUNT;
    END
    ELSE
    BEGIN
        -- Purge all tenants
        DELETE FROM dbo.ConversationMessage WHERE CreatedAtUtc < @CutoffDate;
        SET @DeletedMessages = @@ROWCOUNT;
        
        DELETE FROM dbo.ToolExecution WHERE CreatedAtUtc < @CutoffDate;
        SET @DeletedToolExecutions = @@ROWCOUNT;
        
        DELETE FROM dbo.Conversation WHERE CreatedAtUtc < @CutoffDate;
        SET @DeletedConversations = @@ROWCOUNT;
        
        DELETE FROM dbo.ErrorLog WHERE CreatedAtUtc < @CutoffDate;
        SET @DeletedErrors = @@ROWCOUNT;
    END
    
    -- Return summary of deletions
    SELECT 
        @CutoffDate AS CutoffDate,
        @DeletedMessages AS DeletedMessages,
        @DeletedToolExecutions AS DeletedToolExecutions,
        @DeletedConversations AS DeletedConversations,
        @DeletedErrors AS DeletedErrors,
        (@DeletedMessages + @DeletedToolExecutions + @DeletedConversations + @DeletedErrors) AS TotalDeleted;
END;
GO
