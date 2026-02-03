SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Batch insert procedure for audit events
CREATE OR ALTER PROCEDURE dbo.app_auditlog_insert
    @Events dbo.AuditEventTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.AuditLog (
        EventId, EventType, Timestamp, TenantId, UserId, CorrelationId,
        IpAddress, UserAgent, Outcome, Details, Checksum, CreatedAtUtc
    )
    SELECT
        EventId, EventType, Timestamp, TenantId, UserId, CorrelationId,
        IpAddress, UserAgent, Outcome, Details, Checksum, SYSUTCDATETIME()
    FROM @Events;

    RETURN @@ROWCOUNT;
END;
GO

-- Query procedure with filters
CREATE OR ALTER PROCEDURE dbo.app_auditlog_query
    @TenantId nvarchar(50) = NULL,
    @UserId nvarchar(50) = NULL,
    @EventTypes nvarchar(200) = NULL,  -- Comma-separated list of event type codes
    @FromDate datetimeoffset = NULL,
    @ToDate datetimeoffset = NULL,
    @CorrelationId nvarchar(64) = NULL,
    @Outcome int = NULL,
    @Offset int = 0,
    @Limit int = 100
AS
BEGIN
    SET NOCOUNT ON;

    -- Parse event types if provided
    DECLARE @EventTypeTable TABLE (EventType int);
    IF @EventTypes IS NOT NULL
    BEGIN
        INSERT INTO @EventTypeTable (EventType)
        SELECT CAST(value AS int)
        FROM STRING_SPLIT(@EventTypes, ',')
        WHERE TRY_CAST(value AS int) IS NOT NULL;
    END;

    SELECT
        EventId,
        EventType,
        Timestamp,
        TenantId,
        UserId,
        CorrelationId,
        IpAddress,
        UserAgent,
        Outcome,
        Details,
        Checksum,
        CreatedAtUtc
    FROM dbo.AuditLog
    WHERE (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@UserId IS NULL OR UserId = @UserId)
      AND (@EventTypes IS NULL OR EventType IN (SELECT EventType FROM @EventTypeTable))
      AND (@FromDate IS NULL OR Timestamp >= @FromDate)
      AND (@ToDate IS NULL OR Timestamp <= @ToDate)
      AND (@CorrelationId IS NULL OR CorrelationId = @CorrelationId)
      AND (@Outcome IS NULL OR Outcome = @Outcome)
    ORDER BY Timestamp DESC
    OFFSET @Offset ROWS
    FETCH NEXT @Limit ROWS ONLY;
END;
GO

-- Count procedure for pagination
CREATE OR ALTER PROCEDURE dbo.app_auditlog_count
    @TenantId nvarchar(50) = NULL,
    @UserId nvarchar(50) = NULL,
    @EventTypes nvarchar(200) = NULL,
    @FromDate datetimeoffset = NULL,
    @ToDate datetimeoffset = NULL,
    @CorrelationId nvarchar(64) = NULL,
    @Outcome int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @EventTypeTable TABLE (EventType int);
    IF @EventTypes IS NOT NULL
    BEGIN
        INSERT INTO @EventTypeTable (EventType)
        SELECT CAST(value AS int)
        FROM STRING_SPLIT(@EventTypes, ',')
        WHERE TRY_CAST(value AS int) IS NOT NULL;
    END;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.AuditLog
    WHERE (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@UserId IS NULL OR UserId = @UserId)
      AND (@EventTypes IS NULL OR EventType IN (SELECT EventType FROM @EventTypeTable))
      AND (@FromDate IS NULL OR Timestamp >= @FromDate)
      AND (@ToDate IS NULL OR Timestamp <= @ToDate)
      AND (@CorrelationId IS NULL OR CorrelationId = @CorrelationId)
      AND (@Outcome IS NULL OR Outcome = @Outcome);
END;
GO

-- Retention-based cleanup procedure
CREATE OR ALTER PROCEDURE dbo.app_auditlog_purge
    @RetentionDays int = 365,
    @BatchSize int = 5000
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CutoffDate datetime2(3) = DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
    DECLARE @DeletedCount int = 0;
    DECLARE @TotalDeleted int = 0;

    -- Delete in batches to avoid long-running transactions
    WHILE 1 = 1
    BEGIN
        DELETE TOP (@BatchSize)
        FROM dbo.AuditLog
        WHERE CreatedAtUtc < @CutoffDate;

        SET @DeletedCount = @@ROWCOUNT;
        SET @TotalDeleted = @TotalDeleted + @DeletedCount;

        IF @DeletedCount < @BatchSize
            BREAK;

        -- Brief pause to reduce log pressure
        WAITFOR DELAY '00:00:00.100';
    END;

    SELECT @TotalDeleted AS DeletedCount, @CutoffDate AS CutoffDate;
END;
GO

-- Verify checksum procedure
CREATE OR ALTER PROCEDURE dbo.app_auditlog_verify_checksum
    @EventId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        EventId,
        Checksum,
        CASE
            WHEN Checksum IS NOT NULL AND LEN(Checksum) = 44 THEN 1
            ELSE 0
        END AS IsValid
    FROM dbo.AuditLog
    WHERE EventId = @EventId;
END;
GO
