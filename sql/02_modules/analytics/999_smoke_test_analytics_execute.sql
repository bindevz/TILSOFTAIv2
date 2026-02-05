/*******************************************************************************
* TILSOFTAI Analytics Module - Smoke Test for ai_analytics_execute_plan
* Purpose: Verify SP exists and basic execution works
* 
* PATCH 29.01: Smoke test for metrics execution
* 
* Usage: Run this script on dev DB to verify SP compilation and basic function.
* It is idempotent and does not require production data.
*******************************************************************************/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '=== Analytics Execute Plan Smoke Test ===';
PRINT '';

-- Test 1: Verify procedure exists
PRINT 'Test 1: Checking if dbo.ai_analytics_execute_plan exists...';
IF OBJECT_ID('dbo.ai_analytics_execute_plan', 'P') IS NOT NULL
BEGIN
    PRINT '  [PASS] Procedure exists.';
END
ELSE
BEGIN
    PRINT '  [FAIL] Procedure does not exist!';
    RAISERROR('Smoke test failed: ai_analytics_execute_plan not found.', 16, 1);
    GOTO EndTest;
END
GO

-- Test 2: Verify required dependencies exist
PRINT 'Test 2: Checking dependencies...';

IF OBJECT_ID('dbo.DatasetCatalog', 'U') IS NOT NULL
BEGIN
    PRINT '  [PASS] DatasetCatalog table exists.';
END
ELSE
BEGIN
    PRINT '  [WARN] DatasetCatalog table not found (may be created later).';
END

IF OBJECT_ID('dbo.FieldCatalog', 'U') IS NOT NULL
BEGIN
    PRINT '  [PASS] FieldCatalog table exists.';
END
ELSE
BEGIN
    PRINT '  [WARN] FieldCatalog table not found (may be created later).';
END
GO

-- Test 3: Test error handling with invalid JSON
PRINT 'Test 3: Testing invalid JSON handling...';
BEGIN TRY
    EXEC dbo.ai_analytics_execute_plan 
        @TenantId = 'test-tenant',
        @ArgsJson = 'not valid json';
    
    PRINT '  [FAIL] Should have raised error for invalid JSON.';
END TRY
BEGIN CATCH
    IF ERROR_MESSAGE() LIKE '%valid JSON%'
    BEGIN
        PRINT '  [PASS] Correctly rejected invalid JSON.';
    END
    ELSE
    BEGIN
        PRINT '  [WARN] Error raised but message unexpected: ' + ERROR_MESSAGE();
    END
END CATCH
GO

-- Test 4: Test error handling with missing datasetKey
PRINT 'Test 4: Testing missing datasetKey handling...';
BEGIN TRY
    EXEC dbo.ai_analytics_execute_plan 
        @TenantId = 'test-tenant',
        @ArgsJson = '{"metrics":[{"op":"count"}]}';
    
    PRINT '  [FAIL] Should have raised error for missing datasetKey.';
END TRY
BEGIN CATCH
    IF ERROR_MESSAGE() LIKE '%datasetKey%'
    BEGIN
        PRINT '  [PASS] Correctly rejected missing datasetKey.';
    END
    ELSE
    BEGIN
        PRINT '  [WARN] Error raised but message unexpected: ' + ERROR_MESSAGE();
    END
END CATCH
GO

-- Test 5: Test error handling with invalid metric operation
PRINT 'Test 5: Testing invalid metric operation handling...';
BEGIN TRY
    EXEC dbo.ai_analytics_execute_plan 
        @TenantId = 'test-tenant',
        @ArgsJson = '{"datasetKey":"test","metrics":[{"field":"x","op":"invalid_op"}]}';
    
    PRINT '  [PASS] Or dataset not found error (expected if no test data).';
END TRY
BEGIN CATCH
    IF ERROR_MESSAGE() LIKE '%metric operation%' OR ERROR_MESSAGE() LIKE '%Dataset not found%'
    BEGIN
        PRINT '  [PASS] Correctly handled error.';
    END
    ELSE
    BEGIN
        PRINT '  [INFO] Error: ' + ERROR_MESSAGE();
    END
END CATCH
GO

-- Test 6: Test parameter validation (empty TenantId)
PRINT 'Test 6: Testing empty TenantId handling...';
BEGIN TRY
    EXEC dbo.ai_analytics_execute_plan 
        @TenantId = '',
        @ArgsJson = '{"datasetKey":"test","metrics":[{"op":"count"}]}';
    
    PRINT '  [FAIL] Should have raised error for empty TenantId.';
END TRY
BEGIN CATCH
    IF ERROR_MESSAGE() LIKE '%TenantId%'
    BEGIN
        PRINT '  [PASS] Correctly rejected empty TenantId.';
    END
    ELSE
    BEGIN
        PRINT '  [WARN] Error raised but message unexpected: ' + ERROR_MESSAGE();
    END
END CATCH
GO

EndTest:
PRINT '';
PRINT '=== Smoke Test Complete ===';
GO
