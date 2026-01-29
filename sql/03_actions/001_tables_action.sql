SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.ActionRequest', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ActionRequest
    (
        ActionId nvarchar(64) NOT NULL,
        TenantId nvarchar(50) NOT NULL,
        ConversationId nvarchar(64) NOT NULL,
        RequestedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ActionRequest_RequestedAtUtc DEFAULT sysutcdatetime(),
        Status nvarchar(20) NOT NULL,
        ProposedToolName nvarchar(200) NOT NULL,
        ProposedSpName nvarchar(200) NOT NULL,
        ArgsJson nvarchar(max) NOT NULL,
        RequestedByUserId nvarchar(50) NOT NULL,
        ApprovedByUserId nvarchar(50) NULL,
        ApprovedAtUtc datetime2(3) NULL,
        ExecutedAtUtc datetime2(3) NULL,
        ExecutionResultCompactJson nvarchar(max) NULL,
        CONSTRAINT PK_ActionRequest PRIMARY KEY (ActionId)
    );
END;
GO
