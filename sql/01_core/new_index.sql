IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Conversation_Tenant_CreatedAtUtc' AND object_id = OBJECT_ID('dbo.Conversation'))
BEGIN
    CREATE INDEX IX_Conversation_Tenant_CreatedAtUtc ON dbo.Conversation (TenantId, CreatedAtUtc);
END;
GO

