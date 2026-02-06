/*
Seed metadata dictionary sample for Model domain.
Assumes a platform table dbo.MetadataDictionary exists with specific schema.
If your project uses a different table name or schema, adapt accordingly.

Required columns: TenantId (nullable), [Key], Language, [Text], UpdatedAtUtc
*/

SET NOCOUNT ON;

-- Guard: Skip if table or required columns don't exist
IF OBJECT_ID('dbo.MetadataDictionary','U') IS NULL
BEGIN
    PRINT 'dbo.MetadataDictionary not found. Skip.';
END
ELSE IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MetadataDictionary' AND COLUMN_NAME = 'Text')
BEGIN
    PRINT 'dbo.MetadataDictionary does not have [Text] column (different schema). Skip.';
END
ELSE
BEGIN
    -- All guards passed, execute the seed via sp_executesql to defer column validation
    DECLARE @sql NVARCHAR(MAX) = N'
    DECLARE @TenantId NVARCHAR(64) = NULL; -- NULL = global
    DECLARE @Now DATETIME2 = SYSUTCDATETIME();
    
    ;WITH src AS (
        SELECT * FROM (VALUES
          (''Model.Code'', ''vi'', N''Mã model''),
          (''Model.Code'', ''en'', N''Model code''),
          (''Model.Name'', ''vi'', N''Tên model''),
          (''Model.Name'', ''en'', N''Model name''),
          (''Model.Season'', ''vi'', N''Mùa vụ / Season''),
          (''Model.Season'', ''en'', N''Season''),
          (''Model.CBM'', ''vi'', N''Thể tích khối (m³) của phương án đóng gói''),
          (''Model.CBM'', ''en'', N''Cubic meter volume (m³) for packaging option''),
          (''Model.Qnt40HC'', ''vi'', N''Số lượng xếp container 40HC''),
          (''Model.Qnt40HC'', ''en'', N''Units per 40'''' High Cube container''),
          (''Packaging.BoxInSet'', ''vi'', N''Số thùng trong 1 bộ''),
          (''Packaging.BoxInSet'', ''en'', N''Boxes per set''),
          (''Material.IsFSCEnabled'', ''vi'', N''Tùy chọn FSC (gỗ chứng chỉ)''),
          (''Material.IsFSCEnabled'', ''en'', N''FSC option''),
          (''Material.IsRCSEnabled'', ''vi'', N''Tùy chọn RCS (tái chế)''),
          (''Material.IsRCSEnabled'', ''en'', N''RCS option'')
        ) v([Key],[Language],[Text])
    )
    MERGE dbo.MetadataDictionary AS t
    USING src AS s
    ON (ISNULL(t.TenantId,'''') = ISNULL(@TenantId,'''') AND t.[Key]=s.[Key] AND t.[Language]=s.[Language])
    WHEN NOT MATCHED THEN
      INSERT (TenantId,[Key],[Language],[Text],UpdatedAtUtc)
      VALUES (@TenantId,s.[Key],s.[Language],s.[Text],@Now)
    WHEN MATCHED AND ISNULL(t.[Text],'''') <> ISNULL(s.[Text],'''')
    THEN UPDATE SET t.[Text]=s.[Text], t.UpdatedAtUtc=@Now;
    
    PRINT ''Seed MetadataDictionary completed.'';
    ';
    
    EXEC sp_executesql @sql;
END
GO
