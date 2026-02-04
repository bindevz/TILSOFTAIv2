SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- Patch 26.04: Create ai_action_request_write SP
-- 
-- This SP provides a model-callable interface for creating action requests.
-- The actual approval/execution flow is handled by ActionApprovalService.
-- =============================================

-- ai_action_request_write
-- Creates a pending action request for human approval.
-- This is a model-callable wrapper around the ActionRequest workflow.

CREATE OR ALTER PROCEDURE dbo.ai_action_request_write
    @TenantId nvarchar(50),
    @ArgsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Validate JSON
    IF ISJSON(@ArgsJson) <> 1
    BEGIN
        RAISERROR('@ArgsJson must be valid JSON.', 16, 1);
        RETURN;
    END
    
    -- Extract parameters
    DECLARE @ProposedToolName nvarchar(200) = JSON_VALUE(@ArgsJson, '$.proposedToolName');
    DECLARE @ProposedSpName nvarchar(200) = JSON_VALUE(@ArgsJson, '$.proposedSpName');
    DECLARE @InnerArgsJson nvarchar(max) = JSON_VALUE(@ArgsJson, '$.argsJson');
    
    -- Validate required parameters
    IF @ProposedToolName IS NULL OR LTRIM(RTRIM(@ProposedToolName)) = ''
    BEGIN
        RAISERROR('proposedToolName is required.', 16, 1);
        RETURN;
    END
    
    IF @ProposedSpName IS NULL OR LTRIM(RTRIM(@ProposedSpName)) = ''
    BEGIN
        RAISERROR('proposedSpName is required.', 16, 1);
        RETURN;
    END
    
    IF @InnerArgsJson IS NULL
    BEGIN
        SET @InnerArgsJson = '{}';
    END
    
    -- Security check: ProposedSpName must NOT start with ai_ (write actions use app_/other SPs)
    IF @ProposedSpName LIKE 'ai[_]%'
    BEGIN
        RAISERROR('Write actions must not target ai_ stored procedures.', 16, 1);
        RETURN;
    END
    
    -- Check if ProposedSpName is in WriteActionCatalog
    IF NOT EXISTS (
        SELECT 1 FROM dbo.WriteActionCatalog 
        WHERE TenantId = @TenantId 
          AND SpName = @ProposedSpName 
          AND IsEnabled = 1
    )
    BEGIN
        RAISERROR('Proposed SP is not in the WriteActionCatalog or is disabled.', 16, 1);
        RETURN;
    END
    
    -- Generate action ID
    DECLARE @ActionId nvarchar(64) = REPLACE(CONVERT(nvarchar(36), NEWID()), '-', '');
    DECLARE @ConversationId nvarchar(64) = JSON_VALUE(@ArgsJson, '$.conversationId');
    DECLARE @RequestedByUserId nvarchar(50) = JSON_VALUE(@ArgsJson, '$.userId');
    
    -- Insert action request
    INSERT INTO dbo.ActionRequest
    (
        ActionId,
        TenantId,
        ConversationId,
        Status,
        ProposedToolName,
        ProposedSpName,
        ArgsJson,
        RequestedByUserId
    )
    VALUES
    (
        @ActionId,
        @TenantId,
        @ConversationId,
        'Pending',
        @ProposedToolName,
        @ProposedSpName,
        @InnerArgsJson,
        @RequestedByUserId
    );
    
    -- Return JSON response
    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    
    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    1 AS [rowCount]
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            columns = (
                SELECT [name], [type]
                FROM (VALUES
                    ('ActionId', 'nvarchar(64)'),
                    ('Status', 'nvarchar(50)'),
                    ('ProposedToolName', 'nvarchar(200)'),
                    ('ProposedSpName', 'nvarchar(200)'),
                    ('Message', 'nvarchar(max)')
                ) AS c([name], [type])
                FOR JSON PATH
            ),
            rows = (
                SELECT 
                    @ActionId AS ActionId,
                    'Pending' AS Status,
                    @ProposedToolName AS ProposedToolName,
                    @ProposedSpName AS ProposedSpName,
                    'Action request created successfully. Pending human approval.' AS Message
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO
