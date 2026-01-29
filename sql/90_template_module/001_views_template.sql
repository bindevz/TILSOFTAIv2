SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- TEMPLATE ONLY: replace <module> and view names.
CREATE OR ALTER VIEW dbo.v_<module>_overview
AS
SELECT
    CAST(NULL AS nvarchar(50)) AS TenantId,
    CAST(NULL AS nvarchar(50)) AS ItemCode,
    CAST(NULL AS nvarchar(200)) AS ItemName
WHERE 1 = 0;
GO
