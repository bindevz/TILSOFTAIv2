SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.ModuleScopeLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ModuleScopeLog
    (
        Id              bigint IDENTITY(1,1) NOT NULL,
        ConversationId  nvarchar(100)   NOT NULL,
        TenantId        nvarchar(50)    NOT NULL,
        UserId          nvarchar(100)   NOT NULL,
        UserQuery       nvarchar(2000)  NOT NULL,
        ResolvedModules nvarchar(500)   NOT NULL,
        Confidence      decimal(5,4)    NOT NULL,
        Reasons         nvarchar(2000)  NULL,
        ToolCount       int             NOT NULL DEFAULT 0,
        CreatedAtUtc    datetime2(3)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ModuleScopeLog PRIMARY KEY (Id)
    );

    CREATE NONCLUSTERED INDEX IX_ModuleScopeLog_Tenant_Date
        ON dbo.ModuleScopeLog (TenantId, CreatedAtUtc DESC);

    PRINT 'Created table: dbo.ModuleScopeLog';
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_modulescopelog_insert
    @ConversationId  nvarchar(100),
    @TenantId        nvarchar(50),
    @UserId          nvarchar(100),
    @UserQuery       nvarchar(2000),
    @ResolvedModules nvarchar(500),
    @Confidence      decimal(5,4),
    @Reasons         nvarchar(2000) = NULL,
    @ToolCount       int = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ModuleScopeLog 
        (ConversationId, TenantId, UserId, UserQuery, ResolvedModules, Confidence, Reasons, ToolCount)
    VALUES 
        (@ConversationId, @TenantId, @UserId, @UserQuery, @ResolvedModules, @Confidence, @Reasons, @ToolCount);
END;
GO
