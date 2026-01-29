SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.app_writeactioncatalog_list
    @TenantId nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ActionName, SpName, RequiredRoles, JsonSchema, Description
    FROM dbo.WriteActionCatalog
    WHERE TenantId = @TenantId AND IsEnabled = 1
    ORDER BY ActionName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.app_writeactioncatalog_get
    @TenantId nvarchar(50),
    @SpName nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ActionName, SpName, RequiredRoles, JsonSchema, Description
    FROM dbo.WriteActionCatalog
    WHERE TenantId = @TenantId AND SpName = @SpName AND IsEnabled = 1;
END;
GO
