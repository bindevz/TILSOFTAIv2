-- Migration History Table
-- Tracks all applied database migrations
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '__MigrationHistory' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.__MigrationHistory (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ScriptName NVARCHAR(500) NOT NULL,
        ScriptHash NVARCHAR(64) NOT NULL,
        AppliedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        AppliedBy NVARCHAR(200) NULL,
        ExecutionTimeMs INT NULL,
        CONSTRAINT UQ_MigrationHistory_ScriptName UNIQUE (ScriptName)
    );
    
    CREATE INDEX IX_MigrationHistory_AppliedAt ON dbo.__MigrationHistory(AppliedAt DESC);
    
    PRINT 'Created __MigrationHistory table.';
END
ELSE
BEGIN
    PRINT '__MigrationHistory table already exists.';
END
GO
