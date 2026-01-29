SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_create
    @TenantId nvarchar(50),
    @ConversationId nvarchar(64),
    @ProposedToolName nvarchar(200),
    @ProposedSpName nvarchar(200),
    @ArgsJson nvarchar(max),
    @RequestedByUserId nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ActionId nvarchar(64) = REPLACE(CONVERT(nvarchar(36), NEWID()), '-', '');

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
        @ArgsJson,
        @RequestedByUserId
    );

    SELECT *
    FROM dbo.ActionRequest
    WHERE ActionId = @ActionId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_get
    @TenantId nvarchar(50),
    @ActionId nvarchar(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM dbo.ActionRequest
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_approve
    @TenantId nvarchar(50),
    @ActionId nvarchar(64),
    @ApprovedByUserId nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ActionRequest
    SET Status = 'Approved',
        ApprovedByUserId = @ApprovedByUserId,
        ApprovedAtUtc = sysutcdatetime()
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId
      AND Status = 'Pending';

    SELECT *
    FROM dbo.ActionRequest
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_reject
    @TenantId nvarchar(50),
    @ActionId nvarchar(64),
    @ApprovedByUserId nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ActionRequest
    SET Status = 'Rejected',
        ApprovedByUserId = @ApprovedByUserId,
        ApprovedAtUtc = sysutcdatetime()
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId
      AND Status = 'Pending';

    SELECT *
    FROM dbo.ActionRequest
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_mark_executed
    @TenantId nvarchar(50),
    @ActionId nvarchar(64),
    @ResultCompactJson nvarchar(max),
    @Success bit
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ActionRequest
    SET Status = CASE WHEN @Success = 1 THEN 'Executed' ELSE 'Failed' END,
        ExecutedAtUtc = sysutcdatetime(),
        ExecutionResultCompactJson = @ResultCompactJson
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId
      AND Status = 'Approved';

    SELECT *
    FROM dbo.ActionRequest
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId;
END;
GO
