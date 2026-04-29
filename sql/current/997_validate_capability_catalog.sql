SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT N'Validating SQL-backed capability catalog.';

DECLARE @Failures TABLE
(
    Failure nvarchar(4000) NOT NULL
);

IF OBJECT_ID(N'ai.Capability', N'U') IS NULL
    INSERT INTO @Failures VALUES (N'Missing table ai.Capability.');
IF OBJECT_ID(N'ai.CapabilityText', N'U') IS NULL
    INSERT INTO @Failures VALUES (N'Missing table ai.CapabilityText.');
IF OBJECT_ID(N'ai.CapabilityArgument', N'U') IS NULL
    INSERT INTO @Failures VALUES (N'Missing table ai.CapabilityArgument.');
IF OBJECT_ID(N'ai.CapabilityArgumentText', N'U') IS NULL
    INSERT INTO @Failures VALUES (N'Missing table ai.CapabilityArgumentText.');
IF OBJECT_ID(N'ai.CapabilityResultSchema', N'U') IS NULL
    INSERT INTO @Failures VALUES (N'Missing table ai.CapabilityResultSchema.');
IF OBJECT_ID(N'ai.CapabilityAnswerPolicy', N'U') IS NULL
    INSERT INTO @Failures VALUES (N'Missing table ai.CapabilityAnswerPolicy.');
IF OBJECT_ID(N'ai.CapabilitySensitivityPolicy', N'U') IS NULL
    INSERT INTO @Failures VALUES (N'Missing table ai.CapabilitySensitivityPolicy.');
IF OBJECT_ID(N'ai.CapabilityExample', N'U') IS NULL
    INSERT INTO @Failures VALUES (N'Missing table ai.CapabilityExample.');

IF NOT EXISTS (SELECT 1 FROM @Failures)
BEGIN
    IF (SELECT COUNT(1) FROM ai.Capability WHERE IsEnabled = 1 AND IsActive = 1 AND Domain = N'model') <> 6
        INSERT INTO @Failures VALUES (N'Expected exactly 6 enabled model capabilities.');

    IF EXISTS (SELECT 1 FROM ai.Capability WHERE IsEnabled = 1 AND IsActive = 1 AND Domain <> N'model')
        INSERT INTO @Failures VALUES (N'Enabled non-model capabilities are not allowed in the active runtime catalog.');

    IF EXISTS (
        SELECT CapabilityKey
        FROM ai.Capability
        WHERE IsEnabled = 1 AND IsActive = 1
        GROUP BY CapabilityKey
        HAVING COUNT(1) > 1)
        INSERT INTO @Failures VALUES (N'Duplicate enabled capability keys exist.');

    IF EXISTS (
        SELECT FunctionName
        FROM ai.Capability
        WHERE IsEnabled = 1 AND IsActive = 1
        GROUP BY FunctionName
        HAVING COUNT(1) > 1)
        INSERT INTO @Failures VALUES (N'Duplicate enabled function names exist.');

    IF EXISTS (
        SELECT 1
        FROM ai.Capability
        WHERE IsEnabled = 1
          AND IsActive = 1
          AND NULLIF(LTRIM(RTRIM(FunctionName)), N'') IS NULL)
        INSERT INTO @Failures VALUES (N'All enabled capabilities must have function names.');

    IF EXISTS (
        SELECT 1
        FROM ai.Capability c
        WHERE c.IsEnabled = 1
          AND c.IsActive = 1
          AND OBJECT_ID(N'dbo.' + COALESCE(c.StoredProcedureName, c.StoredProcedure), N'P') IS NULL)
        INSERT INTO @Failures VALUES (N'One or more enabled capabilities reference missing stored procedures.');

    IF EXISTS (
        SELECT 1
        FROM ai.Capability c
        WHERE c.IsEnabled = 1
          AND c.IsActive = 1
          AND c.CapabilityKey IN (
              N'model.overview.by-code',
              N'model.pieces.by-code',
              N'model.materials.by-code',
              N'model.packaging.by-code')
          AND NOT EXISTS (
              SELECT 1
              FROM ai.CapabilityArgument a
              WHERE a.CapabilityKey = c.CapabilityKey
                AND a.ArgumentName = N'modelCode'
                AND a.ModelFacingName = N'modelCode'
                AND a.IsRequired = 1))
        INSERT INTO @Failures VALUES (N'Missing required modelCode argument contract.');

    IF EXISTS (
        SELECT 1
        FROM ai.CapabilityArgument a
        WHERE a.CapabilityKey = N'model.compare'
          AND NOT (a.ArgumentName = N'modelCodes' AND a.ModelFacingName = N'modelCodes' AND a.IsRequired = 1))
        INSERT INTO @Failures VALUES (N'Missing required modelCodes argument contract for model.compare.');

    IF EXISTS (
        SELECT 1
        FROM ai.CapabilityArgument
        WHERE LOWER(ModelFacingName) IN (N'modelid', N'model_id'))
        INSERT INTO @Failures VALUES (N'Model-facing modelId/model_id arguments are forbidden.');

    IF EXISTS (
        SELECT 1
        FROM ai.Capability c
        WHERE c.IsEnabled = 1
          AND c.IsActive = 1
          AND NOT EXISTS (SELECT 1 FROM ai.CapabilityResultSchema rs WHERE rs.CapabilityKey = c.CapabilityKey AND ISJSON(rs.SchemaJson) = 1))
        INSERT INTO @Failures VALUES (N'Every enabled capability must have a valid result schema.');

    IF EXISTS (
        SELECT 1
        FROM ai.Capability c
        WHERE c.IsEnabled = 1
          AND c.IsActive = 1
          AND NOT EXISTS (SELECT 1 FROM ai.CapabilityAnswerPolicy ap WHERE ap.CapabilityKey = c.CapabilityKey AND ISJSON(ap.PolicyJson) = 1))
        INSERT INTO @Failures VALUES (N'Every enabled capability must have a valid answer policy.');

    IF EXISTS (
        SELECT 1 FROM ai.Capability WHERE ArgumentContract IS NOT NULL AND ISJSON(ArgumentContract) <> 1
        UNION ALL
        SELECT 1 FROM ai.Capability WHERE ResultSchema IS NOT NULL AND ISJSON(ResultSchema) <> 1
        UNION ALL
        SELECT 1 FROM ai.Capability WHERE AnswerPolicy IS NOT NULL AND ISJSON(AnswerPolicy) <> 1
        UNION ALL
        SELECT 1 FROM ai.Capability WHERE SensitivityPolicy IS NOT NULL AND ISJSON(SensitivityPolicy) <> 1
        UNION ALL
        SELECT 1 FROM ai.CapabilityText WHERE AliasesJson IS NOT NULL AND ISJSON(AliasesJson) <> 1
        UNION ALL
        SELECT 1 FROM ai.CapabilityArgument WHERE EnumJson IS NOT NULL AND ISJSON(EnumJson) <> 1
        UNION ALL
        SELECT 1 FROM ai.CapabilityArgumentText WHERE AliasesJson IS NOT NULL AND ISJSON(AliasesJson) <> 1
        UNION ALL
        SELECT 1 FROM ai.CapabilityArgumentText WHERE ExamplesJson IS NOT NULL AND ISJSON(ExamplesJson) <> 1
        UNION ALL
        SELECT 1 FROM ai.CapabilityExample WHERE ArgumentsJson IS NOT NULL AND ISJSON(ArgumentsJson) <> 1)
        INSERT INTO @Failures VALUES (N'One or more capability catalog JSON values are invalid.');
END;

IF EXISTS (SELECT 1 FROM @Failures)
BEGIN
    SELECT Failure FROM @Failures ORDER BY Failure;
    DECLARE @FailureCount int = (SELECT COUNT(1) FROM @Failures);
    RAISERROR('Capability catalog validation failed with %d issue(s). See result set for details.', 16, 1, @FailureCount);
    RETURN;
END;

SELECT
    DB_NAME() AS databaseName,
    N'passed' AS capabilityCatalogValidation,
    (SELECT COUNT(1) FROM ai.Capability WHERE IsEnabled = 1 AND IsActive = 1) AS enabledCapabilityCount;
GO
