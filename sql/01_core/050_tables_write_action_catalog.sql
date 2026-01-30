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
        CONSTRAINT PK_WriteActionCatalog PRIMARY KEY (TenantId, ActionName),
        CONSTRAINT CK_WriteActionCatalog_JsonSchema_Valid CHECK (JsonSchema IS NULL OR ISJSON(JsonSchema) = 1)
    );

    CREATE UNIQUE INDEX IX_WriteActionCatalog_SpName ON dbo.WriteActionCatalog(TenantId, SpName);
END
ELSE
BEGIN
    -- Add CHECK constraint if it doesn't exist (for existing deployments)
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_WriteActionCatalog_JsonSchema_Valid' AND parent_object_id = OBJECT_ID('dbo.WriteActionCatalog'))
    BEGIN
        ALTER TABLE dbo.WriteActionCatalog
        ADD CONSTRAINT CK_WriteActionCatalog_JsonSchema_Valid CHECK (JsonSchema IS NULL OR ISJSON(JsonSchema) = 1);
        PRINT 'Added CHECK constraint CK_WriteActionCatalog_JsonSchema_Valid';
    END
END;
GO
