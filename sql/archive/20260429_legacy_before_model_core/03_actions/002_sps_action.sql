SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_create
    @TenantId nvarchar(50),
    @ConversationId nvarchar(64),
    @ProposedToolName nvarchar(200),
    @ProposedSpName nvarchar(200),
    @ArgsJson nvarchar(max),
    @RequestedByUserId nvarchar(50),
    @UserId nvarchar(50) = NULL,
    @CapabilityKey nvarchar(200) = NULL,
    @FunctionName nvarchar(200) = NULL,
    @PreviewResultJson nvarchar(max) = NULL,
    @ExpiresAtUtc datetime2(3) = NULL,
    @CorrelationId nvarchar(100) = NULL,
    @MetadataJson nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ActionId nvarchar(64) = REPLACE(CONVERT(nvarchar(36), NEWID()), '-', '');
    DECLARE @NowUtc datetime2(3) = sysutcdatetime();
    DECLARE @EffectiveUserId nvarchar(50) = COALESCE(NULLIF(@UserId, ''), @RequestedByUserId);

    INSERT INTO dbo.ActionRequest
    (
        ActionId,
        TenantId,
        UserId,
        ConversationId,
        RequestedAtUtc,
        ExpiresAtUtc,
        Status,
        CapabilityKey,
        FunctionName,
        ProposedToolName,
        ProposedSpName,
        ArgsJson,
        PreviewResultJson,
        RequestedByUserId,
        CorrelationId,
        MetadataJson
    )
    VALUES
    (
        @ActionId,
        @TenantId,
        @EffectiveUserId,
        @ConversationId,
        @NowUtc,
        COALESCE(@ExpiresAtUtc, DATEADD(hour, 24, @NowUtc)),
        'Pending',
        COALESCE(NULLIF(@CapabilityKey, ''), @ProposedToolName),
        COALESCE(NULLIF(@FunctionName, ''), @ProposedToolName),
        @ProposedToolName,
        @ProposedSpName,
        @ArgsJson,
        @PreviewResultJson,
        @RequestedByUserId,
        @CorrelationId,
        @MetadataJson
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

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_get_active_for_conversation
    @TenantId nvarchar(50),
    @UserId nvarchar(50),
    @ConversationId nvarchar(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1) *
    FROM dbo.ActionRequest
    WHERE TenantId = @TenantId
      AND COALESCE(UserId, RequestedByUserId) = @UserId
      AND ConversationId = @ConversationId
      AND Status = 'Pending'
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > sysutcdatetime())
    ORDER BY RequestedAtUtc DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_confirm
    @TenantId nvarchar(50),
    @UserId nvarchar(50),
    @ActionId nvarchar(64),
    @Reason nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ActionRequest
    SET Status = 'Confirmed',
        ConfirmedAtUtc = sysutcdatetime()
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId
      AND COALESCE(UserId, RequestedByUserId) = @UserId
      AND Status = 'Pending'
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > sysutcdatetime());

    SELECT *
    FROM dbo.ActionRequest
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId
      AND COALESCE(UserId, RequestedByUserId) = @UserId;
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
      AND Status IN ('Pending', 'Confirmed')
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > sysutcdatetime());

    SELECT *
    FROM dbo.ActionRequest
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_reject
    @TenantId nvarchar(50),
    @UserId nvarchar(50),
    @ActionId nvarchar(64),
    @Reason nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ActionRequest
    SET Status = 'Rejected',
        CancelledAtUtc = sysutcdatetime(),
        MetadataJson = CASE
            WHEN @Reason IS NULL OR @Reason = '' THEN MetadataJson
            WHEN ISJSON(COALESCE(MetadataJson, '{}')) = 1 THEN JSON_MODIFY(COALESCE(MetadataJson, '{}'), '$.rejectReason', @Reason)
            ELSE JSON_MODIFY('{}', '$.rejectReason', @Reason)
        END
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId
      AND Status IN ('Pending', 'Confirmed');

    SELECT *
    FROM dbo.ActionRequest
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_mark_executed
    @TenantId nvarchar(50),
    @ActionId nvarchar(64),
    @ResultCompactJson nvarchar(max) = NULL,
    @Success bit = 1,
    @ExecutedByUserId nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ActionRequest
    SET Status = CASE WHEN @Success = 1 THEN 'Executed' ELSE 'Failed' END,
        ExecutedAtUtc = sysutcdatetime(),
        ExecutedByUserId = @ExecutedByUserId,
        ExecutionResultCompactJson = @ResultCompactJson
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId
      AND Status = 'Approved'
      AND ExecutedAtUtc IS NULL
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > sysutcdatetime());

    SELECT *
    FROM dbo.ActionRequest
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_expire_old
    @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ActionRequest
    SET Status = 'Expired'
    WHERE Status IN ('Pending', 'Confirmed')
      AND ExpiresAtUtc IS NOT NULL
      AND ExpiresAtUtc <= @NowUtc;

    SELECT @@ROWCOUNT;
END;
GO
