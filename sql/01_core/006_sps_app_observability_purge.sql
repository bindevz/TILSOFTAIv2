SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- Observability Data Purge Procedure
-- =============================================
-- Purpose: Purge old observability data in batches to avoid long locks
-- Parameters:
--   @RetentionDays: Number of days to retain (default: 30)
--   @BatchSize: Number of records to delete per batch (default: 5000)
--   @TenantId: Optional tenant filter (NULL = all tenants)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.app_observability_purge
    @RetentionDays int = 30,
    @BatchSize int = 5000,
    @TenantId nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CutoffDate datetime2(3) = DATEADD(day, -@RetentionDays, SYSUTCDATETIME());
    DECLARE @DeletedMessages int = 0;
    DECLARE @DeletedToolExecutions int = 0;
    DECLARE @DeletedConversations int = 0;
    DECLARE @DeletedErrors int = 0;
    DECLARE @BatchCount int = 0;
    DECLARE @RowsDeleted int = 1;
    
    -- Purge ConversationMessage in batches
    WHILE @RowsDeleted > 0
    BEGIN
        IF @TenantId IS NOT NULL
        BEGIN
            DELETE TOP (@BatchSize) FROM dbo.ConversationMessage 
            WHERE TenantId = @TenantId AND CreatedAtUtc < @CutoffDate;
        END
        ELSE
        BEGIN
            DELETE TOP (@BatchSize) FROM dbo.ConversationMessage 
            WHERE CreatedAtUtc < @CutoffDate;
        END
        
        SET @RowsDeleted = @@ROWCOUNT;
        SET @DeletedMessages = @DeletedMessages + @RowsDeleted;
        
        IF @RowsDeleted > 0
        BEGIN
            WAITFOR DELAY '00:00:00.100'; -- 100ms delay between batches
        END
    END
    
    -- Purge ToolExecution in batches
    SET @RowsDeleted = 1;
    WHILE @RowsDeleted > 0
    BEGIN
        IF @TenantId IS NOT NULL
        BEGIN
            DELETE TOP (@BatchSize) FROM dbo.ToolExecution 
            WHERE TenantId = @TenantId AND CreatedAtUtc < @CutoffDate;
        END
        ELSE
        BEGIN
            DELETE TOP (@BatchSize) FROM dbo.ToolExecution 
            WHERE CreatedAtUtc < @CutoffDate;
        END
        
        SET @RowsDeleted = @@ROWCOUNT;
        SET @DeletedToolExecutions = @DeletedToolExecutions + @RowsDeleted;
        
        IF @RowsDeleted > 0
        BEGIN
            WAITFOR DELAY '00:00:00.100';
        END
    END
    
    -- Purge ErrorLog in batches
    SET @RowsDeleted = 1;
    WHILE @RowsDeleted > 0
    BEGIN
        IF @TenantId IS NOT NULL
        BEGIN
            DELETE TOP (@BatchSize) FROM dbo.ErrorLog 
            WHERE TenantId = @TenantId AND CreatedAtUtc < @CutoffDate;
        END
        ELSE
        BEGIN
            DELETE TOP (@BatchSize) FROM dbo.ErrorLog 
            WHERE CreatedAtUtc < @CutoffDate;
        END
        
        SET @RowsDeleted = @@ROWCOUNT;
        SET @DeletedErrors = @DeletedErrors + @RowsDeleted;
        
        IF @RowsDeleted > 0
        BEGIN
            WAITFOR DELAY '00:00:00.100';
        END
    END
    
    -- Purge Conversation in batches (after child records are deleted)
    SET @RowsDeleted = 1;
    WHILE @RowsDeleted > 0
    BEGIN
        IF @TenantId IS NOT NULL
        BEGIN
            DELETE TOP (@BatchSize) FROM dbo.Conversation 
            WHERE TenantId = @TenantId AND CreatedAtUtc < @CutoffDate;
        END
        ELSE
        BEGIN
            DELETE TOP (@BatchSize) FROM dbo.Conversation 
            WHERE CreatedAtUtc < @CutoffDate;
        END
        
        SET @RowsDeleted = @@ROWCOUNT;
        SET @DeletedConversations = @DeletedConversations + @RowsDeleted;
        
        IF @RowsDeleted > 0
        BEGIN
            WAITFOR DELAY '00:00:00.100';
        END
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
