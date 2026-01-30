SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Example 1: Update model price (from SQL 2025 demo)
IF NOT EXISTS (SELECT 1 FROM dbo.WriteActionCatalog WHERE TenantId = 'demo' AND ActionName = 'update_model_price')
BEGIN
    INSERT INTO dbo.WriteActionCatalog
    (TenantId, ActionName, SpName, RequiredRoles, JsonSchema, Description, IsEnabled)
    VALUES
    (
        'demo',
        'update_model_price',
        'app_model_update_price', 
        'Manager,Admin',
        '{"type":"object","properties":{"modelId":{"type":"integer"},"newPrice":{"type":"number"}},"required":["modelId","newPrice"],"additionalProperties":false}',
        'Update the list price of a model.',
        0 
    );
    PRINT 'Seeded example write action: update_model_price (disabled)';
END;
GO

-- Example 2: Generic data update action
IF NOT EXISTS (SELECT 1 FROM dbo.WriteActionCatalog WHERE TenantId = 'demo' AND ActionName = 'generic_data_update')
BEGIN
    INSERT INTO dbo.WriteActionCatalog
    (TenantId, ActionName, SpName, RequiredRoles, JsonSchema, Description, IsEnabled)
    VALUES
    (
        'demo',
        'generic_data_update',
        'app_generic_update',
        'Writer,Admin',
        '{"type":"object","properties":{"id":{"type":"integer","minimum":1},"data":{"type":"object"},"reason":{"type":"string","minLength":5}},"required":["id","data","reason"],"additionalProperties":false}',
        'Generic data update action requiring ID, data object, and reason.',
        0
    );
    PRINT 'Seeded example write action: generic_data_update (disabled)';
END;
GO

-- Example 3: Batch record creation
IF NOT EXISTS (SELECT 1 FROM dbo.WriteActionCatalog WHERE TenantId = 'demo' AND ActionName = 'create_records_batch')
BEGIN
    INSERT INTO dbo.WriteActionCatalog
    (TenantId, ActionName, SpName, RequiredRoles, JsonSchema, Description, IsEnabled)
    VALUES
    (
        'demo',
        'create_records_batch',
        'app_create_records_batch',
        'Admin',
        '{"type":"object","properties":{"records":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"},"value":{"type":"number"}},"required":["name","value"]},"minItems":1,"maxItems":100}},"required":["records"],"additionalProperties":false}',
        'Create multiple records in batch (max 100, requires admin role).',
        0
    );
    PRINT 'Seeded example write action: create_records_batch (disabled)';
END;
GO
