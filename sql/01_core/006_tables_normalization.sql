SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.NormalizationRule', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NormalizationRule
    (
        Id bigint IDENTITY(1,1) NOT NULL,
        RuleKey nvarchar(200) NOT NULL,
        TenantId nvarchar(50) NULL,
        Priority int NOT NULL,
        Pattern nvarchar(1000) NOT NULL,
        Replacement nvarchar(1000) NOT NULL,
        Description nvarchar(2000) NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_NormalizationRule_IsEnabled DEFAULT(1),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_NormalizationRule_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_NormalizationRule_Id PRIMARY KEY (Id)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_NormalizationRule_Tenant_Priority'
      AND object_id = OBJECT_ID('dbo.NormalizationRule')
)
BEGIN
    CREATE INDEX IX_NormalizationRule_Tenant_Priority
        ON dbo.NormalizationRule (TenantId, Priority);
END;
GO
