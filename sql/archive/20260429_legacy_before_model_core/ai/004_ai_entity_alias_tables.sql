IF OBJECT_ID('ai.EntityAlias', 'U') IS NULL
BEGIN
    EXEC(N'
    CREATE TABLE ai.EntityAlias
    (
        EntityAliasID BIGINT IDENTITY CONSTRAINT PK_ai_EntityAlias PRIMARY KEY,
        TenantID NVARCHAR(100) NULL,
        EntityType NVARCHAR(100) NOT NULL,
        EntityID NVARCHAR(100) NOT NULL,
        CanonicalCode NVARCHAR(100) NULL,
        CanonicalName NVARCHAR(300) NULL,
        AliasText NVARCHAR(300) NOT NULL,
        AliasNormalized NVARCHAR(300) NOT NULL,
        Locale NVARCHAR(20) NULL,
        Metadata NVARCHAR(MAX) NULL,
        Embedding VECTOR(1536) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ai_EntityAlias_IsActive DEFAULT (1),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ai_EntityAlias_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_ai_EntityAlias_Metadata_Json CHECK (Metadata IS NULL OR ISJSON(Metadata) = 1),
        CONSTRAINT CK_ai_EntityAlias_EntityType CHECK (EntityType IN (N''customer'', N''supplier'', N''factory'', N''warehouse'', N''item'', N''product_model'', N''season'', N''currency'', N''country'', N''employee'', N''po'', N''so'', N''invoice'', N''container''))
    );');
END;
GO

MERGE ai.EntityAlias AS target
USING (VALUES
    (NULL, N'customer', N'CUST-IKEA', N'IKEA', N'IKEA', N'IKEA', N'ikea', N'en-US', N'{"domain":"sales"}'),
    (NULL, N'customer', N'CUST-IKEA', N'IKEA', N'IKEA', N'khách IKEA', N'khach ikea', N'vi-VN', N'{"domain":"accounting"}'),
    (NULL, N'supplier', N'SUP-ABC', N'ABC', N'ABC Supplier', N'ABC Supplier', N'abc supplier', N'en-US', N'{"domain":"purchasing"}'),
    (NULL, N'supplier', N'SUP-ABC', N'ABC', N'ABC Supplier', N'NCC ABC', N'ncc abc', N'vi-VN', N'{"domain":"purchasing"}'),
    (NULL, N'warehouse', N'WH-MAIN', N'MAIN', N'Main Warehouse', N'main warehouse', N'main warehouse', N'en-US', N'{"domain":"warehouse"}'),
    (NULL, N'warehouse', N'WH-MAIN', N'MAIN', N'Main Warehouse', N'kho chính', N'kho chinh', N'vi-VN', N'{"domain":"warehouse"}'),
    (NULL, N'item', N'ITEM-CHAIR-001', N'CHAIR-001', N'Dining Chair 001', N'CHAIR-001', N'chair-001', NULL, N'{"domain":"warehouse"}'),
    (NULL, N'product_model', N'MODEL-M123', N'M123', N'Model M123', N'M123', N'm123', NULL, N'{"domain":"product_model"}'),
    (NULL, N'currency', N'USD', N'USD', N'US Dollar', N'đô la', N'do la', N'vi-VN', N'{"iso":"USD"}'),
    (NULL, N'season', N'SSN-2026S', N'2026S', N'Spring 2026', N'SS26', N'ss26', NULL, N'{"year":2026}')
) AS source (TenantID, EntityType, EntityID, CanonicalCode, CanonicalName, AliasText, AliasNormalized, Locale, Metadata)
ON ISNULL(target.TenantID, N'') = ISNULL(source.TenantID, N'')
   AND target.EntityType = source.EntityType
   AND target.EntityID = source.EntityID
   AND target.AliasNormalized = source.AliasNormalized
WHEN MATCHED THEN UPDATE SET
    CanonicalCode = source.CanonicalCode,
    CanonicalName = source.CanonicalName,
    AliasText = source.AliasText,
    Locale = source.Locale,
    Metadata = source.Metadata,
    IsActive = 1,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (TenantID, EntityType, EntityID, CanonicalCode, CanonicalName, AliasText, AliasNormalized, Locale, Metadata)
    VALUES (source.TenantID, source.EntityType, source.EntityID, source.CanonicalCode, source.CanonicalName, source.AliasText, source.AliasNormalized, source.Locale, source.Metadata);
GO
