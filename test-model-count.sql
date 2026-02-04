-- Test ai_model_count stored procedure
DECLARE @result nvarchar(max);

EXEC dbo.ai_model_count 
    @TenantId = 'demo', 
    @ArgsJson = '{"season":"24/25"}';

PRINT 'Test with season filter completed';
PRINT '';

-- Test without filter
EXEC dbo.ai_model_count 
    @TenantId = 'demo', 
    @ArgsJson = '{}';

PRINT 'Test without season filter completed';
