SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.WriteActionCatalog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WriteActionCatalog
    (
        TenantId nvarchar(50) NOT NULL,
        ActionName nvarchar(100) NOT NULL,
        SpName nvarchar(200) NOT NULL,
        RequiredRoles nvarchar(200) NULL, -- CSV of roles, NULL = any authorized user
        JsonSchema nvarchar(max) NULL,
        Description nvarchar(500) NULL,
        IsEnabled bit NOT NULL DEFAULT 1,
        CreatedAtUtc datetime2(3) NOT NULL DEFAULT sysutcdatetime(),
        CONSTRAINT PK_WriteActionCatalog PRIMARY KEY (TenantId, ActionName)
    );

    CREATE UNIQUE INDEX IX_WriteActionCatalog_SpName ON dbo.WriteActionCatalog(TenantId, SpName);
END;
GO
