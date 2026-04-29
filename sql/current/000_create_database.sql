:setvar DatabaseName "TILSOFTAI"
USE [master];
GO

IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(N'$(DatabaseName)') + N';';
    EXEC (@sql);
END;
GO

PRINT N'Ensured database $(DatabaseName) exists.';
GO
