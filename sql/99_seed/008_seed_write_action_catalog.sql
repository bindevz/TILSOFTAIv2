SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

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
        '{"type":"object","properties":{"modelId":{"type":"integer"},"newPrice":{"type":"number"}},"required":["modelId","newPrice"]}',
        'Update the list price of a model.',
        0 
    );
    PRINT 'Seeded example write action (disabled).';
END;
GO
