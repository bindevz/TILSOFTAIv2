SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.DiagnosticsRule', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DiagnosticsRule
    (
        Id bigint IDENTITY(1,1) NOT NULL,
        RuleKey nvarchar(200) NOT NULL,
        TenantId nvarchar(50) NULL,
        Module nvarchar(100) NOT NULL,
        Description nvarchar(2000) NOT NULL,
        AiSpName nvarchar(200) NOT NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_DiagnosticsRule_IsEnabled DEFAULT (1),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_DiagnosticsRule_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_DiagnosticsRule_Id PRIMARY KEY (Id)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_DiagnosticsRule_AiSpNamePrefix'
      AND parent_object_id = OBJECT_ID('dbo.DiagnosticsRule'))
BEGIN
    ALTER TABLE dbo.DiagnosticsRule
    ADD CONSTRAINT CK_DiagnosticsRule_AiSpNamePrefix CHECK (AiSpName LIKE 'ai[_]%');
END;
GO
