SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- AuditLog table for immutable security audit trail
IF OBJECT_ID('dbo.AuditLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLog
    (
        EventId uniqueidentifier NOT NULL,
        EventType int NOT NULL,
        Timestamp datetimeoffset(3) NOT NULL,
        TenantId nvarchar(50) NOT NULL,
        UserId nvarchar(50) NOT NULL,
        CorrelationId nvarchar(64) NULL,
        IpAddress nvarchar(45) NULL,
        UserAgent nvarchar(500) NULL,
        Outcome int NOT NULL,
        Details nvarchar(max) NULL,
        Checksum nvarchar(64) NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_AuditLog_CreatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_AuditLog PRIMARY KEY NONCLUSTERED (EventId)
    );
END;
GO

-- Clustered index on (TenantId, Timestamp) for efficient tenant-scoped queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_Tenant_Timestamp' AND object_id = OBJECT_ID('dbo.AuditLog'))
BEGIN
    CREATE CLUSTERED INDEX IX_AuditLog_Tenant_Timestamp
        ON dbo.AuditLog (TenantId, Timestamp DESC);
END;
GO

-- Index on UserId for user activity queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_UserId' AND object_id = OBJECT_ID('dbo.AuditLog'))
BEGIN
    CREATE INDEX IX_AuditLog_UserId
        ON dbo.AuditLog (UserId, Timestamp DESC)
        INCLUDE (EventType, TenantId);
END;
GO

-- Index on EventType for event type filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_EventType' AND object_id = OBJECT_ID('dbo.AuditLog'))
BEGIN
    CREATE INDEX IX_AuditLog_EventType
        ON dbo.AuditLog (EventType, Timestamp DESC)
        INCLUDE (TenantId, UserId);
END;
GO

-- Index on CorrelationId for request tracing
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_CorrelationId' AND object_id = OBJECT_ID('dbo.AuditLog'))
BEGIN
    CREATE INDEX IX_AuditLog_CorrelationId
        ON dbo.AuditLog (CorrelationId)
        WHERE CorrelationId IS NOT NULL;
END;
GO

-- Index on CreatedAtUtc for retention cleanup
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_CreatedAtUtc' AND object_id = OBJECT_ID('dbo.AuditLog'))
BEGIN
    CREATE INDEX IX_AuditLog_CreatedAtUtc
        ON dbo.AuditLog (CreatedAtUtc);
END;
GO

-- Table-valued parameter type for batch inserts
IF TYPE_ID('dbo.AuditEventTableType') IS NULL
BEGIN
    CREATE TYPE dbo.AuditEventTableType AS TABLE
    (
        EventId uniqueidentifier NOT NULL,
        EventType int NOT NULL,
        Timestamp datetimeoffset(3) NOT NULL,
        TenantId nvarchar(50) NOT NULL,
        UserId nvarchar(50) NOT NULL,
        CorrelationId nvarchar(64) NULL,
        IpAddress nvarchar(45) NULL,
        UserAgent nvarchar(500) NULL,
        Outcome int NOT NULL,
        Details nvarchar(max) NULL,
        Checksum nvarchar(64) NOT NULL
    );
END;
GO
