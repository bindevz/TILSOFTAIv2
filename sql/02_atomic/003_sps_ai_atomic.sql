SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.ai_atomic_execute_plan
    @TenantId nvarchar(50),
    @PlanJson nvarchar(max),
    @CallerUserId nvarchar(50),
    @CallerRoles nvarchar(1000)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @MaxLimit int = 5000;
    DECLARE @DefaultLimit int = 200;

    IF ISJSON(@PlanJson) <> 1
    BEGIN
        RAISERROR('PlanJson must be valid JSON.', 16, 1);
        RETURN;
    END

    DECLARE @DatasetKey nvarchar(200) = LTRIM(RTRIM(JSON_VALUE(@PlanJson, '$.datasetKey')));
    IF @DatasetKey IS NULL OR @DatasetKey = ''
    BEGIN
        RAISERROR('datasetKey is required.', 16, 1);
        RETURN;
    END

    DECLARE @Limit int = TRY_CONVERT(int, JSON_VALUE(@PlanJson, '$.limit'));
    IF @Limit IS NULL OR @Limit <= 0
    BEGIN
        SET @Limit = @DefaultLimit;
    END

    DECLARE @LimitCapped bit = 0;
    IF @Limit > @MaxLimit
    BEGIN
        SET @Limit = @MaxLimit;
        SET @LimitCapped = 1;
    END

    DECLARE @Offset int = TRY_CONVERT(int, JSON_VALUE(@PlanJson, '$.offset'));
    IF @Offset IS NULL OR @Offset < 0
    BEGIN
        SET @Offset = 0;
    END

    DECLARE @BaseObjectRaw nvarchar(200);
    DECLARE @TimeColumn nvarchar(200);
    DECLARE @DatasetTenantId nvarchar(50);

    SELECT TOP 1
        @BaseObjectRaw = BaseObject,
        @TimeColumn = TimeColumn,
        @DatasetTenantId = TenantId
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

    DECLARE @BaseSchema sysname = PARSENAME(@BaseObjectRaw, 2);
    DECLARE @BaseName sysname = PARSENAME(@BaseObjectRaw, 1);
    IF @BaseName IS NULL
    BEGIN
        RAISERROR('BaseObject must be schema-qualified.', 16, 1);
        RETURN;
    END

    IF @BaseSchema IS NULL
    BEGIN
        SET @BaseSchema = 'dbo';
    END

    DECLARE @BaseObjectId int = OBJECT_ID(QUOTENAME(@BaseSchema) + '.' + QUOTENAME(@BaseName));
    IF @BaseObjectId IS NULL
    BEGIN
        RAISERROR('BaseObject does not exist.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.objects
        WHERE object_id = @BaseObjectId
          AND type IN ('V', 'IF')
    )
    BEGIN
        RAISERROR('BaseObject must be a view or inline table-valued function.', 16, 1);
        RETURN;
    END

    DECLARE @BaseObjectSql nvarchar(300) = QUOTENAME(@BaseSchema) + '.' + QUOTENAME(@BaseName);
    IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = @BaseObjectId AND type = 'IF')
       AND CHARINDEX('(', @BaseObjectRaw) = 0
    BEGIN
        SET @BaseObjectSql = @BaseObjectSql + '()';
    END

    DECLARE @Fields TABLE
    (
        FieldKey nvarchar(200) NOT NULL,
        PhysicalColumn nvarchar(200) NOT NULL,
        DataType nvarchar(50) NOT NULL,
        IsFilterable bit NOT NULL,
        IsGroupable bit NOT NULL,
        IsSortable bit NOT NULL
    );

    INSERT INTO @Fields (FieldKey, PhysicalColumn, DataType, IsFilterable, IsGroupable, IsSortable)
    SELECT
        FieldKey,
        PhysicalColumn,
        DataType,
        IsFilterable,
        IsGroupable,
        IsSortable
    FROM dbo.FieldCatalog
    WHERE DatasetKey = @DatasetKey
      AND IsEnabled = 1;

    IF NOT EXISTS (SELECT 1 FROM @Fields)
    BEGIN
        RAISERROR('No enabled fields found for dataset.', 16, 1);
        RETURN;
    END

    DECLARE @HasTenantColumn bit = CASE
        WHEN EXISTS (
            SELECT 1
            FROM sys.columns
            WHERE object_id = @BaseObjectId
              AND name = 'TenantId'
        ) THEN 1 ELSE 0 END;

    DECLARE @TenantPhysicalColumn nvarchar(200) = NULL;
    IF @HasTenantColumn = 1
    BEGIN
        SELECT TOP 1 @TenantPhysicalColumn = PhysicalColumn
        FROM @Fields
        WHERE FieldKey = 'tenantId' AND PhysicalColumn = 'TenantId';

        IF @TenantPhysicalColumn IS NULL
        BEGIN
            RAISERROR('Tenant enforcement not possible for dataset.', 16, 1);
            RETURN;
        END
    END

    DECLARE @TimePhysicalColumn nvarchar(200) = NULL;
    IF @TimeColumn IS NOT NULL AND LTRIM(RTRIM(@TimeColumn)) <> ''
    BEGIN
        SELECT TOP 1 @TimePhysicalColumn = PhysicalColumn
        FROM @Fields
        WHERE PhysicalColumn = @TimeColumn;

        IF @TimePhysicalColumn IS NULL
        BEGIN
            RAISERROR('TimeColumn not present in FieldCatalog.', 16, 1);
            RETURN;
        END
    END

    DECLARE @Select TABLE (Ordinal int NOT NULL, FieldKey nvarchar(200) NOT NULL);
    INSERT INTO @Select (Ordinal, FieldKey)
    SELECT CAST([key] AS int), value
    FROM OPENJSON(@PlanJson, '$.select');

    IF NOT EXISTS (SELECT 1 FROM @Select)
    BEGIN
        RAISERROR('select must be a non-empty array.', 16, 1);
        RETURN;
    END

    IF EXISTS (
        SELECT 1
        FROM @Select s
        LEFT JOIN @Fields f ON f.FieldKey = s.FieldKey
        WHERE f.FieldKey IS NULL
    )
    BEGIN
        RAISERROR('Unknown field in select.', 16, 1);
        RETURN;
    END

    DECLARE @SelectMap TABLE
    (
        Ordinal int NOT NULL,
        FieldKey nvarchar(200) NOT NULL,
        PhysicalColumn nvarchar(200) NOT NULL,
        DataType nvarchar(50) NOT NULL
    );

    INSERT INTO @SelectMap (Ordinal, FieldKey, PhysicalColumn, DataType)
    SELECT s.Ordinal, s.FieldKey, f.PhysicalColumn, f.DataType
    FROM @Select s
    INNER JOIN @Fields f ON f.FieldKey = s.FieldKey;

    DECLARE @GroupBy TABLE (Ordinal int NOT NULL, FieldKey nvarchar(200) NOT NULL, PhysicalColumn nvarchar(200) NOT NULL);
    IF JSON_QUERY(@PlanJson, '$.groupBy') IS NOT NULL
    BEGIN
        INSERT INTO @GroupBy (Ordinal, FieldKey, PhysicalColumn)
        SELECT CAST(j.[key] AS int), j.value, f.PhysicalColumn
        FROM OPENJSON(@PlanJson, '$.groupBy') j
        INNER JOIN @Fields f ON f.FieldKey = j.value
        WHERE f.IsGroupable = 1;

        IF EXISTS (
            SELECT 1
            FROM OPENJSON(@PlanJson, '$.groupBy') j
            LEFT JOIN @Fields f ON f.FieldKey = j.value
            WHERE f.FieldKey IS NULL OR f.IsGroupable = 0
        )
        BEGIN
            RAISERROR('Unknown or non-groupable field in groupBy.', 16, 1);
            RETURN;
        END
    END

    DECLARE @OrderBy TABLE (Ordinal int NOT NULL, FieldKey nvarchar(200) NOT NULL, PhysicalColumn nvarchar(200) NOT NULL, Dir nvarchar(4) NOT NULL);
    IF JSON_QUERY(@PlanJson, '$.orderBy') IS NOT NULL
    BEGIN
        INSERT INTO @OrderBy (Ordinal, FieldKey, PhysicalColumn, Dir)
        SELECT CAST(j.[key] AS int),
               LTRIM(RTRIM(d.field)),
               f.PhysicalColumn,
               LOWER(LTRIM(RTRIM(d.dir)))
        FROM OPENJSON(@PlanJson, '$.orderBy') j
        CROSS APPLY (
            SELECT
                JSON_VALUE(j.value, '$.field') AS field,
                JSON_VALUE(j.value, '$.dir') AS dir
        ) d
        INNER JOIN @Fields f ON f.FieldKey = d.field
        WHERE f.IsSortable = 1;

        IF EXISTS (
            SELECT 1
            FROM OPENJSON(@PlanJson, '$.orderBy') j
            CROSS APPLY (
                SELECT
                    JSON_VALUE(j.value, '$.field') AS field,
                    JSON_VALUE(j.value, '$.dir') AS dir
            ) d
            LEFT JOIN @Fields f ON f.FieldKey = d.field
            WHERE f.FieldKey IS NULL OR f.IsSortable = 0 OR d.dir IS NULL OR LOWER(LTRIM(RTRIM(d.dir))) NOT IN ('asc', 'desc')
        )
        BEGIN
            RAISERROR('Invalid orderBy entry.', 16, 1);
            RETURN;
        END
    END

    DECLARE @Where TABLE
    (
        Ordinal int NOT NULL,
        FieldKey nvarchar(200) NOT NULL,
        PhysicalColumn nvarchar(200) NOT NULL,
        Op nvarchar(20) NOT NULL,
        ValueText nvarchar(max) NULL,
        ValuesJson nvarchar(max) NULL
    );

    IF JSON_QUERY(@PlanJson, '$.where') IS NOT NULL
    BEGIN
        INSERT INTO @Where (Ordinal, FieldKey, PhysicalColumn, Op, ValueText, ValuesJson)
        SELECT CAST(j.[key] AS int),
               LTRIM(RTRIM(d.field)),
               f.PhysicalColumn,
               LOWER(LTRIM(RTRIM(d.op))),
               d.value,
               d.values
        FROM OPENJSON(@PlanJson, '$.where') j
        CROSS APPLY (
            SELECT
                JSON_VALUE(j.value, '$.field') AS field,
                JSON_VALUE(j.value, '$.op') AS op,
                JSON_VALUE(j.value, '$.value') AS value,
                JSON_QUERY(j.value, '$.values') AS values
        ) d
        INNER JOIN @Fields f ON f.FieldKey = d.field
        WHERE f.IsFilterable = 1;

        IF EXISTS (
            SELECT 1
            FROM OPENJSON(@PlanJson, '$.where') j
            CROSS APPLY (
                SELECT
                    JSON_VALUE(j.value, '$.field') AS field,
                    JSON_VALUE(j.value, '$.op') AS op,
                    JSON_VALUE(j.value, '$.value') AS value,
                    JSON_QUERY(j.value, '$.values') AS values
            ) d
            LEFT JOIN @Fields f ON f.FieldKey = d.field
            WHERE f.FieldKey IS NULL OR f.IsFilterable = 0 OR d.op IS NULL OR LOWER(LTRIM(RTRIM(d.op))) NOT IN
                ('eq','ne','gt','gte','lt','lte','like','in','between')
        )
        BEGIN
            RAISERROR('Invalid where clause entry.', 16, 1);
            RETURN;
        END

        IF EXISTS (
            SELECT 1 FROM @Where w
            WHERE w.Op IN ('in','between')
              AND (w.ValuesJson IS NULL OR ISJSON(w.ValuesJson) <> 1)
        )
        BEGIN
            RAISERROR('where.values must be an array for in/between.', 16, 1);
            RETURN;
        END

        IF EXISTS (
            SELECT 1
            FROM @Where w
            OUTER APPLY (SELECT COUNT(1) AS Cnt FROM OPENJSON(w.ValuesJson)) v
            WHERE w.Op = 'between' AND ISNULL(v.Cnt, 0) <> 2
        )
        BEGIN
            RAISERROR('between requires exactly 2 values.', 16, 1);
            RETURN;
        END

        IF EXISTS (
            SELECT 1
            FROM @Where w
            OUTER APPLY (SELECT COUNT(1) AS Cnt FROM OPENJSON(w.ValuesJson)) v
            WHERE w.Op = 'in' AND ISNULL(v.Cnt, 0) < 1
        )
        BEGIN
            RAISERROR('in requires at least one value.', 16, 1);
            RETURN;
        END

        IF EXISTS (
            SELECT 1
            FROM @Where w
            WHERE w.Op NOT IN ('in','between') AND w.ValueText IS NULL
        )
        BEGIN
            RAISERROR('where.value is required for specified operator.', 16, 1);
            RETURN;
        END
    END

    DECLARE @DrilldownDatasetKey nvarchar(200) = LTRIM(RTRIM(JSON_VALUE(@PlanJson, '$.drilldown.toDatasetKey')));
    DECLARE @JoinKey nvarchar(200) = LTRIM(RTRIM(JSON_VALUE(@PlanJson, '$.drilldown.joinKey')));
    DECLARE @JoinClause nvarchar(max) = '';
    DECLARE @DrilldownWhereSql nvarchar(max) = '';
    DECLARE @DrilldownTenantFilter nvarchar(max) = '';

    IF (@DrilldownDatasetKey IS NOT NULL OR @JoinKey IS NOT NULL)
    BEGIN
        IF @DrilldownDatasetKey IS NULL OR @JoinKey IS NULL
        BEGIN
            RAISERROR('drilldown requires toDatasetKey and joinKey.', 16, 1);
            RETURN;
        END

        DECLARE @DrilldownBaseObjectRaw nvarchar(200);
        SELECT TOP 1 @DrilldownBaseObjectRaw = BaseObject
        FROM dbo.DatasetCatalog
        WHERE DatasetKey = @DrilldownDatasetKey
          AND IsEnabled = 1
          AND (TenantId = @TenantId OR TenantId IS NULL)
        ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END;

        IF @DrilldownBaseObjectRaw IS NULL
        BEGIN
            RAISERROR('drilldown dataset not found or disabled.', 16, 1);
            RETURN;
        END

        DECLARE @DrilldownSchema sysname = PARSENAME(@DrilldownBaseObjectRaw, 2);
        DECLARE @DrilldownName sysname = PARSENAME(@DrilldownBaseObjectRaw, 1);
        IF @DrilldownName IS NULL
        BEGIN
            RAISERROR('Drilldown BaseObject must be schema-qualified.', 16, 1);
            RETURN;
        END

        IF @DrilldownSchema IS NULL
        BEGIN
            SET @DrilldownSchema = 'dbo';
        END

        DECLARE @DrilldownObjectId int = OBJECT_ID(QUOTENAME(@DrilldownSchema) + '.' + QUOTENAME(@DrilldownName));
        IF @DrilldownObjectId IS NULL
        BEGIN
            RAISERROR('Drilldown BaseObject does not exist.', 16, 1);
            RETURN;
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.objects
            WHERE object_id = @DrilldownObjectId
              AND type IN ('V', 'IF')
        )
        BEGIN
            RAISERROR('Drilldown BaseObject must be a view or inline table-valued function.', 16, 1);
            RETURN;
        END

        DECLARE @DrilldownObjectSql nvarchar(300) = QUOTENAME(@DrilldownSchema) + '.' + QUOTENAME(@DrilldownName);
        IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = @DrilldownObjectId AND type = 'IF')
           AND CHARINDEX('(', @DrilldownBaseObjectRaw) = 0
        BEGIN
            SET @DrilldownObjectSql = @DrilldownObjectSql + '()';
        END

        DECLARE @DrilldownFields TABLE
        (
            FieldKey nvarchar(200) NOT NULL,
            PhysicalColumn nvarchar(200) NOT NULL,
            IsFilterable bit NOT NULL
        );

        INSERT INTO @DrilldownFields (FieldKey, PhysicalColumn, IsFilterable)
        SELECT FieldKey, PhysicalColumn, IsFilterable
        FROM dbo.FieldCatalog
        WHERE DatasetKey = @DrilldownDatasetKey
          AND IsEnabled = 1;

        IF NOT EXISTS (SELECT 1 FROM @DrilldownFields)
        BEGIN
            RAISERROR('No enabled fields found for drilldown dataset.', 16, 1);
            RETURN;
        END

        DECLARE @DrilldownHasTenantColumn bit = CASE
            WHEN EXISTS (
                SELECT 1
                FROM sys.columns
                WHERE object_id = @DrilldownObjectId
                  AND name = 'TenantId'
            ) THEN 1 ELSE 0 END;

        DECLARE @DrilldownTenantPhysicalColumn nvarchar(200) = NULL;
        IF @DrilldownHasTenantColumn = 1
        BEGIN
            SELECT TOP 1 @DrilldownTenantPhysicalColumn = PhysicalColumn
            FROM @DrilldownFields
            WHERE FieldKey = 'tenantId' AND PhysicalColumn = 'TenantId';

            IF @DrilldownTenantPhysicalColumn IS NULL
            BEGIN
                RAISERROR('Tenant enforcement not possible for drilldown dataset.', 16, 1);
                RETURN;
            END
        END

        DECLARE @JoinType nvarchar(50);
        DECLARE @JoinConditionTemplate nvarchar(2000);
        DECLARE @GraphFrom nvarchar(200);
        DECLARE @GraphTo nvarchar(200);

        SELECT TOP 1
            @JoinType = JoinType,
            @JoinConditionTemplate = JoinConditionTemplate,
            @GraphFrom = FromDatasetKey,
            @GraphTo = ToDatasetKey
        FROM dbo.EntityGraphCatalog
        WHERE GraphKey = @JoinKey
          AND IsEnabled = 1
          AND (
              (FromDatasetKey = @DatasetKey AND ToDatasetKey = @DrilldownDatasetKey)
              OR (FromDatasetKey = @DrilldownDatasetKey AND ToDatasetKey = @DatasetKey)
          );

        IF @JoinType IS NULL OR @JoinConditionTemplate IS NULL
        BEGIN
            RAISERROR('drilldown joinKey is not valid for the datasets.', 16, 1);
            RETURN;
        END

        IF CHARINDEX('{fromAlias}', @JoinConditionTemplate) = 0 OR CHARINDEX('{toAlias}', @JoinConditionTemplate) = 0
        BEGIN
            RAISERROR('JoinConditionTemplate must include {fromAlias} and {toAlias}.', 16, 1);
            RETURN;
        END

        DECLARE @FromAlias nvarchar(20) = 'src';
        DECLARE @ToAlias nvarchar(20) = 'dd';
        IF @GraphFrom = @DrilldownDatasetKey
        BEGIN
            SET @FromAlias = 'dd';
            SET @ToAlias = 'src';
        END

        DECLARE @JoinCondition nvarchar(2000) =
            REPLACE(REPLACE(@JoinConditionTemplate, '{fromAlias}', @FromAlias), '{toAlias}', @ToAlias);

        SET @JoinClause = ' ' + @JoinType + ' JOIN ' + @DrilldownObjectSql + ' AS dd ON ' + @JoinCondition;

        IF @DrilldownHasTenantColumn = 1
        BEGIN
            SET @DrilldownTenantFilter = ' AND dd.' + QUOTENAME(@DrilldownTenantPhysicalColumn) + ' = @TenantId';
        END

        IF JSON_QUERY(@PlanJson, '$.drilldown.where') IS NOT NULL
        BEGIN
            DECLARE @DrilldownWhere TABLE
            (
                Ordinal int NOT NULL,
                FieldKey nvarchar(200) NOT NULL,
                PhysicalColumn nvarchar(200) NOT NULL,
                Op nvarchar(20) NOT NULL,
                ValueText nvarchar(max) NULL,
                ValuesJson nvarchar(max) NULL
            );

            INSERT INTO @DrilldownWhere (Ordinal, FieldKey, PhysicalColumn, Op, ValueText, ValuesJson)
            SELECT CAST(j.[key] AS int),
                   LTRIM(RTRIM(d.field)),
                   f.PhysicalColumn,
                   LOWER(LTRIM(RTRIM(d.op))),
                   d.value,
                   d.values
            FROM OPENJSON(@PlanJson, '$.drilldown.where') j
            CROSS APPLY (
                SELECT
                    JSON_VALUE(j.value, '$.field') AS field,
                    JSON_VALUE(j.value, '$.op') AS op,
                    JSON_VALUE(j.value, '$.value') AS value,
                    JSON_QUERY(j.value, '$.values') AS values
            ) d
            INNER JOIN @DrilldownFields f ON f.FieldKey = d.field
            WHERE f.IsFilterable = 1;

            IF EXISTS (
                SELECT 1
                FROM OPENJSON(@PlanJson, '$.drilldown.where') j
                CROSS APPLY (
                    SELECT
                        JSON_VALUE(j.value, '$.field') AS field,
                        JSON_VALUE(j.value, '$.op') AS op,
                        JSON_VALUE(j.value, '$.value') AS value,
                        JSON_QUERY(j.value, '$.values') AS values
                ) d
                LEFT JOIN @DrilldownFields f ON f.FieldKey = d.field
                WHERE f.FieldKey IS NULL OR f.IsFilterable = 0 OR d.op IS NULL OR LOWER(LTRIM(RTRIM(d.op))) NOT IN
                    ('eq','ne','gt','gte','lt','lte','like','in','between')
            )
            BEGIN
                RAISERROR('Invalid drilldown where clause entry.', 16, 1);
                RETURN;
            END

            IF EXISTS (
                SELECT 1 FROM @DrilldownWhere w
                WHERE w.Op IN ('in','between')
                  AND (w.ValuesJson IS NULL OR ISJSON(w.ValuesJson) <> 1)
            )
            BEGIN
                RAISERROR('drilldown.where.values must be an array for in/between.', 16, 1);
                RETURN;
            END

            IF EXISTS (
                SELECT 1
                FROM @DrilldownWhere w
                OUTER APPLY (SELECT COUNT(1) AS Cnt FROM OPENJSON(w.ValuesJson)) v
                WHERE w.Op = 'between' AND ISNULL(v.Cnt, 0) <> 2
            )
            BEGIN
                RAISERROR('drilldown.between requires exactly 2 values.', 16, 1);
                RETURN;
            END

            IF EXISTS (
                SELECT 1
                FROM @DrilldownWhere w
                OUTER APPLY (SELECT COUNT(1) AS Cnt FROM OPENJSON(w.ValuesJson)) v
                WHERE w.Op = 'in' AND ISNULL(v.Cnt, 0) < 1
            )
            BEGIN
                RAISERROR('drilldown.in requires at least one value.', 16, 1);
                RETURN;
            END

            IF EXISTS (
                SELECT 1
                FROM @DrilldownWhere w
                WHERE w.Op NOT IN ('in','between') AND w.ValueText IS NULL
            )
            BEGIN
                RAISERROR('drilldown.where.value is required for specified operator.', 16, 1);
                RETURN;
            END

            SELECT @DrilldownWhereSql = @DrilldownWhereSql +
                CASE w.Op
                    WHEN 'eq' THEN ' AND dd.' + QUOTENAME(w.PhysicalColumn) + ' = JSON_VALUE(@PlanJson, ''$.drilldown.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
                    WHEN 'ne' THEN ' AND dd.' + QUOTENAME(w.PhysicalColumn) + ' <> JSON_VALUE(@PlanJson, ''$.drilldown.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
                    WHEN 'gt' THEN ' AND dd.' + QUOTENAME(w.PhysicalColumn) + ' > JSON_VALUE(@PlanJson, ''$.drilldown.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
                    WHEN 'gte' THEN ' AND dd.' + QUOTENAME(w.PhysicalColumn) + ' >= JSON_VALUE(@PlanJson, ''$.drilldown.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
                    WHEN 'lt' THEN ' AND dd.' + QUOTENAME(w.PhysicalColumn) + ' < JSON_VALUE(@PlanJson, ''$.drilldown.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
                    WHEN 'lte' THEN ' AND dd.' + QUOTENAME(w.PhysicalColumn) + ' <= JSON_VALUE(@PlanJson, ''$.drilldown.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
                    WHEN 'like' THEN ' AND dd.' + QUOTENAME(w.PhysicalColumn) + ' LIKE JSON_VALUE(@PlanJson, ''$.drilldown.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
                    WHEN 'in' THEN ' AND dd.' + QUOTENAME(w.PhysicalColumn) + ' IN (SELECT value FROM OPENJSON(@PlanJson, ''$.drilldown.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].values''))'
                    WHEN 'between' THEN ' AND dd.' + QUOTENAME(w.PhysicalColumn) + ' BETWEEN JSON_VALUE(@PlanJson, ''$.drilldown.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].values[0]'') AND JSON_VALUE(@PlanJson, ''$.drilldown.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].values[1]'')'
                END
            FROM @DrilldownWhere w
            ORDER BY w.Ordinal;
        END
    END

    DECLARE @WhereSql nvarchar(max) = '';
    SELECT @WhereSql = @WhereSql +
        CASE w.Op
            WHEN 'eq' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' = JSON_VALUE(@PlanJson, ''$.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
            WHEN 'ne' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' <> JSON_VALUE(@PlanJson, ''$.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
            WHEN 'gt' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' > JSON_VALUE(@PlanJson, ''$.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
            WHEN 'gte' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' >= JSON_VALUE(@PlanJson, ''$.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
            WHEN 'lt' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' < JSON_VALUE(@PlanJson, ''$.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
            WHEN 'lte' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' <= JSON_VALUE(@PlanJson, ''$.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
            WHEN 'like' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' LIKE JSON_VALUE(@PlanJson, ''$.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].value'')'
            WHEN 'in' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' IN (SELECT value FROM OPENJSON(@PlanJson, ''$.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].values''))'
            WHEN 'between' THEN ' AND src.' + QUOTENAME(w.PhysicalColumn) + ' BETWEEN JSON_VALUE(@PlanJson, ''$.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].values[0]'') AND JSON_VALUE(@PlanJson, ''$.where[' + CAST(w.Ordinal AS nvarchar(10)) + '].values[1]'')'
        END
    FROM @Where w
    ORDER BY w.Ordinal;

    DECLARE @TimeSql nvarchar(max) = '';
    IF JSON_QUERY(@PlanJson, '$.timeRange') IS NOT NULL
    BEGIN
        IF @TimePhysicalColumn IS NULL
        BEGIN
            RAISERROR('timeRange provided but TimeColumn is not configured.', 16, 1);
            RETURN;
        END

        IF JSON_VALUE(@PlanJson, '$.timeRange.from') IS NULL OR JSON_VALUE(@PlanJson, '$.timeRange.to') IS NULL
        BEGIN
            RAISERROR('timeRange requires from/to.', 16, 1);
            RETURN;
        END

        SET @TimeSql = ' AND src.' + QUOTENAME(@TimePhysicalColumn)
            + ' >= TRY_CONVERT(datetime2, JSON_VALUE(@PlanJson, ''$.timeRange.from''))'
            + ' AND src.' + QUOTENAME(@TimePhysicalColumn)
            + ' <= TRY_CONVERT(datetime2, JSON_VALUE(@PlanJson, ''$.timeRange.to''))';
    END

    DECLARE @TenantFilter nvarchar(max) = '';
    IF @HasTenantColumn = 1
    BEGIN
        SET @TenantFilter = ' AND src.' + QUOTENAME(@TenantPhysicalColumn) + ' = @TenantId';
    END

    DECLARE @SelectList nvarchar(max) = '';
    SELECT @SelectList = @SelectList + CASE WHEN @SelectList = '' THEN '' ELSE ', ' END
        + 'src.' + QUOTENAME(PhysicalColumn) + ' AS ' + QUOTENAME(FieldKey)
    FROM @SelectMap
    ORDER BY Ordinal;

    DECLARE @GroupByList nvarchar(max) = '';
    SELECT @GroupByList = @GroupByList + CASE WHEN @GroupByList = '' THEN '' ELSE ', ' END
        + 'src.' + QUOTENAME(PhysicalColumn)
    FROM @GroupBy
    ORDER BY Ordinal;

    DECLARE @OrderByList nvarchar(max) = '';
    SELECT @OrderByList = @OrderByList + CASE WHEN @OrderByList = '' THEN '' ELSE ', ' END
        + 'src.' + QUOTENAME(PhysicalColumn) + ' ' + UPPER(Dir)
    FROM @OrderBy
    ORDER BY Ordinal;

    IF @OrderByList = ''
    BEGIN
        DECLARE @FallbackColumn nvarchar(200);
        SELECT TOP 1 @FallbackColumn = PhysicalColumn FROM @SelectMap ORDER BY Ordinal;
        SET @OrderByList = 'src.' + QUOTENAME(@FallbackColumn) + ' ASC';
    END

    IF OBJECT_ID('tempdb..#AtomicResults') IS NOT NULL
    BEGIN
        DROP TABLE #AtomicResults;
    END

    DECLARE @Sql nvarchar(max) = N'
SELECT ' + @SelectList + '
INTO #AtomicResults
FROM ' + @BaseObjectSql + ' AS src' + @JoinClause + '
WHERE 1 = 1' + @TenantFilter + @DrilldownTenantFilter + @WhereSql + @DrilldownWhereSql + @TimeSql
    + CASE WHEN @GroupByList <> '' THEN ' GROUP BY ' + @GroupByList ELSE '' END
    + ' ORDER BY ' + @OrderByList
    + ' OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY;';

    DECLARE @ParamDef nvarchar(max) = N'@PlanJson nvarchar(max), @TenantId nvarchar(50), @Limit int, @Offset int';
    EXEC sp_executesql @Sql, @ParamDef,
        @PlanJson = @PlanJson,
        @TenantId = @TenantId,
        @Limit = @Limit,
        @Offset = @Offset;

    DECLARE @RowCount int = (SELECT COUNT(1) FROM #AtomicResults);

    DECLARE @Columns TABLE ([name] nvarchar(200), [type] nvarchar(50), [descriptionKey] nvarchar(200), Ordinal int);
    INSERT INTO @Columns ([name], [type], [descriptionKey], Ordinal)
    SELECT FieldKey, DataType, FieldKey, Ordinal
    FROM @SelectMap
    ORDER BY Ordinal;

    DECLARE @Warnings TABLE ([warning] nvarchar(4000));
    IF @LimitCapped = 1
    BEGIN
        INSERT INTO @Warnings ([warning]) VALUES ('Limit capped to max allowed.');
    END

    DECLARE @WarningsJson nvarchar(max) = NULL;
    IF EXISTS (SELECT 1 FROM @Warnings)
    BEGIN
        SELECT @WarningsJson = (
            SELECT [warning]
            FROM @Warnings
            FOR JSON PATH
        );
    END

    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS rowCount,
                    @DatasetKey AS datasetKey,
                    JSON_QUERY(@PlanJson) AS [plan],
                    CASE WHEN @WarningsJson IS NOT NULL THEN JSON_QUERY(@WarningsJson) END AS warnings
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            columns = (
                SELECT [name], [type], [descriptionKey]
                FROM @Columns
                ORDER BY Ordinal
                FOR JSON PATH
            ),
            rows = (
                SELECT *
                FROM #AtomicResults
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO
