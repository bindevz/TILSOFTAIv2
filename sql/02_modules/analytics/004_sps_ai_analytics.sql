/*******************************************************************************
* TILSOFTAI Analytics Module - Model-Callable Stored Procedures (ai_*)
* Purpose: Schema RAG tools for LLM to discover datasets/fields
* 
* CRITICAL: All ai_* SPs MUST:
*   1. Accept @TenantId and enforce tenant scope
*   2. Return JSON envelope (meta + columns + rows)
*   3. Apply pagination/limit guards
*
* PATCH 28: Enhanced validation for metrics/joins/security/time-window
* PATCH 30.02: SECURITY - _roles is SERVER-INJECTED by C# tool handlers.
*              Never trust model-provided _roles. Treat missing _roles as empty.
*******************************************************************************/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- catalog_search: Search datasets and fields by query
CREATE OR ALTER PROCEDURE dbo.ai_catalog_search
    @TenantId   NVARCHAR(50),
    @Query      NVARCHAR(500),
    @TopK       INT = 5,
    @Domain     NVARCHAR(50) = 'internal'
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @GeneratedAtUtc DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @MaxTopK INT = 20;
    
    -- Validate inputs
    IF @TenantId IS NULL OR @TenantId = ''
    BEGIN
        RAISERROR('TenantId is required.', 16, 1);
        RETURN;
    END
    
    IF @Query IS NULL OR LTRIM(RTRIM(@Query)) = ''
    BEGIN
        RAISERROR('Query is required.', 16, 1);
        RETURN;
    END
    
    IF @TopK <= 0 SET @TopK = 5;
    IF @TopK > @MaxTopK SET @TopK = @MaxTopK;
    
    -- Normalize query for search
    DECLARE @NormalizedQuery NVARCHAR(500) = LOWER(LTRIM(RTRIM(@Query)));
    
    -- Search datasets
    DECLARE @Datasets TABLE (
        DatasetKey NVARCHAR(200),
        DisplayName NVARCHAR(500),
        Description NVARCHAR(2000),
        Grain NVARCHAR(200),
        Score INT
    );
    
    INSERT INTO @Datasets (DatasetKey, DisplayName, Description, Grain, Score)
    SELECT TOP (@TopK)
        DatasetKey,
        DisplayName,
        Description,
        Grain,
        -- Simple scoring: exact match > contains > partial
        CASE 
            WHEN LOWER(DatasetKey) = @NormalizedQuery THEN 100
            WHEN SearchText LIKE '%' + @NormalizedQuery + '%' THEN 50 + EnabledFieldCount
            ELSE EnabledFieldCount
        END AS Score
    FROM dbo.v_Analytics_DatasetCatalog
    WHERE (TenantId = @TenantId OR TenantId IS NULL)
      AND (
          LOWER(DatasetKey) = @NormalizedQuery
          OR SearchText LIKE '%' + @NormalizedQuery + '%'
      )
    ORDER BY Score DESC, DatasetKey;
    
    -- Search fields across matched datasets
    DECLARE @Fields TABLE (
        DatasetKey NVARCHAR(200),
        FieldKey NVARCHAR(200),
        DisplayName NVARCHAR(500),
        DataType NVARCHAR(50),
        SemanticType NVARCHAR(100),
        IsFilterable BIT,
        IsGroupable BIT,
        AllowedAggregations NVARCHAR(500),
        Score INT
    );
    
    INSERT INTO @Fields (DatasetKey, FieldKey, DisplayName, DataType, SemanticType, IsFilterable, IsGroupable, AllowedAggregations, Score)
    SELECT TOP (@TopK * 3)
        fc.DatasetKey,
        fc.FieldKey,
        fc.DisplayName,
        fc.DataType,
        fc.SemanticType,
        fc.IsFilterable,
        fc.IsGroupable,
        fc.AllowedAggregations,
        CASE 
            WHEN LOWER(fc.FieldKey) = @NormalizedQuery THEN 100
            WHEN fc.SearchText LIKE '%' + @NormalizedQuery + '%' THEN 50
            ELSE 10
        END AS Score
    FROM dbo.v_Analytics_FieldCatalog fc
    WHERE fc.DatasetKey IN (SELECT DatasetKey FROM @Datasets)
       OR fc.SearchText LIKE '%' + @NormalizedQuery + '%'
    ORDER BY Score DESC, fc.FieldKey;
    
    -- Generate hints based on search
    DECLARE @Hints TABLE (Hint NVARCHAR(500));
    
    IF NOT EXISTS (SELECT 1 FROM @Datasets)
    BEGIN
        INSERT INTO @Hints (Hint) VALUES ('No datasets found matching query. Try broader search terms.');
    END
    ELSE
    BEGIN
        INSERT INTO @Hints (Hint)
        SELECT TOP 3 'Try dataset: ' + DatasetKey + ' (grain: ' + ISNULL(Grain, 'unknown') + ')'
        FROM @Datasets
        ORDER BY Score DESC;
    END
    
    -- Return JSON envelope
    DECLARE @RowCount INT = (SELECT COUNT(1) FROM @Datasets) + (SELECT COUNT(1) FROM @Fields);
    
    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS [rowCount],
                    @NormalizedQuery AS searchQuery,
                    @TopK AS topK
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            datasets = (
                SELECT DatasetKey, DisplayName, Description, Grain, Score
                FROM @Datasets
                ORDER BY Score DESC
                FOR JSON PATH
            ),
            fields = (
                SELECT DatasetKey, FieldKey, DisplayName, DataType, SemanticType, 
                       IsFilterable, IsGroupable, AllowedAggregations, Score
                FROM @Fields
                ORDER BY Score DESC
                FOR JSON PATH
            ),
            hints = (
                SELECT Hint AS [hint]
                FROM @Hints
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

-- catalog_get_dataset: Get full schema + metadata for a dataset
CREATE OR ALTER PROCEDURE dbo.ai_catalog_get_dataset
    @TenantId   NVARCHAR(50),
    @DatasetKey NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @GeneratedAtUtc DATETIME2(3) = SYSUTCDATETIME();
    
    -- Validate inputs
    IF @TenantId IS NULL OR @TenantId = ''
    BEGIN
        RAISERROR('TenantId is required.', 16, 1);
        RETURN;
    END
    
    IF @DatasetKey IS NULL OR LTRIM(RTRIM(@DatasetKey)) = ''
    BEGIN
        RAISERROR('DatasetKey is required.', 16, 1);
        RETURN;
    END
    
    -- Get dataset metadata
    DECLARE @Dataset TABLE (
        DatasetKey NVARCHAR(200),
        DisplayName NVARCHAR(500),
        Description NVARCHAR(2000),
        BaseObject NVARCHAR(500),
        Grain NVARCHAR(200),
        TimeColumn NVARCHAR(200),
        Tags NVARCHAR(1000)
    );
    
    INSERT INTO @Dataset
    SELECT TOP 1
        DatasetKey,
        DisplayName,
        Description,
        BaseObject,
        Grain,
        TimeColumn,
        Tags
    FROM dbo.v_Analytics_DatasetCatalog
    WHERE DatasetKey = @DatasetKey
      AND (TenantId = @TenantId OR TenantId IS NULL)
    ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END;
    
    IF NOT EXISTS (SELECT 1 FROM @Dataset)
    BEGIN
        RAISERROR('Dataset not found or access denied.', 16, 1);
        RETURN;
    END
    
    -- Get all enabled fields
    DECLARE @Fields TABLE (
        FieldKey NVARCHAR(200),
        PhysicalColumn NVARCHAR(200),
        DisplayName NVARCHAR(500),
        Description NVARCHAR(2000),
        DataType NVARCHAR(50),
        SemanticType NVARCHAR(100),
        AllowedAggregations NVARCHAR(500),
        IsFilterable BIT,
        IsGroupable BIT,
        IsSortable BIT,
        SecurityTag NVARCHAR(50)
    );
    
    INSERT INTO @Fields
    SELECT
        FieldKey,
        PhysicalColumn,
        DisplayName,
        Description,
        DataType,
        SemanticType,
        AllowedAggregations,
        IsFilterable,
        IsGroupable,
        IsSortable,
        SecurityTag
    FROM dbo.v_Analytics_FieldCatalog
    WHERE DatasetKey = @DatasetKey
    ORDER BY FieldKey;
    
    -- Get join relationships from EntityGraphCatalog (if exists)
    DECLARE @Joins TABLE (
        TargetDatasetKey NVARCHAR(200),
        JoinType NVARCHAR(50),
        SourceFields NVARCHAR(500),
        TargetFields NVARCHAR(500)
    );
    
    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EntityGraphCatalog')
    BEGIN
        INSERT INTO @Joins
        SELECT
            TargetDatasetKey,
            JoinType,
            SourceFields,
            TargetFields
        FROM dbo.EntityGraphCatalog
        WHERE SourceDatasetKey = @DatasetKey
          AND IsEnabled = 1;
    END
    
    -- Return JSON envelope
    DECLARE @RowCount INT = (SELECT COUNT(1) FROM @Fields);
    
    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS fieldCount
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            dataset = (
                SELECT TOP 1 DatasetKey, DisplayName, Description, BaseObject, Grain, TimeColumn, Tags
                FROM @Dataset
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            fields = (
                SELECT FieldKey, PhysicalColumn, DisplayName, Description, DataType,
                       SemanticType, AllowedAggregations, IsFilterable, IsGroupable, IsSortable, SecurityTag
                FROM @Fields
                ORDER BY FieldKey
                FOR JSON PATH
            ),
            joins = (
                SELECT TargetDatasetKey, JoinType, SourceFields, TargetFields
                FROM @Joins
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

-- analytics_validate_plan: Validate atomic plan before execution
-- PATCH 28: Complete validation with metrics/joins/security/time-window
-- PATCH 29.03: Exact token matching, error code alignment, time-window tied to TimeColumn
CREATE OR ALTER PROCEDURE dbo.ai_analytics_validate_plan
    @TenantId   NVARCHAR(50),
    @PlanJson   NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @GeneratedAtUtc DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @IsValid BIT = 1;
    DECLARE @ErrorCode NVARCHAR(100) = NULL;
    DECLARE @ErrorMessage NVARCHAR(2000) = NULL;
    
    -- Validation limits (configurable via appsettings, hardcoded as defaults here)
    DECLARE @MaxRows INT = 200;
    DECLARE @MaxGroupBy INT = 4;
    DECLARE @MaxMetrics INT = 3;
    DECLARE @MaxJoins INT = 1;
    DECLARE @MaxTimeWindowDays INT = 366;
    
    -- PATCH 29.03: Build exact-match allowlist using table variable
    DECLARE @AllowedOps TABLE (Op NVARCHAR(50) PRIMARY KEY);
    INSERT INTO @AllowedOps (Op) VALUES 
        ('count'), ('countdistinct'), ('sum'), ('avg'), ('min'), ('max');
    
    -- Validate inputs
    IF @TenantId IS NULL OR @TenantId = ''
    BEGIN
        RAISERROR('TenantId is required.', 16, 1);
        RETURN;
    END
    
    IF ISJSON(@PlanJson) <> 1
    BEGIN
        SET @IsValid = 0;
        SET @ErrorCode = 'INVALID_JSON';
        SET @ErrorMessage = 'PlanJson must be valid JSON.';
    END
    
    -- Check datasetKey exists
    DECLARE @DatasetKey NVARCHAR(200) = JSON_VALUE(@PlanJson, '$.datasetKey');
    IF @IsValid = 1 AND (@DatasetKey IS NULL OR @DatasetKey = '')
    BEGIN
        SET @IsValid = 0;
        SET @ErrorCode = 'MISSING_DATASET';
        SET @ErrorMessage = 'datasetKey is required.';
    END
    
    -- Get dataset metadata (needed for TimeColumn check)
    DECLARE @TimeColumn NVARCHAR(200) = NULL;
    
    -- Validate dataset exists and accessible
    IF @IsValid = 1
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM dbo.DatasetCatalog 
            WHERE DatasetKey = @DatasetKey 
              AND IsEnabled = 1
              AND (TenantId = @TenantId OR TenantId IS NULL)
        )
        BEGIN
            SET @IsValid = 0;
            SET @ErrorCode = 'DATASET_NOT_FOUND';
            SET @ErrorMessage = 'Dataset not found or disabled: ' + @DatasetKey;
        END
        ELSE
        BEGIN
            -- Get TimeColumn for time-window validation
            SELECT @TimeColumn = TimeColumn
            FROM dbo.DatasetCatalog
            WHERE DatasetKey = @DatasetKey 
              AND IsEnabled = 1
              AND (TenantId = @TenantId OR TenantId IS NULL);
        END
    END
    
    -- Check limit
    DECLARE @Limit INT = TRY_CONVERT(INT, JSON_VALUE(@PlanJson, '$.limit'));
    IF @IsValid = 1 AND @Limit IS NOT NULL AND @Limit > @MaxRows
    BEGIN
        SET @IsValid = 0;
        SET @ErrorCode = 'LIMIT_EXCEEDED';
        SET @ErrorMessage = 'Limit (' + CAST(@Limit AS NVARCHAR(10)) + ') exceeds maximum allowed (' + CAST(@MaxRows AS NVARCHAR(10)) + ').';
    END
    
    -- Check groupBy count
    DECLARE @GroupByCount INT = (SELECT COUNT(1) FROM OPENJSON(@PlanJson, '$.groupBy'));
    IF @IsValid = 1 AND @GroupByCount > @MaxGroupBy
    BEGIN
        SET @IsValid = 0;
        SET @ErrorCode = 'GROUPBY_EXCEEDED';
        SET @ErrorMessage = 'GroupBy count (' + CAST(@GroupByCount AS NVARCHAR(10)) + ') exceeds maximum allowed (' + CAST(@MaxGroupBy AS NVARCHAR(10)) + ').';
    END
    
    -- Check metrics count
    DECLARE @MetricsCount INT = (SELECT COUNT(1) FROM OPENJSON(@PlanJson, '$.metrics'));
    IF @IsValid = 1 AND @MetricsCount > @MaxMetrics
    BEGIN
        SET @IsValid = 0;
        SET @ErrorCode = 'METRICS_EXCEEDED';
        SET @ErrorMessage = 'Metrics count (' + CAST(@MetricsCount AS NVARCHAR(10)) + ') exceeds maximum allowed (' + CAST(@MaxMetrics AS NVARCHAR(10)) + ').';
    END
    
    -- Check joins count
    DECLARE @JoinsCount INT = (SELECT COUNT(1) FROM OPENJSON(@PlanJson, '$.joins'));
    IF @IsValid = 1 AND @JoinsCount > @MaxJoins
    BEGIN
        SET @IsValid = 0;
        SET @ErrorCode = 'JOINS_EXCEEDED';
        SET @ErrorMessage = 'Joins count (' + CAST(@JoinsCount AS NVARCHAR(10)) + ') exceeds maximum allowed (' + CAST(@MaxJoins AS NVARCHAR(10)) + ').';
    END
    
    -- Validate field existence for select/where/groupBy
    DECLARE @InvalidFields TABLE (FieldKey NVARCHAR(200), Context NVARCHAR(50));
    
    IF @IsValid = 1
    BEGIN
        -- Check select fields
        INSERT INTO @InvalidFields (FieldKey, Context)
        SELECT value, 'select'
        FROM OPENJSON(@PlanJson, '$.select')
        WHERE value NOT IN (
            SELECT FieldKey FROM dbo.FieldCatalog WHERE DatasetKey = @DatasetKey AND IsEnabled = 1
        );
        
        -- Check where fields
        INSERT INTO @InvalidFields (FieldKey, Context)
        SELECT JSON_VALUE(value, '$.field'), 'where'
        FROM OPENJSON(@PlanJson, '$.where')
        WHERE JSON_VALUE(value, '$.field') IS NOT NULL
          AND JSON_VALUE(value, '$.field') NOT IN (
            SELECT FieldKey FROM dbo.FieldCatalog WHERE DatasetKey = @DatasetKey AND IsEnabled = 1
        );
        
        -- Check groupBy fields
        INSERT INTO @InvalidFields (FieldKey, Context)
        SELECT value, 'groupBy'
        FROM OPENJSON(@PlanJson, '$.groupBy')
        WHERE value NOT IN (
            SELECT FieldKey FROM dbo.FieldCatalog WHERE DatasetKey = @DatasetKey AND IsEnabled = 1
        );
        
        IF EXISTS (SELECT 1 FROM @InvalidFields)
        BEGIN
            SET @IsValid = 0;
            SET @ErrorCode = 'UNKNOWN_FIELD';
            SELECT @ErrorMessage = 'Unknown fields: ' + STRING_AGG(FieldKey + ' (' + Context + ')', ', ')
            FROM @InvalidFields;
        END
    END
    
    -- PATCH 29.03 T01/T02: Validate metrics with EXACT token matching and aligned error codes
    DECLARE @InvalidMetrics TABLE (FieldKey NVARCHAR(200), Op NVARCHAR(50), Reason NVARCHAR(200), ErrorType NVARCHAR(50));
    
    IF @IsValid = 1
    BEGIN
        -- Check each metric for field existence and op validity
        INSERT INTO @InvalidMetrics (FieldKey, Op, Reason, ErrorType)
        SELECT 
            JSON_VALUE(value, '$.field') AS FieldKey,
            LOWER(LTRIM(RTRIM(JSON_VALUE(value, '$.op')))) AS Op,
            CASE
                -- Check if field exists (except * which is allowed for count)
                WHEN JSON_VALUE(value, '$.field') IS NOT NULL 
                     AND JSON_VALUE(value, '$.field') <> '*'
                     AND JSON_VALUE(value, '$.field') NOT IN (
                         SELECT FieldKey FROM dbo.FieldCatalog WHERE DatasetKey = @DatasetKey AND IsEnabled = 1
                     ) THEN 'Field not found'
                -- PATCH 29.03: Exact match check using table join
                WHEN LOWER(LTRIM(RTRIM(JSON_VALUE(value, '$.op')))) NOT IN (SELECT Op FROM @AllowedOps)
                THEN 'Invalid operation'
                -- Check field-specific allowed aggregations  
                WHEN fc.AllowedAggregations IS NOT NULL 
                     AND EXISTS (
                         SELECT 1 FROM STRING_SPLIT(fc.AllowedAggregations, ',') allowed
                         WHERE LOWER(LTRIM(RTRIM(allowed.value))) = LOWER(LTRIM(RTRIM(JSON_VALUE(m.value, '$.op'))))
                     ) = 0
                THEN 'Operation not allowed for this field'
                ELSE NULL
            END AS Reason,
            CASE
                WHEN JSON_VALUE(value, '$.field') IS NOT NULL 
                     AND JSON_VALUE(value, '$.field') <> '*'
                     AND JSON_VALUE(value, '$.field') NOT IN (
                         SELECT FieldKey FROM dbo.FieldCatalog WHERE DatasetKey = @DatasetKey AND IsEnabled = 1
                     ) THEN 'UNKNOWN_FIELD'
                ELSE 'INVALID_OP'
            END AS ErrorType
        FROM OPENJSON(@PlanJson, '$.metrics') m
        LEFT JOIN dbo.FieldCatalog fc 
            ON fc.DatasetKey = @DatasetKey 
            AND fc.FieldKey = JSON_VALUE(m.value, '$.field')
            AND fc.IsEnabled = 1;
        
        -- Filter out valid metrics
        DELETE FROM @InvalidMetrics WHERE Reason IS NULL;
        
        IF EXISTS (SELECT 1 FROM @InvalidMetrics)
        BEGIN
            SET @IsValid = 0;
            -- PATCH 29.03 T02: Use INVALID_OP or UNKNOWN_FIELD based on error type
            SELECT TOP 1 @ErrorCode = ErrorType FROM @InvalidMetrics;
            SELECT @ErrorMessage = 'Invalid metrics: ' + STRING_AGG(FieldKey + '.' + Op + ' (' + Reason + ')', '; ')
            FROM @InvalidMetrics;
        END
    END
    
    -- PATCH 29.03 T04: Validate orderBy against groupBy fields AND metric aliases
    DECLARE @GroupByFields TABLE (FieldKey NVARCHAR(200));
    DECLARE @MetricAliases TABLE (Alias NVARCHAR(200));
    
    IF @IsValid = 1
    BEGIN
        -- Collect valid groupBy fields
        INSERT INTO @GroupByFields (FieldKey)
        SELECT value FROM OPENJSON(@PlanJson, '$.groupBy');
        
        -- Collect metric aliases
        INSERT INTO @MetricAliases (Alias)
        SELECT COALESCE(
            JSON_VALUE(value, '$.alias'),
            JSON_VALUE(value, '$.as'),
            JSON_VALUE(value, '$.op') + '_' + COALESCE(JSON_VALUE(value, '$.field'), 'total')
        )
        FROM OPENJSON(@PlanJson, '$.metrics');
        
        -- Also add raw field catalog fields
        INSERT INTO @GroupByFields (FieldKey)
        SELECT FieldKey FROM dbo.FieldCatalog WHERE DatasetKey = @DatasetKey AND IsEnabled = 1
        EXCEPT SELECT FieldKey FROM @GroupByFields;
        
        -- Check orderBy fields
        DECLARE @InvalidOrderBy TABLE (FieldKey NVARCHAR(200));
        
        INSERT INTO @InvalidOrderBy (FieldKey)
        SELECT JSON_VALUE(value, '$.field')
        FROM OPENJSON(@PlanJson, '$.orderBy')
        WHERE JSON_VALUE(value, '$.field') IS NOT NULL
          AND JSON_VALUE(value, '$.field') NOT IN (SELECT FieldKey FROM @GroupByFields)
          AND JSON_VALUE(value, '$.field') NOT IN (SELECT Alias FROM @MetricAliases);
        
        IF EXISTS (SELECT 1 FROM @InvalidOrderBy)
        BEGIN
            SET @IsValid = 0;
            SET @ErrorCode = 'UNKNOWN_FIELD';
            SELECT @ErrorMessage = 'Unknown orderBy field(s): ' + STRING_AGG(FieldKey, ', ') + 
                '. Must be a groupBy field or metric alias.'
            FROM @InvalidOrderBy;
        END
    END
    
    -- PATCH 29.07: Validate security tags with role-aware access
    -- Roles can be passed via plan JSON as "_roles" array
    DECLARE @UserRoles TABLE (Role NVARCHAR(100));
    DECLARE @AllowedTags TABLE (Tag NVARCHAR(50));
    
    -- Parse roles from plan (e.g., "$._roles": ["analytics.read", "analytics.pii"])
    INSERT INTO @UserRoles (Role)
    SELECT value FROM OPENJSON(@PlanJson, '$._roles');
    
    -- Build allowed tags based on roles
    INSERT INTO @AllowedTags (Tag) VALUES ('PUBLIC'), ('INTERNAL'); -- All authenticated users
    IF EXISTS (SELECT 1 FROM @UserRoles WHERE Role = 'analytics.pii')
        INSERT INTO @AllowedTags (Tag) VALUES ('PII');
    IF EXISTS (SELECT 1 FROM @UserRoles WHERE Role = 'analytics.sensitive')
        INSERT INTO @AllowedTags (Tag) VALUES ('SENSITIVE');
    IF EXISTS (SELECT 1 FROM @UserRoles WHERE Role = 'analytics.admin')
    BEGIN
        INSERT INTO @AllowedTags (Tag) VALUES ('PII'), ('SENSITIVE'), ('RESTRICTED');
    END
    
    DECLARE @RestrictedFields TABLE (FieldKey NVARCHAR(200), SecurityTag NVARCHAR(50));
    
    IF @IsValid = 1
    BEGIN
        -- Find any referenced fields with security tags NOT in allowed list
        INSERT INTO @RestrictedFields (FieldKey, SecurityTag)
        SELECT fc.FieldKey, fc.SecurityTag
        FROM dbo.FieldCatalog fc
        WHERE fc.DatasetKey = @DatasetKey 
          AND fc.IsEnabled = 1
          AND fc.SecurityTag IS NOT NULL
          AND fc.SecurityTag NOT IN (SELECT Tag FROM @AllowedTags)
          AND (
              fc.FieldKey IN (SELECT value FROM OPENJSON(@PlanJson, '$.select'))
              OR fc.FieldKey IN (SELECT JSON_VALUE(value, '$.field') FROM OPENJSON(@PlanJson, '$.where'))
              OR fc.FieldKey IN (SELECT value FROM OPENJSON(@PlanJson, '$.groupBy'))
              OR fc.FieldKey IN (SELECT JSON_VALUE(value, '$.field') FROM OPENJSON(@PlanJson, '$.metrics'))
          );
        
        IF EXISTS (SELECT 1 FROM @RestrictedFields)
        BEGIN
            SET @IsValid = 0;
            SET @ErrorCode = 'SECURITY_VIOLATION';
            SELECT @ErrorMessage = 'Access denied to restricted fields: ' + STRING_AGG(FieldKey + ' (' + SecurityTag + ')', ', ')
            FROM @RestrictedFields;
        END
    END
    
    -- PATCH 29.03 T03: Time-window validation ONLY for dataset's TimeColumn or date semantic type
    IF @IsValid = 1
    BEGIN
        -- Get date/time fields for this dataset
        DECLARE @TimeFields TABLE (FieldKey NVARCHAR(200));
        
        INSERT INTO @TimeFields (FieldKey)
        SELECT FieldKey FROM dbo.FieldCatalog 
        WHERE DatasetKey = @DatasetKey 
          AND IsEnabled = 1
          AND (
              FieldKey = @TimeColumn
              OR SemanticType IN ('date', 'datetime', 'timestamp')
          );
        
        -- Only validate time window if filtering on a time field
        DECLARE @StartDate DATETIME2, @EndDate DATETIME2;
        DECLARE @TimeFieldFiltered BIT = 0;
        
        -- Check if any where clause targets a time field with date operators
        SELECT TOP 1 
            @TimeFieldFiltered = 1,
            @StartDate = CASE 
                WHEN JSON_VALUE(value, '$.op') IN ('>=', '>', 'gte', 'gt', 'between') 
                THEN TRY_CONVERT(DATETIME2, JSON_VALUE(value, '$.value'))
                ELSE @StartDate
            END,
            @EndDate = CASE 
                WHEN JSON_VALUE(value, '$.op') IN ('<=', '<', 'lte', 'lt') 
                THEN TRY_CONVERT(DATETIME2, JSON_VALUE(value, '$.value'))
                -- Handle between operator (value2 for end date)
                WHEN JSON_VALUE(value, '$.op') = 'between'
                THEN TRY_CONVERT(DATETIME2, JSON_VALUE(value, '$.value2'))
                ELSE @EndDate
            END
        FROM OPENJSON(@PlanJson, '$.where')
        WHERE JSON_VALUE(value, '$.field') IN (SELECT FieldKey FROM @TimeFields);
        
        -- Also try to parse gte/lte style with separate entries
        SELECT @StartDate = COALESCE(@StartDate, TRY_CONVERT(DATETIME2, JSON_VALUE(value, '$.value')))
        FROM OPENJSON(@PlanJson, '$.where')
        WHERE JSON_VALUE(value, '$.field') IN (SELECT FieldKey FROM @TimeFields)
          AND JSON_VALUE(value, '$.op') IN ('>=', '>', 'gte', 'gt')
          AND @StartDate IS NULL;
        
        SELECT @EndDate = COALESCE(@EndDate, TRY_CONVERT(DATETIME2, JSON_VALUE(value, '$.value')))
        FROM OPENJSON(@PlanJson, '$.where')
        WHERE JSON_VALUE(value, '$.field') IN (SELECT FieldKey FROM @TimeFields)
          AND JSON_VALUE(value, '$.op') IN ('<=', '<', 'lte', 'lt')
          AND @EndDate IS NULL;
        
        IF @StartDate IS NOT NULL AND @EndDate IS NOT NULL
        BEGIN
            DECLARE @DaysDiff INT = DATEDIFF(DAY, @StartDate, @EndDate);
            IF @DaysDiff > @MaxTimeWindowDays
            BEGIN
                SET @IsValid = 0;
                SET @ErrorCode = 'TIME_WINDOW_EXCEEDED';
                SET @ErrorMessage = 'Time window (' + CAST(@DaysDiff AS NVARCHAR(10)) + ' days) exceeds maximum allowed (' + CAST(@MaxTimeWindowDays AS NVARCHAR(10)) + ' days).';
            END
        END
    END
    
    -- Generate suggestions for errors
    DECLARE @Suggestions TABLE (Suggestion NVARCHAR(500));
    
    IF @IsValid = 0
    BEGIN
        IF @ErrorCode = 'UNKNOWN_FIELD'
        BEGIN
            -- Suggest similar fields (fuzzy match)
            INSERT INTO @Suggestions (Suggestion)
            SELECT DISTINCT TOP 5 'Did you mean: ' + fc.FieldKey
            FROM @InvalidFields inv
            CROSS APPLY (
                SELECT FieldKey
                FROM dbo.FieldCatalog
                WHERE DatasetKey = @DatasetKey AND IsEnabled = 1
                  AND (
                      SOUNDEX(FieldKey) = SOUNDEX(inv.FieldKey)
                      OR FieldKey LIKE '%' + LEFT(inv.FieldKey, 3) + '%'
                  )
            ) fc;
        END
        
        IF @ErrorCode = 'LIMIT_EXCEEDED'
        BEGIN
            INSERT INTO @Suggestions (Suggestion)
            VALUES ('Use limit <= ' + CAST(@MaxRows AS NVARCHAR(10)));
        END
        
        -- PATCH 29.03: Standardize on INVALID_OP
        IF @ErrorCode = 'INVALID_OP'
        BEGIN
            INSERT INTO @Suggestions (Suggestion)
            VALUES ('Allowed metric operations: count, countDistinct, sum, avg, min, max');
        END
        
        IF @ErrorCode = 'SECURITY_VIOLATION'
        BEGIN
            INSERT INTO @Suggestions (Suggestion)
            VALUES ('Remove restricted fields or request elevated permissions.');
        END
        
        IF @ErrorCode = 'TIME_WINDOW_EXCEEDED'
        BEGIN
            INSERT INTO @Suggestions (Suggestion)
            VALUES ('Reduce time window to <= ' + CAST(@MaxTimeWindowDays AS NVARCHAR(10)) + ' days.');
        END
    END
    
    -- Return validation result
    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @DatasetKey AS datasetKey
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            validation = (
                SELECT
                    @IsValid AS isValid,
                    @ErrorCode AS errorCode,
                    @ErrorMessage AS errorMessage,
                    CASE WHEN @IsValid = 0 AND @ErrorCode NOT IN ('SECURITY_VIOLATION') THEN 1 ELSE 0 END AS retryable
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            suggestions = (
                SELECT Suggestion AS [suggestion]
                FROM @Suggestions
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO
