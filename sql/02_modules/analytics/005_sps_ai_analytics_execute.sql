/*******************************************************************************
* TILSOFTAI Analytics Module - Metrics Execution Stored Procedure
* Purpose: Execute validated analytics plans with aggregate metrics
* 
* CRITICAL: Security-first design:
*   1. QUOTENAME for all identifiers
*   2. Strict metric operation whitelist
*   3. Tenant isolation enforced
*   4. Defense-in-depth validation at execution boundary
*
* PATCH 29.01: New SP for secure metrics execution
*******************************************************************************/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.ai_analytics_execute_plan
    @TenantId   NVARCHAR(50),
    @ArgsJson   NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StartTime DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @GeneratedAtUtc DATETIME2(3) = @StartTime;
    
    -- Validation limits (defense-in-depth, same as validate_plan)
    DECLARE @MaxRows INT = 200;
    DECLARE @MaxGroupBy INT = 4;
    DECLARE @MaxMetrics INT = 3;
    DECLARE @MaxJoins INT = 1;
    
    -- Allowed metric operations (strict whitelist)
    DECLARE @AllowedOps TABLE (Op NVARCHAR(20) PRIMARY KEY);
    INSERT INTO @AllowedOps (Op) VALUES ('count'), ('countDistinct'), ('sum'), ('avg'), ('min'), ('max');
    
    -- Validate TenantId
    IF @TenantId IS NULL OR @TenantId = ''
    BEGIN
        RAISERROR('TenantId is required.', 16, 1);
        RETURN;
    END
    
    -- Validate JSON
    IF ISJSON(@ArgsJson) <> 1
    BEGIN
        RAISERROR('ArgsJson must be valid JSON.', 16, 1);
        RETURN;
    END
    
    -- Parse datasetKey
    DECLARE @DatasetKey NVARCHAR(200) = LTRIM(RTRIM(JSON_VALUE(@ArgsJson, '$.datasetKey')));
    IF @DatasetKey IS NULL OR @DatasetKey = ''
    BEGIN
        RAISERROR('datasetKey is required.', 16, 1);
        RETURN;
    END
    
    -- Parse and validate limit
    DECLARE @Limit INT = ISNULL(TRY_CONVERT(INT, JSON_VALUE(@ArgsJson, '$.limit')), @MaxRows);
    DECLARE @Truncated BIT = 0;
    IF @Limit <= 0 SET @Limit = @MaxRows;
    IF @Limit > @MaxRows
    BEGIN
        SET @Limit = @MaxRows;
        SET @Truncated = 1;
    END
    
    -- Validate groupBy count
    DECLARE @GroupByCount INT = (SELECT COUNT(1) FROM OPENJSON(@ArgsJson, '$.groupBy'));
    IF @GroupByCount > @MaxGroupBy
    BEGIN
        RAISERROR('GroupBy count exceeds maximum allowed (4).', 16, 1);
        RETURN;
    END
    
    -- Validate metrics count
    DECLARE @MetricsCount INT = (SELECT COUNT(1) FROM OPENJSON(@ArgsJson, '$.metrics'));
    IF @MetricsCount > @MaxMetrics
    BEGIN
        RAISERROR('Metrics count exceeds maximum allowed (3).', 16, 1);
        RETURN;
    END
    
    IF @MetricsCount = 0
    BEGIN
        RAISERROR('At least one metric is required.', 16, 1);
        RETURN;
    END
    
    -- Validate joins count
    DECLARE @JoinsCount INT = (SELECT COUNT(1) FROM OPENJSON(@ArgsJson, '$.joins'));
    IF @JoinsCount > @MaxJoins
    BEGIN
        RAISERROR('Joins count exceeds maximum allowed (1).', 16, 1);
        RETURN;
    END
    
    -- Get dataset metadata
    DECLARE @BaseObjectRaw NVARCHAR(500);
    DECLARE @TimeColumn NVARCHAR(200);
    
    SELECT TOP 1
        @BaseObjectRaw = BaseObject,
        @TimeColumn = TimeColumn
    FROM dbo.DatasetCatalog
    WHERE DatasetKey = @DatasetKey
      AND IsEnabled = 1
      AND (TenantId = @TenantId OR TenantId IS NULL)
    ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END;
    
    IF @BaseObjectRaw IS NULL
    BEGIN
        RAISERROR('Dataset not found or disabled.', 16, 1);
        RETURN;
    END
    
    -- Parse base object
    DECLARE @BaseSchema SYSNAME = ISNULL(PARSENAME(@BaseObjectRaw, 2), 'dbo');
    DECLARE @BaseName SYSNAME = PARSENAME(@BaseObjectRaw, 1);
    IF @BaseName IS NULL
    BEGIN
        RAISERROR('BaseObject must be schema-qualified.', 16, 1);
        RETURN;
    END
    
    DECLARE @BaseObjectId INT = OBJECT_ID(QUOTENAME(@BaseSchema) + '.' + QUOTENAME(@BaseName));
    IF @BaseObjectId IS NULL
    BEGIN
        RAISERROR('BaseObject does not exist.', 16, 1);
        RETURN;
    END
    
    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = @BaseObjectId AND type IN ('V', 'IF', 'U'))
    BEGIN
        RAISERROR('BaseObject must be a view, table, or inline function.', 16, 1);
        RETURN;
    END
    
    DECLARE @BaseObjectSql NVARCHAR(300) = QUOTENAME(@BaseSchema) + '.' + QUOTENAME(@BaseName);
    IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = @BaseObjectId AND type = 'IF')
       AND CHARINDEX('(', @BaseObjectRaw) = 0
    BEGIN
        SET @BaseObjectSql = @BaseObjectSql + '()';
    END
    
    -- Load field catalog
    DECLARE @Fields TABLE (
        FieldKey NVARCHAR(200) PRIMARY KEY,
        PhysicalColumn NVARCHAR(200) NOT NULL,
        DataType NVARCHAR(50) NOT NULL,
        AllowedAggregations NVARCHAR(500),
        IsFilterable BIT NOT NULL,
        IsGroupable BIT NOT NULL,
        IsSortable BIT NOT NULL
    );
    
    INSERT INTO @Fields (FieldKey, PhysicalColumn, DataType, AllowedAggregations, IsFilterable, IsGroupable, IsSortable)
    SELECT FieldKey, PhysicalColumn, DataType, AllowedAggregations, IsFilterable, IsGroupable, IsSortable
    FROM dbo.FieldCatalog
    WHERE DatasetKey = @DatasetKey AND IsEnabled = 1;
    
    IF NOT EXISTS (SELECT 1 FROM @Fields)
    BEGIN
        RAISERROR('No enabled fields found for dataset.', 16, 1);
        RETURN;
    END
    
    -- PATCH 29.07: Defense-in-depth security tag validation
    DECLARE @UserRoles TABLE (Role NVARCHAR(100));
    DECLARE @AllowedTags TABLE (Tag NVARCHAR(50));
    
    INSERT INTO @UserRoles (Role)
    SELECT value FROM OPENJSON(@ArgsJson, '$._roles');
    
    INSERT INTO @AllowedTags (Tag) VALUES ('PUBLIC'), ('INTERNAL');
    IF EXISTS (SELECT 1 FROM @UserRoles WHERE Role = 'analytics.pii')
        INSERT INTO @AllowedTags (Tag) VALUES ('PII');
    IF EXISTS (SELECT 1 FROM @UserRoles WHERE Role = 'analytics.sensitive')
        INSERT INTO @AllowedTags (Tag) VALUES ('SENSITIVE');
    IF EXISTS (SELECT 1 FROM @UserRoles WHERE Role = 'analytics.admin')
        INSERT INTO @AllowedTags (Tag) VALUES ('PII'), ('SENSITIVE'), ('RESTRICTED');
    
    -- Check for restricted field access in metrics, groupBy, where
    IF EXISTS (
        SELECT 1 FROM dbo.FieldCatalog fc
        WHERE fc.DatasetKey = @DatasetKey
          AND fc.IsEnabled = 1
          AND fc.SecurityTag IS NOT NULL
          AND fc.SecurityTag NOT IN (SELECT Tag FROM @AllowedTags)
          AND (
              fc.FieldKey IN (SELECT JSON_VALUE(value, '$.field') FROM OPENJSON(@ArgsJson, '$.metrics'))
              OR fc.FieldKey IN (SELECT value FROM OPENJSON(@ArgsJson, '$.groupBy'))
              OR fc.FieldKey IN (SELECT JSON_VALUE(value, '$.field') FROM OPENJSON(@ArgsJson, '$.where'))
          )
    )
    BEGIN
        RAISERROR('SECURITY_VIOLATION: Access denied to restricted fields.', 16, 1);
        RETURN;
    END
    
    -- Check tenant column
    DECLARE @HasTenantColumn BIT = CASE 
        WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @BaseObjectId AND name = 'TenantId') 
        THEN 1 ELSE 0 END;
    
    -- Parse and validate metrics
    DECLARE @Metrics TABLE (
        Ordinal INT NOT NULL,
        FieldKey NVARCHAR(200) NOT NULL,
        PhysicalColumn NVARCHAR(200),
        Op NVARCHAR(20) NOT NULL,
        Alias NVARCHAR(200) NOT NULL
    );
    
    INSERT INTO @Metrics (Ordinal, FieldKey, Op, Alias)
    SELECT 
        CAST(j.[key] AS INT),
        LTRIM(RTRIM(JSON_VALUE(j.value, '$.field'))),
        LOWER(LTRIM(RTRIM(JSON_VALUE(j.value, '$.op')))),
        LTRIM(RTRIM(ISNULL(JSON_VALUE(j.value, '$.alias'), 
            LOWER(LTRIM(RTRIM(JSON_VALUE(j.value, '$.op')))) + '_' + LTRIM(RTRIM(JSON_VALUE(j.value, '$.field'))))))
    FROM OPENJSON(@ArgsJson, '$.metrics') j;
    
    -- Validate metric operations (strict whitelist)
    IF EXISTS (SELECT 1 FROM @Metrics m WHERE m.Op NOT IN (SELECT Op FROM @AllowedOps))
    BEGIN
        DECLARE @InvalidOp NVARCHAR(200);
        SELECT TOP 1 @InvalidOp = Op FROM @Metrics WHERE Op NOT IN (SELECT Op FROM @AllowedOps);
        RAISERROR('Invalid metric operation: %s. Allowed: count, countDistinct, sum, avg, min, max.', 16, 1, @InvalidOp);
        RETURN;
    END
    
    -- Validate metric fields exist (except for count which can use * or any field)
    UPDATE m SET m.PhysicalColumn = f.PhysicalColumn
    FROM @Metrics m
    INNER JOIN @Fields f ON f.FieldKey = m.FieldKey;
    
    IF EXISTS (SELECT 1 FROM @Metrics WHERE PhysicalColumn IS NULL AND Op <> 'count')
    BEGIN
        RAISERROR('Unknown field in metrics.', 16, 1);
        RETURN;
    END
    
    -- For count without field, use *
    UPDATE @Metrics SET PhysicalColumn = '*' WHERE PhysicalColumn IS NULL AND Op = 'count';
    
    -- Validate field-specific aggregation rules
    IF EXISTS (
        SELECT 1 FROM @Metrics m
        INNER JOIN @Fields f ON f.FieldKey = m.FieldKey
        WHERE f.AllowedAggregations IS NOT NULL 
          AND f.AllowedAggregations <> ''
          AND CHARINDEX(m.Op, f.AllowedAggregations) = 0
    )
    BEGIN
        RAISERROR('Aggregation operation not allowed for field.', 16, 1);
        RETURN;
    END
    
    -- Check for alias collisions
    IF EXISTS (SELECT Alias FROM @Metrics GROUP BY Alias HAVING COUNT(1) > 1)
    BEGIN
        RAISERROR('Duplicate metric alias detected.', 16, 1);
        RETURN;
    END
    
    -- Parse and validate groupBy
    DECLARE @GroupBy TABLE (
        Ordinal INT NOT NULL,
        FieldKey NVARCHAR(200) NOT NULL,
        PhysicalColumn NVARCHAR(200) NOT NULL
    );
    
    INSERT INTO @GroupBy (Ordinal, FieldKey, PhysicalColumn)
    SELECT CAST(j.[key] AS INT), j.value, f.PhysicalColumn
    FROM OPENJSON(@ArgsJson, '$.groupBy') j
    INNER JOIN @Fields f ON f.FieldKey = j.value AND f.IsGroupable = 1;
    
    IF @GroupByCount > 0 AND (SELECT COUNT(1) FROM @GroupBy) <> @GroupByCount
    BEGIN
        RAISERROR('Unknown or non-groupable field in groupBy.', 16, 1);
        RETURN;
    END
    
    -- Parse and validate where clauses
    DECLARE @Where TABLE (
        Ordinal INT NOT NULL,
        FieldKey NVARCHAR(200) NOT NULL,
        PhysicalColumn NVARCHAR(200) NOT NULL,
        Op NVARCHAR(20) NOT NULL,
        ValueText NVARCHAR(MAX) NULL,
        ValuesJson NVARCHAR(MAX) NULL
    );
    
    IF JSON_QUERY(@ArgsJson, '$.where') IS NOT NULL
    BEGIN
        INSERT INTO @Where (Ordinal, FieldKey, PhysicalColumn, Op, ValueText, ValuesJson)
        SELECT 
            CAST(j.[key] AS INT),
            LTRIM(RTRIM(d.field)),
            f.PhysicalColumn,
            LOWER(LTRIM(RTRIM(d.op))),
            d.value,
            d.[values]
        FROM OPENJSON(@ArgsJson, '$.where') j
        CROSS APPLY (
            SELECT
                JSON_VALUE(j.value, '$.field') AS field,
                JSON_VALUE(j.value, '$.op') AS op,
                JSON_VALUE(j.value, '$.value') AS value,
                JSON_QUERY(j.value, '$.values') AS [values]
        ) d
        INNER JOIN @Fields f ON f.FieldKey = d.field AND f.IsFilterable = 1;
        
        -- Validate all where entries resolved
        DECLARE @WhereInputCount INT = (SELECT COUNT(1) FROM OPENJSON(@ArgsJson, '$.where'));
        IF (SELECT COUNT(1) FROM @Where) <> @WhereInputCount
        BEGIN
            RAISERROR('Unknown or non-filterable field in where clause.', 16, 1);
            RETURN;
        END
        
        -- Validate where operators
        IF EXISTS (
            SELECT 1 FROM @Where 
            WHERE Op NOT IN ('eq','ne','gt','gte','lt','lte','like','in','between')
        )
        BEGIN
            RAISERROR('Invalid where operator.', 16, 1);
            RETURN;
        END
    END
    
    -- Parse and validate orderBy
    DECLARE @OrderBy TABLE (
        Ordinal INT NOT NULL,
        FieldOrAlias NVARCHAR(200) NOT NULL,
        Dir NVARCHAR(4) NOT NULL
    );
    
    IF JSON_QUERY(@ArgsJson, '$.orderBy') IS NOT NULL
    BEGIN
        INSERT INTO @OrderBy (Ordinal, FieldOrAlias, Dir)
        SELECT 
            CAST(j.[key] AS INT),
            LTRIM(RTRIM(JSON_VALUE(j.value, '$.field'))),
            UPPER(ISNULL(LTRIM(RTRIM(JSON_VALUE(j.value, '$.dir'))), 'ASC'))
        FROM OPENJSON(@ArgsJson, '$.orderBy') j;
        
        -- Validate dir
        IF EXISTS (SELECT 1 FROM @OrderBy WHERE Dir NOT IN ('ASC', 'DESC'))
        BEGIN
            RAISERROR('Invalid orderBy direction. Use ASC or DESC.', 16, 1);
            RETURN;
        END
        
        -- Validate field/alias exists (in groupBy or metrics)
        IF EXISTS (
            SELECT 1 FROM @OrderBy o
            WHERE o.FieldOrAlias NOT IN (SELECT FieldKey FROM @GroupBy)
              AND o.FieldOrAlias NOT IN (SELECT Alias FROM @Metrics)
        )
        BEGIN
            RAISERROR('orderBy field must be a groupBy field or metric alias.', 16, 1);
            RETURN;
        END
    END
    
    -- Build SELECT clause for metrics
    DECLARE @SelectList NVARCHAR(MAX) = '';
    
    -- Add groupBy fields first
    SELECT @SelectList = @SelectList + 
        CASE WHEN @SelectList = '' THEN '' ELSE ', ' END +
        'src.' + QUOTENAME(PhysicalColumn) + ' AS ' + QUOTENAME(FieldKey)
    FROM @GroupBy
    ORDER BY Ordinal;
    
    -- Add metrics
    SELECT @SelectList = @SelectList + 
        CASE WHEN @SelectList = '' THEN '' ELSE ', ' END +
        CASE m.Op
            WHEN 'count' THEN 'COUNT(' + CASE WHEN m.PhysicalColumn = '*' THEN '*' ELSE 'src.' + QUOTENAME(m.PhysicalColumn) END + ')'
            WHEN 'countDistinct' THEN 'COUNT(DISTINCT src.' + QUOTENAME(m.PhysicalColumn) + ')'
            WHEN 'sum' THEN 'SUM(src.' + QUOTENAME(m.PhysicalColumn) + ')'
            WHEN 'avg' THEN 'AVG(CAST(src.' + QUOTENAME(m.PhysicalColumn) + ' AS FLOAT))'
            WHEN 'min' THEN 'MIN(src.' + QUOTENAME(m.PhysicalColumn) + ')'
            WHEN 'max' THEN 'MAX(src.' + QUOTENAME(m.PhysicalColumn) + ')'
        END + ' AS ' + QUOTENAME(m.Alias)
    FROM @Metrics m
    ORDER BY m.Ordinal;
    
    -- Build WHERE clause
    DECLARE @WhereSql NVARCHAR(MAX) = '';
    
    -- Tenant filter
    IF @HasTenantColumn = 1
    BEGIN
        SET @WhereSql = ' AND src.[TenantId] = @TenantId';
    END
    
    -- User filters
    SELECT @WhereSql = @WhereSql +
        CASE w.Op
            WHEN 'eq' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' = JSON_VALUE(@ArgsJson, ''$.where[' + CAST(w.Ordinal AS NVARCHAR(10)) + '].value'')'
            WHEN 'ne' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' <> JSON_VALUE(@ArgsJson, ''$.where[' + CAST(w.Ordinal AS NVARCHAR(10)) + '].value'')'
            WHEN 'gt' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' > JSON_VALUE(@ArgsJson, ''$.where[' + CAST(w.Ordinal AS NVARCHAR(10)) + '].value'')'
            WHEN 'gte' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' >= JSON_VALUE(@ArgsJson, ''$.where[' + CAST(w.Ordinal AS NVARCHAR(10)) + '].value'')'
            WHEN 'lt' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' < JSON_VALUE(@ArgsJson, ''$.where[' + CAST(w.Ordinal AS NVARCHAR(10)) + '].value'')'
            WHEN 'lte' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' <= JSON_VALUE(@ArgsJson, ''$.where[' + CAST(w.Ordinal AS NVARCHAR(10)) + '].value'')'
            WHEN 'like' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' LIKE JSON_VALUE(@ArgsJson, ''$.where[' + CAST(w.Ordinal AS NVARCHAR(10)) + '].value'')'
            WHEN 'in' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' IN (SELECT value FROM OPENJSON(@ArgsJson, ''$.where[' + CAST(w.Ordinal AS NVARCHAR(10)) + '].values''))'
            WHEN 'between' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' BETWEEN JSON_VALUE(@ArgsJson, ''$.where[' + CAST(w.Ordinal AS NVARCHAR(10)) + '].values[0]'') AND JSON_VALUE(@ArgsJson, ''$.where[' + CAST(w.Ordinal AS NVARCHAR(10)) + '].values[1]'')'
        END
    FROM @Where w
    ORDER BY w.Ordinal;
    
    -- Build GROUP BY clause
    DECLARE @GroupBySql NVARCHAR(MAX) = '';
    IF EXISTS (SELECT 1 FROM @GroupBy)
    BEGIN
        SELECT @GroupBySql = @GroupBySql + 
            CASE WHEN @GroupBySql = '' THEN ' GROUP BY ' ELSE ', ' END +
            'src.' + QUOTENAME(PhysicalColumn)
        FROM @GroupBy
        ORDER BY Ordinal;
    END
    
    -- Build ORDER BY clause
    DECLARE @OrderBySql NVARCHAR(MAX) = '';
    IF EXISTS (SELECT 1 FROM @OrderBy)
    BEGIN
        SELECT @OrderBySql = @OrderBySql +
            CASE WHEN @OrderBySql = '' THEN ' ORDER BY ' ELSE ', ' END +
            CASE 
                WHEN EXISTS (SELECT 1 FROM @Metrics WHERE Alias = o.FieldOrAlias) 
                THEN QUOTENAME(o.FieldOrAlias)
                ELSE 'src.' + QUOTENAME((SELECT TOP 1 PhysicalColumn FROM @GroupBy WHERE FieldKey = o.FieldOrAlias))
            END + ' ' + o.Dir
        FROM @OrderBy o
        ORDER BY o.Ordinal;
    END
    ELSE IF EXISTS (SELECT 1 FROM @Metrics)
    BEGIN
        -- Default order by first metric descending
        SELECT TOP 1 @OrderBySql = ' ORDER BY ' + QUOTENAME(Alias) + ' DESC' FROM @Metrics ORDER BY Ordinal;
    END
    
    -- Create temp table for results
    IF OBJECT_ID('tempdb..#MetricsResults') IS NOT NULL DROP TABLE #MetricsResults;
    
    -- Build and execute dynamic SQL
    DECLARE @Sql NVARCHAR(MAX) = N'
SELECT TOP (@Limit) ' + @SelectList + '
INTO #MetricsResults
FROM ' + @BaseObjectSql + ' AS src
WHERE 1 = 1' + @WhereSql + @GroupBySql + @OrderBySql + ';

SELECT * FROM #MetricsResults;';

    DECLARE @ParamDef NVARCHAR(500) = N'@ArgsJson NVARCHAR(MAX), @TenantId NVARCHAR(50), @Limit INT';
    
    -- Execute query
    DECLARE @ResultRows TABLE (ResultJson NVARCHAR(MAX));
    
    BEGIN TRY
        -- Execute the aggregation query
        EXEC sp_executesql @Sql, @ParamDef, 
            @ArgsJson = @ArgsJson, 
            @TenantId = @TenantId, 
            @Limit = @Limit;
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMsg NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR('Query execution failed: %s', 16, 1, @ErrorMsg);
        RETURN;
    END CATCH
    
    -- Get row count from last execution
    DECLARE @RowCount INT = @@ROWCOUNT;
    
    -- Calculate duration
    DECLARE @EndTime DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @DurationMs INT = DATEDIFF(MILLISECOND, @StartTime, @EndTime);
    
    -- Build warnings
    DECLARE @Warnings TABLE (Warning NVARCHAR(500));
    IF @Truncated = 1
    BEGIN
        INSERT INTO @Warnings (Warning) VALUES ('Results truncated to ' + CAST(@MaxRows AS NVARCHAR(10)) + ' rows.');
    END
    
    -- Build column metadata from metrics and groupBy
    DECLARE @Columns TABLE (
        Ordinal INT,
        Name NVARCHAR(200),
        DataType NVARCHAR(50),
        IsMetric BIT
    );
    
    INSERT INTO @Columns (Ordinal, Name, DataType, IsMetric)
    SELECT Ordinal, FieldKey, 
           (SELECT DataType FROM @Fields WHERE FieldKey = g.FieldKey),
           0
    FROM @GroupBy g;
    
    INSERT INTO @Columns (Ordinal, Name, DataType, IsMetric)
    SELECT Ordinal + 1000, Alias, 
           CASE WHEN Op IN ('count', 'countDistinct') THEN 'int' ELSE 'decimal' END,
           1
    FROM @Metrics;
    
    -- Return JSON envelope (note: actual rows were returned by the select above)
    -- The calling application should combine the metadata with the result set
    SELECT (
        SELECT
            meta = (
                SELECT
                    @RowCount AS [rowCount],
                    @Truncated AS truncated,
                    @DurationMs AS durationMs,
                    freshness = (
                        SELECT
                            @GeneratedAtUtc AS asOfUtc,
                            'SQL' AS [source]
                        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                    )
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            [columns] = (
                SELECT [Name] AS [name], DataType AS [type], IsMetric AS isMetric
                FROM @Columns
                ORDER BY Ordinal
                FOR JSON PATH
            ),
            warnings = (
                SELECT Warning AS [warning]
                FROM @Warnings
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS MetaJson;
END;
GO
