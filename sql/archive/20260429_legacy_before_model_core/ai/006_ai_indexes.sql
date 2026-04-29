IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ai_Capability_DomainActive' AND object_id = OBJECT_ID('ai.Capability'))
BEGIN
    CREATE INDEX IX_ai_Capability_DomainActive
        ON ai.Capability (Domain, IsActive, Operation)
        INCLUDE (CapabilityKey, BusinessArea, AdapterType, ExecutionMode);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ai_CapabilityText_KeyLocale' AND object_id = OBJECT_ID('ai.CapabilityText'))
BEGIN
    CREATE INDEX IX_ai_CapabilityText_KeyLocale
        ON ai.CapabilityText (CapabilityKey, Locale);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ai_KnowledgeChunk_Search' AND object_id = OBJECT_ID('ai.KnowledgeChunk'))
BEGIN
    CREATE INDEX IX_ai_KnowledgeChunk_Search
        ON ai.KnowledgeChunk (IsActive, TenantID, Locale, Domain, ChunkType, UpdatedAt DESC)
        INCLUDE (ObjectKey, Title);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ai_EntityAlias_Search' AND object_id = OBJECT_ID('ai.EntityAlias'))
BEGIN
    CREATE INDEX IX_ai_EntityAlias_Search
        ON ai.EntityAlias (IsActive, TenantID, EntityType, AliasNormalized, Locale)
        INCLUDE (EntityID, CanonicalCode, CanonicalName);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ai_ToolRoutingTrace_TenantCreated' AND object_id = OBJECT_ID('ai.ToolRoutingTrace'))
BEGIN
    CREATE INDEX IX_ai_ToolRoutingTrace_TenantCreated
        ON ai.ToolRoutingTrace (TenantID, CreatedAt DESC)
        INCLUDE (Success, SelectedTool, SelectedFunction, CandidateDomainCount, CandidateCapabilityCount, AdvertisedToolCount, ErrorCode, AnswerMode);
END;
GO
