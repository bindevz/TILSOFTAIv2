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

IF COL_LENGTH('dbo.ActionRequest', 'UserId') IS NULL
    ALTER TABLE dbo.ActionRequest ADD UserId nvarchar(50) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'ExpiresAtUtc') IS NULL
    ALTER TABLE dbo.ActionRequest ADD ExpiresAtUtc datetime2(3) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'CapabilityKey') IS NULL
    ALTER TABLE dbo.ActionRequest ADD CapabilityKey nvarchar(200) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'FunctionName') IS NULL
    ALTER TABLE dbo.ActionRequest ADD FunctionName nvarchar(200) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'PreviewResultJson') IS NULL
    ALTER TABLE dbo.ActionRequest ADD PreviewResultJson nvarchar(max) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'ConfirmedAtUtc') IS NULL
    ALTER TABLE dbo.ActionRequest ADD ConfirmedAtUtc datetime2(3) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'CancelledAtUtc') IS NULL
    ALTER TABLE dbo.ActionRequest ADD CancelledAtUtc datetime2(3) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'ExecutedByUserId') IS NULL
    ALTER TABLE dbo.ActionRequest ADD ExecutedByUserId nvarchar(50) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'CorrelationId') IS NULL
    ALTER TABLE dbo.ActionRequest ADD CorrelationId nvarchar(100) NULL;
IF COL_LENGTH('dbo.ActionRequest', 'MetadataJson') IS NULL
    ALTER TABLE dbo.ActionRequest ADD MetadataJson nvarchar(max) NULL;
GO

UPDATE dbo.ActionRequest
SET UserId = RequestedByUserId
WHERE UserId IS NULL;

UPDATE dbo.ActionRequest
SET CapabilityKey = ProposedToolName
WHERE CapabilityKey IS NULL;

UPDATE dbo.ActionRequest
SET FunctionName = ProposedToolName
WHERE FunctionName IS NULL;

UPDATE dbo.ActionRequest
SET ExpiresAtUtc = DATEADD(hour, 24, RequestedAtUtc)
WHERE ExpiresAtUtc IS NULL;
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
