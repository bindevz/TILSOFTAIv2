-- ============================================================
-- PATCH 36.07: Conversation extensions for observability
-- ConversationPolicySnapshot: effective policy hash per conversation
-- ConversationReActTrace: triggered follow-up rules trace
-- ============================================================

-- ============================================================
-- ConversationPolicySnapshot
-- ============================================================
IF OBJECT_ID('dbo.ConversationPolicySnapshot', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConversationPolicySnapshot
    (
        SnapshotId      bigint IDENTITY(1,1) NOT NULL,
        ConversationId  nvarchar(64)         NOT NULL,
        PolicyHash      nvarchar(128)        NOT NULL,
        PolicyJson      nvarchar(max)        NOT NULL,
        CreatedAtUtc    datetime2(7)         NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_ConversationPolicySnapshot PRIMARY KEY CLUSTERED (SnapshotId),
        INDEX IX_ConversationPolicySnapshot_ConvId NONCLUSTERED (ConversationId)
    );
END
GO

-- ============================================================
-- ConversationReActTrace
-- ============================================================
IF OBJECT_ID('dbo.ConversationReActTrace', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConversationReActTrace
    (
        TraceId          bigint IDENTITY(1,1) NOT NULL,
        ConversationId   nvarchar(64)         NOT NULL,
        Step             int                  NOT NULL,
        RuleKey          nvarchar(128)        NOT NULL,
        TriggerToolName  nvarchar(128)        NOT NULL,
        FollowUpToolName nvarchar(128)        NOT NULL,
        CreatedAtUtc     datetime2(7)         NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_ConversationReActTrace PRIMARY KEY CLUSTERED (TraceId),
        INDEX IX_ConversationReActTrace_ConvId NONCLUSTERED (ConversationId)
    );
END
GO
