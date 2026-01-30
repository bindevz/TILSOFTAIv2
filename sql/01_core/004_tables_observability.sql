SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.Conversation', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Conversation
    (
        TenantId nvarchar(50) NOT NULL,
        ConversationId nvarchar(64) NOT NULL,
        UserId nvarchar(50) NOT NULL,
        Language nvarchar(10) NOT NULL,
        CorrelationId nvarchar(64) NULL,
        TraceId nvarchar(64) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Conversation_CreatedAtUtc DEFAULT sysutcdatetime(),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Conversation_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_Conversation PRIMARY KEY (TenantId, ConversationId)
    );
END;
GO

-- Add columns if table exists but columns don't
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Conversation') AND name = 'CorrelationId')
BEGIN
    ALTER TABLE dbo.Conversation ADD CorrelationId nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Conversation') AND name = 'TraceId')
BEGIN
    ALTER TABLE dbo.Conversation ADD TraceId nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Conversation_Tenant_User' AND object_id = OBJECT_ID('dbo.Conversation'))
BEGIN
    CREATE INDEX IX_Conversation_Tenant_User ON dbo.Conversation (TenantId, UserId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Conversation_Tenant_CorrelationId' AND object_id = OBJECT_ID('dbo.Conversation'))
BEGIN
    CREATE INDEX IX_Conversation_Tenant_CorrelationId ON dbo.Conversation (TenantId, CorrelationId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Conversation_Tenant_TraceId' AND object_id = OBJECT_ID('dbo.Conversation'))
BEGIN
    CREATE INDEX IX_Conversation_Tenant_TraceId ON dbo.Conversation (TenantId, TraceId);
END;
GO

IF OBJECT_ID('dbo.ConversationMessage', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConversationMessage
    (
        TenantId nvarchar(50) NOT NULL,
        ConversationId nvarchar(64) NOT NULL,
        MessageId nvarchar(64) NOT NULL,
        Role nvarchar(20) NOT NULL,
        Content nvarchar(max) NULL,
        ToolName nvarchar(200) NULL,
        CorrelationId nvarchar(64) NULL,
        TraceId nvarchar(64) NULL,
        RequestId nvarchar(64) NULL,
        UserId nvarchar(50) NULL,
        Language nvarchar(10) NULL,
        IsRedacted bit NOT NULL CONSTRAINT DF_ConversationMessage_IsRedacted DEFAULT 0,
        PayloadHash nvarchar(64) NULL,
        PayloadLength int NULL,
        IsPayloadOmitted bit NOT NULL CONSTRAINT DF_ConversationMessage_IsPayloadOmitted DEFAULT 0,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ConversationMessage_CreatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_ConversationMessage PRIMARY KEY (TenantId, ConversationId, MessageId)
    );
END;
GO

-- Add columns if table exists but columns don't
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConversationMessage') AND name = 'CorrelationId')
BEGIN
    ALTER TABLE dbo.ConversationMessage ADD CorrelationId nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConversationMessage') AND name = 'TraceId')
BEGIN
    ALTER TABLE dbo.ConversationMessage ADD TraceId nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConversationMessage') AND name = 'RequestId')
BEGIN
    ALTER TABLE dbo.ConversationMessage ADD RequestId nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConversationMessage') AND name = 'UserId')
BEGIN
    ALTER TABLE dbo.ConversationMessage ADD UserId nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConversationMessage') AND name = 'Language')
BEGIN
    ALTER TABLE dbo.ConversationMessage ADD Language nvarchar(10) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConversationMessage') AND name = 'IsRedacted')
BEGIN
    ALTER TABLE dbo.ConversationMessage ADD IsRedacted bit NOT NULL CONSTRAINT DF_ConversationMessage_IsRedacted DEFAULT 0;
END;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConversationMessage') AND name = 'Content' AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.ConversationMessage ALTER COLUMN Content nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConversationMessage') AND name = 'PayloadHash')
BEGIN
    ALTER TABLE dbo.ConversationMessage ADD PayloadHash nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConversationMessage') AND name = 'PayloadLength')
BEGIN
    ALTER TABLE dbo.ConversationMessage ADD PayloadLength int NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConversationMessage') AND name = 'IsPayloadOmitted')
BEGIN
    ALTER TABLE dbo.ConversationMessage ADD IsPayloadOmitted bit NOT NULL CONSTRAINT DF_ConversationMessage_IsPayloadOmitted DEFAULT 0;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ConversationMessage_Tenant_Conversation' AND object_id = OBJECT_ID('dbo.ConversationMessage'))
BEGIN
    CREATE INDEX IX_ConversationMessage_Tenant_Conversation
        ON dbo.ConversationMessage (TenantId, ConversationId, CreatedAtUtc);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ConversationMessage_Tenant_CorrelationId' AND object_id = OBJECT_ID('dbo.ConversationMessage'))
BEGIN
    CREATE INDEX IX_ConversationMessage_Tenant_CorrelationId ON dbo.ConversationMessage (TenantId, CorrelationId);
END;
GO

IF OBJECT_ID('dbo.ToolExecution', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ToolExecution
    (
        TenantId nvarchar(50) NOT NULL,
        ConversationId nvarchar(64) NOT NULL,
        ExecutionId nvarchar(64) NOT NULL,
        ToolName nvarchar(200) NOT NULL,
        SpName nvarchar(200) NOT NULL,
        ArgumentsJson nvarchar(max) NOT NULL,
        ResultJson nvarchar(max) NULL,
        CompactedResultJson nvarchar(max) NULL,
        Success bit NOT NULL,
        DurationMs bigint NOT NULL,
        CorrelationId nvarchar(64) NULL,
        TraceId nvarchar(64) NULL,
        RequestId nvarchar(64) NULL,
        UserId nvarchar(50) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ToolExecution_CreatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_ToolExecution PRIMARY KEY (TenantId, ConversationId, ExecutionId)
    );
END;
GO

-- Add columns if table exists but columns don't
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ToolExecution') AND name = 'CorrelationId')
BEGIN
    ALTER TABLE dbo.ToolExecution ADD CorrelationId nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ToolExecution') AND name = 'TraceId')
BEGIN
    ALTER TABLE dbo.ToolExecution ADD TraceId nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ToolExecution') AND name = 'RequestId')
BEGIN
    ALTER TABLE dbo.ToolExecution ADD RequestId nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ToolExecution') AND name = 'UserId')
BEGIN
    ALTER TABLE dbo.ToolExecution ADD UserId nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ToolExecution_Tenant_Tool' AND object_id = OBJECT_ID('dbo.ToolExecution'))
BEGIN
    CREATE INDEX IX_ToolExecution_Tenant_Tool
        ON dbo.ToolExecution (TenantId, ToolName, CreatedAtUtc);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ToolExecution_Tenant_Conversation' AND object_id = OBJECT_ID('dbo.ToolExecution'))
BEGIN
    CREATE INDEX IX_ToolExecution_Tenant_Conversation
        ON dbo.ToolExecution (TenantId, ConversationId, CreatedAtUtc);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ToolExecution_Tenant_CorrelationId' AND object_id = OBJECT_ID('dbo.ToolExecution'))
BEGIN
    CREATE INDEX IX_ToolExecution_Tenant_CorrelationId ON dbo.ToolExecution (TenantId, CorrelationId);
END;
GO



IF OBJECT_ID('dbo.ErrorLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ErrorLog
    (
        TenantId nvarchar(50) NOT NULL,
        ErrorId nvarchar(64) NOT NULL,
        CorrelationId nvarchar(64) NULL,
        ConversationId nvarchar(64) NULL,
        TraceId nvarchar(64) NULL,
        RequestId nvarchar(64) NULL,
        UserId nvarchar(50) NULL,
        ErrorCode nvarchar(100) NOT NULL,
        Message nvarchar(2000) NOT NULL,
        DetailJson nvarchar(max) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ErrorLog_CreatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_ErrorLog PRIMARY KEY (TenantId, ErrorId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ErrorLog_Tenant_CreatedAt' AND object_id = OBJECT_ID('dbo.ErrorLog'))
BEGIN
    CREATE INDEX IX_ErrorLog_Tenant_CreatedAt
        ON dbo.ErrorLog (TenantId, CreatedAtUtc);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ErrorLog_Tenant_CreatedAt_Code' AND object_id = OBJECT_ID('dbo.ErrorLog'))
BEGIN
    CREATE INDEX IX_ErrorLog_Tenant_CreatedAt_Code
        ON dbo.ErrorLog (TenantId, CreatedAtUtc, ErrorCode);
END;
GO
