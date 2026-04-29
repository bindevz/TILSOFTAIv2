SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.ActionRequest', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ActionRequest
    (
        ActionId nvarchar(64) NOT NULL,
        TenantId nvarchar(50) NOT NULL,
        UserId nvarchar(50) NULL,
        ConversationId nvarchar(64) NOT NULL,
        RequestedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ActionRequest_RequestedAtUtc DEFAULT sysutcdatetime(),
        ExpiresAtUtc datetime2(3) NULL,
        Status nvarchar(20) NOT NULL,
        CapabilityKey nvarchar(200) NULL,
        FunctionName nvarchar(200) NULL,
        ProposedToolName nvarchar(200) NOT NULL,
        ProposedSpName nvarchar(200) NOT NULL,
        ArgsJson nvarchar(max) NOT NULL,
        PreviewResultJson nvarchar(max) NULL,
        RequestedByUserId nvarchar(50) NOT NULL,
        ConfirmedAtUtc datetime2(3) NULL,
        ApprovedByUserId nvarchar(50) NULL,
        ApprovedAtUtc datetime2(3) NULL,
        CancelledAtUtc datetime2(3) NULL,
        ExecutedAtUtc datetime2(3) NULL,
        ExecutedByUserId nvarchar(50) NULL,
        CorrelationId nvarchar(100) NULL,
        MetadataJson nvarchar(max) NULL,
        ExecutionResultCompactJson nvarchar(max) NULL,
        CONSTRAINT PK_ActionRequest PRIMARY KEY (ActionId)
    );
END;
GO

IF COL_LENGTH('dbo.ActionRequest', 'UserId') IS NULL ALTER TABLE dbo.ActionRequest ADD UserId nvarchar(50) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'ExpiresAtUtc') IS NULL ALTER TABLE dbo.ActionRequest ADD ExpiresAtUtc datetime2(3) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'CapabilityKey') IS NULL ALTER TABLE dbo.ActionRequest ADD CapabilityKey nvarchar(200) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'FunctionName') IS NULL ALTER TABLE dbo.ActionRequest ADD FunctionName nvarchar(200) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'PreviewResultJson') IS NULL ALTER TABLE dbo.ActionRequest ADD PreviewResultJson nvarchar(max) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'ConfirmedAtUtc') IS NULL ALTER TABLE dbo.ActionRequest ADD ConfirmedAtUtc datetime2(3) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'ApprovedByUserId') IS NULL ALTER TABLE dbo.ActionRequest ADD ApprovedByUserId nvarchar(50) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'ApprovedAtUtc') IS NULL ALTER TABLE dbo.ActionRequest ADD ApprovedAtUtc datetime2(3) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'CancelledAtUtc') IS NULL ALTER TABLE dbo.ActionRequest ADD CancelledAtUtc datetime2(3) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'ExecutedAtUtc') IS NULL ALTER TABLE dbo.ActionRequest ADD ExecutedAtUtc datetime2(3) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'ExecutedByUserId') IS NULL ALTER TABLE dbo.ActionRequest ADD ExecutedByUserId nvarchar(50) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'CorrelationId') IS NULL ALTER TABLE dbo.ActionRequest ADD CorrelationId nvarchar(100) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'MetadataJson') IS NULL ALTER TABLE dbo.ActionRequest ADD MetadataJson nvarchar(max) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'ExecutionResultCompactJson') IS NULL ALTER TABLE dbo.ActionRequest ADD ExecutionResultCompactJson nvarchar(max) NULL;
GO

UPDATE dbo.ActionRequest SET UserId = RequestedByUserId WHERE UserId IS NULL;
UPDATE dbo.ActionRequest SET CapabilityKey = ProposedToolName WHERE CapabilityKey IS NULL;
UPDATE dbo.ActionRequest SET FunctionName = ProposedToolName WHERE FunctionName IS NULL;
UPDATE dbo.ActionRequest SET ExpiresAtUtc = DATEADD(hour, 24, RequestedAtUtc) WHERE ExpiresAtUtc IS NULL;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_ActionRequest_ActiveConversation'
      AND object_id = OBJECT_ID('dbo.ActionRequest')
)
BEGIN
    CREATE INDEX IX_ActionRequest_ActiveConversation
        ON dbo.ActionRequest(TenantId, UserId, ConversationId, Status, ExpiresAtUtc)
        INCLUDE (RequestedByUserId, ProposedToolName, ProposedSpName);
END;
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
        ActionId, TenantId, UserId, ConversationId, RequestedAtUtc, ExpiresAtUtc, Status,
        CapabilityKey, FunctionName, ProposedToolName, ProposedSpName, ArgsJson,
        PreviewResultJson, RequestedByUserId, CorrelationId, MetadataJson
    )
    VALUES
    (
        @ActionId, @TenantId, @EffectiveUserId, @ConversationId, @NowUtc,
        COALESCE(@ExpiresAtUtc, DATEADD(hour, 24, @NowUtc)), 'Pending',
        COALESCE(NULLIF(@CapabilityKey, ''), @ProposedToolName),
        COALESCE(NULLIF(@FunctionName, ''), @ProposedToolName),
        @ProposedToolName, @ProposedSpName, @ArgsJson, @PreviewResultJson,
        @RequestedByUserId, @CorrelationId, @MetadataJson
    );

    SELECT * FROM dbo.ActionRequest WHERE ActionId = @ActionId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_actionrequest_get
    @TenantId nvarchar(50),
    @ActionId nvarchar(64)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.ActionRequest WHERE TenantId = @TenantId AND ActionId = @ActionId;
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
    SET Status = 'Confirmed', ConfirmedAtUtc = sysutcdatetime()
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId
      AND COALESCE(UserId, RequestedByUserId) = @UserId
      AND Status = 'Pending'
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > sysutcdatetime());

    SELECT * FROM dbo.ActionRequest
    WHERE TenantId = @TenantId AND ActionId = @ActionId AND COALESCE(UserId, RequestedByUserId) = @UserId;
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
    SET Status = 'Approved', ApprovedByUserId = @ApprovedByUserId, ApprovedAtUtc = sysutcdatetime()
    WHERE TenantId = @TenantId
      AND ActionId = @ActionId
      AND Status IN ('Pending', 'Confirmed')
      AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > sysutcdatetime());

    SELECT * FROM dbo.ActionRequest WHERE TenantId = @TenantId AND ActionId = @ActionId;
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

    SELECT * FROM dbo.ActionRequest WHERE TenantId = @TenantId AND ActionId = @ActionId;
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

    SELECT * FROM dbo.ActionRequest WHERE TenantId = @TenantId AND ActionId = @ActionId;
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

    SELECT @@ROWCOUNT AS ExpiredCount;
END;
GO

PRINT N'Action request table/procedures are present. Real write execution remains application-disabled.';
GO
