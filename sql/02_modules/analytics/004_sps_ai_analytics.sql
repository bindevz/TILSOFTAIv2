/*******************************************************************************
* TILSOFTAI Analytics Module - Model-Callable Stored Procedures (ai_*)
* Purpose: Schema RAG tools for LLM to discover datasets/fields
* 
* CRITICAL: All ai_* SPs MUST:
*   1. Accept @TenantId and enforce tenant scope
*   2. Return JSON envelope (meta + columns + rows)
*   3. Apply pagination/limit guards
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
        ISNULL(DisplayName, DatasetKey),
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
        ISNULL(fc.DisplayName, fc.FieldKey),
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
        ISNULL(DisplayName, DatasetKey),
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
        ISNULL(DisplayName, FieldKey),
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
    
    -- Get example values if available
    DECLARE @Examples TABLE (
        FieldKey NVARCHAR(200),
        ExampleValues NVARCHAR(1000)
    );
    
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
            ),
            examples = (
                SELECT FieldKey, ExampleValues
                FROM @Examples
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

-- analytics_validate_plan: Validate atomic plan before execution
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
    
    -- Validation limits
    DECLARE @MaxRows INT = 200;
    DECLARE @MaxGroupBy INT = 4;
    DECLARE @MaxMetrics INT = 3;
    DECLARE @MaxJoins INT = 1;
    
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
    
    -- Validate dataset exists and accessible
    IF @IsValid = 1 AND NOT EXISTS (
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
    
    -- Check limit
    DECLARE @Limit INT = TRY_CONVERT(INT, JSON_VALUE(@PlanJson, '$.limit'));
    IF @IsValid = 1 AND @Limit IS NOT NULL AND @Limit > @MaxRows
    BEGIN
        SET @IsValid = 0;
        SET @ErrorCode = 'LIMIT_EXCEEDED';
        SET @ErrorMessage = 'Limit exceeds maximum allowed (' + CAST(@MaxRows AS NVARCHAR(10)) + ').';
    END
    
    -- Check groupBy count
    DECLARE @GroupByCount INT = (SELECT COUNT(1) FROM OPENJSON(@PlanJson, '$.groupBy'));
    IF @IsValid = 1 AND @GroupByCount > @MaxGroupBy
    BEGIN
        SET @IsValid = 0;
        SET @ErrorCode = 'GROUPBY_EXCEEDED';
        SET @ErrorMessage = 'GroupBy count exceeds maximum allowed (' + CAST(@MaxGroupBy AS NVARCHAR(10)) + ').';
    END
    
    -- Check metrics count
    DECLARE @MetricsCount INT = (SELECT COUNT(1) FROM OPENJSON(@PlanJson, '$.metrics'));
    IF @IsValid = 1 AND @MetricsCount > @MaxMetrics
    BEGIN
        SET @IsValid = 0;
        SET @ErrorCode = 'METRICS_EXCEEDED';
        SET @ErrorMessage = 'Metrics count exceeds maximum allowed (' + CAST(@MaxMetrics AS NVARCHAR(10)) + ').';
    END
    
    -- Validate field existence
    DECLARE @InvalidFields TABLE (FieldKey NVARCHAR(200));
    
    IF @IsValid = 1
    BEGIN
        -- Check select fields
        INSERT INTO @InvalidFields (FieldKey)
        SELECT value
        FROM OPENJSON(@PlanJson, '$.select')
        WHERE value NOT IN (
            SELECT FieldKey FROM dbo.FieldCatalog WHERE DatasetKey = @DatasetKey AND IsEnabled = 1
        );
        
        -- Check where fields
        INSERT INTO @InvalidFields (FieldKey)
        SELECT JSON_VALUE(value, '$.field')
        FROM OPENJSON(@PlanJson, '$.where')
        WHERE JSON_VALUE(value, '$.field') NOT IN (
            SELECT FieldKey FROM dbo.FieldCatalog WHERE DatasetKey = @DatasetKey AND IsEnabled = 1
        );
        
        -- Check groupBy fields
        INSERT INTO @InvalidFields (FieldKey)
        SELECT value
        FROM OPENJSON(@PlanJson, '$.groupBy')
        WHERE value NOT IN (
            SELECT FieldKey FROM dbo.FieldCatalog WHERE DatasetKey = @DatasetKey AND IsEnabled = 1
        );
        
        IF EXISTS (SELECT 1 FROM @InvalidFields)
        BEGIN
            SET @IsValid = 0;
            SET @ErrorCode = 'UNKNOWN_FIELD';
            SELECT @ErrorMessage = 'Unknown fields: ' + STRING_AGG(FieldKey, ', ')
            FROM @InvalidFields;
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
            SELECT TOP 5 'Did you mean: ' + fc.FieldKey
            FROM @InvalidFields inv
            CROSS APPLY (
                SELECT FieldKey
                FROM dbo.FieldCatalog
                WHERE DatasetKey = @DatasetKey AND IsEnabled = 1
                  AND SOUNDEX(FieldKey) = SOUNDEX(inv.FieldKey)
            ) fc;
        END
        
        IF @ErrorCode = 'LIMIT_EXCEEDED'
        BEGIN
            INSERT INTO @Suggestions (Suggestion)
            VALUES ('Use limit <= ' + CAST(@MaxRows AS NVARCHAR(10)));
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
                    CASE WHEN @IsValid = 0 THEN 1 ELSE 0 END AS retryable
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
