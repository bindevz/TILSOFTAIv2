SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.app_conversation_upsert
    @TenantId nvarchar(50),
    @ConversationId nvarchar(64),
    @UserId nvarchar(50),
    @Language nvarchar(10),
    @CorrelationId nvarchar(64) = NULL,
    @TraceId nvarchar(64) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Conversation WHERE TenantId = @TenantId AND ConversationId = @ConversationId)
    BEGIN
        UPDATE dbo.Conversation
        SET UpdatedAtUtc = sysutcdatetime(),
            Language = @Language,
            CorrelationId = COALESCE(@CorrelationId, CorrelationId),
            TraceId = COALESCE(@TraceId, TraceId)
        WHERE TenantId = @TenantId
          AND ConversationId = @ConversationId;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.Conversation
        (
            TenantId,
            ConversationId,
            UserId,
            Language,
            CorrelationId,
            TraceId
        )
        VALUES
        (
            @TenantId,
            @ConversationId,
            @UserId,
            @Language,
            @CorrelationId,
            @TraceId
        );
    END
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_conversationmessage_insert
    @TenantId nvarchar(50),
    @ConversationId nvarchar(64),
    @MessageId nvarchar(64),
    @Role nvarchar(20),
    @Content nvarchar(max),
    @ToolName nvarchar(200) = NULL,
    @CorrelationId nvarchar(64) = NULL,
    @TraceId nvarchar(64) = NULL,
    @RequestId nvarchar(64) = NULL,
    @UserId nvarchar(50) = NULL,
    @Language nvarchar(10) = NULL,
    @IsRedacted bit = 0,
    @PayloadHash nvarchar(64) = NULL,
    @PayloadLength int = NULL,
    @IsPayloadOmitted bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ConversationMessage
    (
        TenantId,
        ConversationId,
        MessageId,
        Role,
        Content,
        ToolName,
        CorrelationId,
        TraceId,
        RequestId,
        UserId,
        Language,
        IsRedacted,
        PayloadHash,
        PayloadLength,
        IsPayloadOmitted
    )
    VALUES
    (
        @TenantId,
        @ConversationId,
        @MessageId,
        @Role,
        @Content,
        @ToolName,
        @CorrelationId,
        @TraceId,
        @RequestId,
        @UserId,
        @Language,
        @IsRedacted,
        @PayloadHash,
        @PayloadLength,
        @IsPayloadOmitted
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_toolexecution_insert
    @TenantId nvarchar(50),
    @ConversationId nvarchar(64),
    @ExecutionId nvarchar(64),
    @ToolName nvarchar(200),
    @SpName nvarchar(200),
    @ArgumentsJson nvarchar(max),
    @ResultJson nvarchar(max) = NULL,
    @CompactedResultJson nvarchar(max) = NULL,
    @Success bit,
    @DurationMs bigint,
    @CorrelationId nvarchar(64) = NULL,
    @TraceId nvarchar(64) = NULL,
    @RequestId nvarchar(64) = NULL,
    @UserId nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ToolExecution
    (
        TenantId,
        ConversationId,
        ExecutionId,
        ToolName,
        SpName,
        ArgumentsJson,
        ResultJson,
        CompactedResultJson,
        Success,
        DurationMs,
        CorrelationId,
        TraceId,
        RequestId,
        UserId
    )
    VALUES
    (
        @TenantId,
        @ConversationId,
        @ExecutionId,
        @ToolName,
        @SpName,
        @ArgumentsJson,
        @ResultJson,
        @CompactedResultJson,
        @Success,
        @DurationMs,
        @CorrelationId,
        @TraceId,
        @RequestId,
        @UserId
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_errorlog_insert
    @TenantId nvarchar(50),
    @ErrorId nvarchar(64),
    @CorrelationId nvarchar(64) = NULL,
    @ConversationId nvarchar(64) = NULL,
    @TraceId nvarchar(64) = NULL,
    @RequestId nvarchar(64) = NULL,
    @UserId nvarchar(50) = NULL,
    @ErrorCode nvarchar(100),
    @Message nvarchar(2000),
    @DetailJson nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ErrorLog
    (
        TenantId,
        ErrorId,
        CorrelationId,
        ConversationId,
        TraceId,
        RequestId,
        UserId,
        ErrorCode,
        Message,
        DetailJson
    )
    VALUES
    (
        @TenantId,
        @ErrorId,
        @CorrelationId,
        @ConversationId,
        @TraceId,
        @RequestId,
        @UserId,
        @ErrorCode,
        @Message,
        @DetailJson
    );
END;
GO
