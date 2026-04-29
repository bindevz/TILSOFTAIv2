SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'ai') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA ai');
END;
GO

IF OBJECT_ID(N'ai.Capability', N'U') IS NULL
BEGIN
    CREATE TABLE ai.Capability
    (
        CapabilityID bigint IDENTITY(1,1) CONSTRAINT PK_ai_Capability PRIMARY KEY,
        CapabilityKey nvarchar(200) NOT NULL,
        Domain nvarchar(100) NOT NULL,
        BusinessArea nvarchar(100) NULL,
        FunctionName nvarchar(200) NOT NULL,
        AdapterType nvarchar(50) NOT NULL,
        Operation nvarchar(100) NOT NULL,
        StoredProcedure sysname NULL,
        ExecutionMode nvarchar(50) NOT NULL,
        ArgumentContract nvarchar(max) NULL,
        ResultSchema nvarchar(max) NULL,
        AnswerPolicy nvarchar(max) NULL,
        SensitivityPolicy nvarchar(max) NULL,
        RequiredRoles nvarchar(max) NULL,
        AllowedTenants nvarchar(max) NULL,
        AllowMultiCall bit NOT NULL CONSTRAINT DF_ai_Capability_AllowMultiCall DEFAULT (0),
        IsActive bit NOT NULL CONSTRAINT DF_ai_Capability_IsActive DEFAULT (1),
        VersionNo int NOT NULL CONSTRAINT DF_ai_Capability_VersionNo DEFAULT (1),
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_ai_Capability_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UX_ai_Capability_CapabilityKey UNIQUE (CapabilityKey),
        CONSTRAINT CK_ai_Capability_ArgumentContract_Json CHECK (ArgumentContract IS NULL OR ISJSON(ArgumentContract) = 1),
        CONSTRAINT CK_ai_Capability_ResultSchema_Json CHECK (ResultSchema IS NULL OR ISJSON(ResultSchema) = 1),
        CONSTRAINT CK_ai_Capability_AnswerPolicy_Json CHECK (AnswerPolicy IS NULL OR ISJSON(AnswerPolicy) = 1),
        CONSTRAINT CK_ai_Capability_SensitivityPolicy_Json CHECK (SensitivityPolicy IS NULL OR ISJSON(SensitivityPolicy) = 1),
        CONSTRAINT CK_ai_Capability_RequiredRoles_Json CHECK (RequiredRoles IS NULL OR ISJSON(RequiredRoles) = 1),
        CONSTRAINT CK_ai_Capability_AllowedTenants_Json CHECK (AllowedTenants IS NULL OR ISJSON(AllowedTenants) = 1)
    );
END;
GO

IF OBJECT_ID(N'ai.CapabilityText', N'U') IS NULL
BEGIN
    CREATE TABLE ai.CapabilityText
    (
        CapabilityTextID bigint IDENTITY(1,1) CONSTRAINT PK_ai_CapabilityText PRIMARY KEY,
        CapabilityKey nvarchar(200) NOT NULL,
        Locale nvarchar(20) NOT NULL,
        ShortName nvarchar(300) NULL,
        Description nvarchar(max) NOT NULL,
        UseWhen nvarchar(max) NULL,
        DoNotUseWhen nvarchar(max) NULL,
        BusinessNotes nvarchar(max) NULL,
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_ai_CapabilityText_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ai_CapabilityText_Capability FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey),
        CONSTRAINT UX_ai_CapabilityText_KeyLocale UNIQUE (CapabilityKey, Locale)
    );
END;
GO

IF OBJECT_ID(N'ai.CapabilityArgument', N'U') IS NULL
BEGIN
    CREATE TABLE ai.CapabilityArgument
    (
        ArgumentID bigint IDENTITY(1,1) CONSTRAINT PK_ai_CapabilityArgument PRIMARY KEY,
        CapabilityKey nvarchar(200) NOT NULL,
        ArgumentName nvarchar(128) NOT NULL,
        ProcParameterName nvarchar(128) NOT NULL,
        DataType nvarchar(50) NOT NULL,
        IsRequired bit NOT NULL,
        DefaultSource nvarchar(100) NULL,
        ValidationRule nvarchar(max) NULL,
        ClarificationPolicy nvarchar(max) NULL,
        DisplayOrder int NOT NULL CONSTRAINT DF_ai_CapabilityArgument_DisplayOrder DEFAULT (0),
        IsActive bit NOT NULL CONSTRAINT DF_ai_CapabilityArgument_IsActive DEFAULT (1),
        CONSTRAINT FK_ai_CapabilityArgument_Capability FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey),
        CONSTRAINT UX_ai_CapabilityArgument_KeyName UNIQUE (CapabilityKey, ArgumentName),
        CONSTRAINT CK_ai_CapabilityArgument_ValidationRule_Json CHECK (ValidationRule IS NULL OR ISJSON(ValidationRule) = 1),
        CONSTRAINT CK_ai_CapabilityArgument_ClarificationPolicy_Json CHECK (ClarificationPolicy IS NULL OR ISJSON(ClarificationPolicy) = 1)
    );
END;
GO

IF OBJECT_ID(N'ai.ArgumentText', N'U') IS NULL
BEGIN
    CREATE TABLE ai.ArgumentText
    (
        ArgumentTextID bigint IDENTITY(1,1) CONSTRAINT PK_ai_ArgumentText PRIMARY KEY,
        CapabilityKey nvarchar(200) NOT NULL,
        ArgumentName nvarchar(128) NOT NULL,
        Locale nvarchar(20) NOT NULL,
        Description nvarchar(max) NOT NULL,
        Aliases nvarchar(max) NULL,
        Examples nvarchar(max) NULL,
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_ai_ArgumentText_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ai_ArgumentText_CapabilityArgument FOREIGN KEY (CapabilityKey, ArgumentName) REFERENCES ai.CapabilityArgument(CapabilityKey, ArgumentName),
        CONSTRAINT UX_ai_ArgumentText_KeyNameLocale UNIQUE (CapabilityKey, ArgumentName, Locale),
        CONSTRAINT CK_ai_ArgumentText_Aliases_Json CHECK (Aliases IS NULL OR ISJSON(Aliases) = 1),
        CONSTRAINT CK_ai_ArgumentText_Examples_Json CHECK (Examples IS NULL OR ISJSON(Examples) = 1)
    );
END;
GO

MERGE ai.Capability AS target
USING (VALUES
    (N'model.count', N'model', N'model', N'model_count', N'sql', N'read', N'ai_model_count', N'read', N'{"type":"object","required":[],"properties":{"season":{"type":"string"}}}', N'{"type":"array"}', N'{"mode":"structured","defaultLocale":"vi"}', N'{"maskFields":[]}', N'[]', 1),
    (N'model.overview.by-code', N'model', N'model', N'model_overview_by_code', N'sql', N'read', N'ai_model_get_overview', N'read', N'{"type":"object","required":["modelCode"],"properties":{"modelCode":{"type":"string"}}}', N'{"type":"object"}', N'{"mode":"structured","defaultLocale":"vi"}', N'{"maskFields":[]}', N'[]', 0),
    (N'model.pieces.by-code', N'model', N'model', N'model_pieces_by_code', N'sql', N'read', N'ai_model_get_pieces', N'read', N'{"type":"object","required":["modelCode"],"properties":{"modelCode":{"type":"string"}}}', N'{"type":"array"}', N'{"mode":"structured","defaultLocale":"vi"}', N'{"maskFields":[]}', N'[]', 0),
    (N'model.materials.by-code', N'model', N'model', N'model_materials_by_code', N'sql', N'read', N'ai_model_get_materials', N'read', N'{"type":"object","required":["modelCode"],"properties":{"modelCode":{"type":"string"}}}', N'{"type":"array"}', N'{"mode":"structured","defaultLocale":"vi"}', N'{"maskFields":[]}', N'[]', 0),
    (N'model.compare', N'model', N'model', N'model_compare', N'sql', N'read', N'ai_model_compare', N'read', N'{"type":"object","required":["modelCodes"],"properties":{"modelCodes":{"type":"array","items":{"type":"string"},"minItems":2}}}', N'{"type":"array"}', N'{"mode":"structured","defaultLocale":"vi"}', N'{"maskFields":[]}', N'[]', 1),
    (N'model.packaging.by-code', N'model', N'model', N'model_packaging_by_code', N'sql', N'read', N'ai_model_get_packaging', N'read', N'{"type":"object","required":["modelCode"],"properties":{"modelCode":{"type":"string"}}}', N'{"type":"array"}', N'{"mode":"structured","defaultLocale":"vi"}', N'{"maskFields":[]}', N'[]', 0)
) AS source (CapabilityKey, Domain, BusinessArea, FunctionName, AdapterType, Operation, StoredProcedure, ExecutionMode, ArgumentContract, ResultSchema, AnswerPolicy, SensitivityPolicy, RequiredRoles, AllowMultiCall)
ON target.CapabilityKey = source.CapabilityKey
WHEN MATCHED THEN UPDATE SET
    Domain = source.Domain,
    BusinessArea = source.BusinessArea,
    FunctionName = source.FunctionName,
    AdapterType = source.AdapterType,
    Operation = source.Operation,
    StoredProcedure = source.StoredProcedure,
    ExecutionMode = source.ExecutionMode,
    ArgumentContract = source.ArgumentContract,
    ResultSchema = source.ResultSchema,
    AnswerPolicy = source.AnswerPolicy,
    SensitivityPolicy = source.SensitivityPolicy,
    RequiredRoles = source.RequiredRoles,
    AllowMultiCall = source.AllowMultiCall,
    IsActive = 1,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, Domain, BusinessArea, FunctionName, AdapterType, Operation, StoredProcedure, ExecutionMode, ArgumentContract, ResultSchema, AnswerPolicy, SensitivityPolicy, RequiredRoles, AllowMultiCall)
    VALUES (source.CapabilityKey, source.Domain, source.BusinessArea, source.FunctionName, source.AdapterType, source.Operation, source.StoredProcedure, source.ExecutionMode, source.ArgumentContract, source.ResultSchema, source.AnswerPolicy, source.SensitivityPolicy, source.RequiredRoles, source.AllowMultiCall);
GO

MERGE ai.CapabilityText AS target
USING (VALUES
    (N'model.count', N'en-US', N'Model count', N'Count product models.', N'Use for model count and total models.', NULL, N'Model-only read-only SQL capability.'),
    (N'model.count', N'vi-VN', N'Dem model', N'Dem so luong model san pham.', N'Dung khi hoi co bao nhieu model hoac tong so model.', NULL, N'Model-only read-only SQL capability.'),
    (N'model.overview.by-code', N'en-US', N'Model overview', N'Return overview information for one model code.', N'Use for model details and information by code.', NULL, N'Model-only read-only SQL capability.'),
    (N'model.overview.by-code', N'vi-VN', N'Thong tin model', N'Tra thong tin tong quan cua mot ma model.', N'Dung khi hoi thong tin model theo ma.', NULL, N'Model-only read-only SQL capability.'),
    (N'model.pieces.by-code', N'en-US', N'Model pieces', N'Return pieces/components for one model code.', N'Use for pieces, parts, components, or child models.', NULL, N'Model-only read-only SQL capability.'),
    (N'model.pieces.by-code', N'vi-VN', N'Piece cua model', N'Tra danh sach piece cua mot ma model.', N'Dung khi hoi model gom nhung piece nao.', NULL, N'Model-only read-only SQL capability.'),
    (N'model.materials.by-code', N'en-US', N'Model materials', N'Return materials for one model code.', N'Use for material, raw material, or item material questions.', NULL, N'Model-only read-only SQL capability.'),
    (N'model.materials.by-code', N'vi-VN', N'Nguyen lieu model', N'Tra danh sach nguyen lieu cua mot ma model.', N'Dung khi hoi material, vat tu, nguyen lieu cua model.', NULL, N'Model-only read-only SQL capability.'),
    (N'model.compare', N'en-US', N'Compare models', N'Compare two or more model codes.', N'Use for comparing model codes.', NULL, N'Model-only read-only SQL capability.'),
    (N'model.compare', N'vi-VN', N'So sanh model', N'So sanh hai hoac nhieu ma model.', N'Dung khi hoi so sanh model.', NULL, N'Model-only read-only SQL capability.'),
    (N'model.packaging.by-code', N'en-US', N'Model packaging', N'Return packaging options for one model code.', N'Use for packaging, carton, and qnt40hc questions.', NULL, N'Model-only read-only SQL capability.'),
    (N'model.packaging.by-code', N'vi-VN', N'Dong goi model', N'Tra thong tin dong goi cua mot ma model.', N'Dung khi hoi packaging, carton, dong goi, qnt40hc.', NULL, N'Model-only read-only SQL capability.')
) AS source (CapabilityKey, Locale, ShortName, Description, UseWhen, DoNotUseWhen, BusinessNotes)
ON target.CapabilityKey = source.CapabilityKey AND target.Locale = source.Locale
WHEN MATCHED THEN UPDATE SET
    ShortName = source.ShortName,
    Description = source.Description,
    UseWhen = source.UseWhen,
    DoNotUseWhen = source.DoNotUseWhen,
    BusinessNotes = source.BusinessNotes,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, Locale, ShortName, Description, UseWhen, DoNotUseWhen, BusinessNotes)
    VALUES (source.CapabilityKey, source.Locale, source.ShortName, source.Description, source.UseWhen, source.DoNotUseWhen, source.BusinessNotes);
GO

MERGE ai.CapabilityArgument AS target
USING (VALUES
    (N'model.count', N'season', N'@Season', N'string', 0, 1),
    (N'model.overview.by-code', N'modelCode', N'@ModelCode', N'string', 1, 1),
    (N'model.pieces.by-code', N'modelCode', N'@ModelCode', N'string', 1, 1),
    (N'model.materials.by-code', N'modelCode', N'@ModelCode', N'string', 1, 1),
    (N'model.compare', N'modelCodes', N'@ModelCodesJson', N'array', 1, 1),
    (N'model.packaging.by-code', N'modelCode', N'@ModelCode', N'string', 1, 1)
) AS source (CapabilityKey, ArgumentName, ProcParameterName, DataType, IsRequired, DisplayOrder)
ON target.CapabilityKey = source.CapabilityKey AND target.ArgumentName = source.ArgumentName
WHEN MATCHED THEN UPDATE SET
    ProcParameterName = source.ProcParameterName,
    DataType = source.DataType,
    IsRequired = source.IsRequired,
    DisplayOrder = source.DisplayOrder,
    IsActive = 1
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, ArgumentName, ProcParameterName, DataType, IsRequired, DisplayOrder)
    VALUES (source.CapabilityKey, source.ArgumentName, source.ProcParameterName, source.DataType, source.IsRequired, source.DisplayOrder);
GO

MERGE ai.ArgumentText AS target
USING (VALUES
    (N'model.count', N'season', N'en-US', N'Optional season filter.', N'["season"]', N'["count models for season 2026"]'),
    (N'model.count', N'season', N'vi-VN', N'Loc season neu co.', N'["season","mua"]', N'["co bao nhieu model season 2026"]'),
    (N'model.overview.by-code', N'modelCode', N'en-US', N'Model code to inspect.', N'["model","model code"]', N'["model ABC details"]'),
    (N'model.overview.by-code', N'modelCode', N'vi-VN', N'Ma model can xem.', N'["model","ma model"]', N'["thong tin model ABC"]'),
    (N'model.pieces.by-code', N'modelCode', N'en-US', N'Model code whose pieces should be listed.', N'["piece","component","model code"]', N'["pieces for SET-DINING-001"]'),
    (N'model.pieces.by-code', N'modelCode', N'vi-VN', N'Ma model can xem piece.', N'["piece","thanh phan","ma model"]', N'["SET-DINING-001 gom nhung piece nao"]'),
    (N'model.materials.by-code', N'modelCode', N'en-US', N'Model code whose materials should be listed.', N'["material","materials","model code"]', N'["materials for ABC"]'),
    (N'model.materials.by-code', N'modelCode', N'vi-VN', N'Ma model can xem nguyen lieu.', N'["material","vat tu","nguyen lieu"]', N'["nguyen lieu model ABC"]'),
    (N'model.compare', N'modelCodes', N'en-US', N'Two or more model codes to compare.', N'["compare","model codes"]', N'["compare ABC and XYZ"]'),
    (N'model.compare', N'modelCodes', N'vi-VN', N'Hai hoac nhieu ma model can so sanh.', N'["so sanh","ma model"]', N'["so sanh model ABC va XYZ"]'),
    (N'model.packaging.by-code', N'modelCode', N'en-US', N'Model code whose packaging should be listed.', N'["packaging","carton","qnt40hc"]', N'["packaging for ABC"]'),
    (N'model.packaging.by-code', N'modelCode', N'vi-VN', N'Ma model can xem dong goi.', N'["dong goi","carton","qnt40hc"]', N'["dong goi model ABC"]')
) AS source (CapabilityKey, ArgumentName, Locale, Description, Aliases, Examples)
ON target.CapabilityKey = source.CapabilityKey AND target.ArgumentName = source.ArgumentName AND target.Locale = source.Locale
WHEN MATCHED THEN UPDATE SET
    Description = source.Description,
    Aliases = source.Aliases,
    Examples = source.Examples,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, ArgumentName, Locale, Description, Aliases, Examples)
    VALUES (source.CapabilityKey, source.ArgumentName, source.Locale, source.Description, source.Aliases, source.Examples);
GO

IF OBJECT_ID(N'ai.KnowledgeChunk', N'U') IS NULL
BEGIN
    CREATE TABLE ai.KnowledgeChunk
    (
        ChunkID bigint IDENTITY(1,1) CONSTRAINT PK_ai_KnowledgeChunk PRIMARY KEY,
        TenantID nvarchar(100) NULL,
        ChunkType nvarchar(50) NOT NULL,
        ObjectKey nvarchar(300) NOT NULL,
        Domain nvarchar(100) NULL,
        Locale nvarchar(20) NULL,
        Title nvarchar(300) NULL,
        ContentText nvarchar(max) NOT NULL,
        Metadata nvarchar(max) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_ai_KnowledgeChunk_IsActive DEFAULT (1),
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_ai_KnowledgeChunk_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_ai_KnowledgeChunk_Metadata_Json CHECK (Metadata IS NULL OR ISJSON(Metadata) = 1),
        CONSTRAINT CK_ai_KnowledgeChunk_ChunkType CHECK (ChunkType IN (N'domain', N'capability', N'argument', N'result_field', N'glossary', N'example', N'entity_alias'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ai_KnowledgeChunk_Search' AND object_id = OBJECT_ID(N'ai.KnowledgeChunk'))
BEGIN
    CREATE INDEX IX_ai_KnowledgeChunk_Search
        ON ai.KnowledgeChunk (IsActive, TenantID, Locale, Domain, ChunkType, UpdatedAt DESC)
        INCLUDE (ObjectKey, Title);
END;
GO

IF OBJECT_ID(N'ai.EntityAlias', N'U') IS NULL
BEGIN
    CREATE TABLE ai.EntityAlias
    (
        EntityAliasID bigint IDENTITY(1,1) CONSTRAINT PK_ai_EntityAlias PRIMARY KEY,
        TenantID nvarchar(100) NULL,
        EntityType nvarchar(100) NOT NULL,
        EntityID nvarchar(300) NOT NULL,
        CanonicalCode nvarchar(300) NULL,
        CanonicalName nvarchar(300) NULL,
        AliasText nvarchar(300) NOT NULL,
        AliasNormalized nvarchar(300) NOT NULL,
        Locale nvarchar(20) NULL,
        Metadata nvarchar(max) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_ai_EntityAlias_IsActive DEFAULT (1),
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_ai_EntityAlias_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_ai_EntityAlias_Metadata_Json CHECK (Metadata IS NULL OR ISJSON(Metadata) = 1)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ai_EntityAlias_Search' AND object_id = OBJECT_ID(N'ai.EntityAlias'))
BEGIN
    CREATE INDEX IX_ai_EntityAlias_Search
        ON ai.EntityAlias (IsActive, TenantID, EntityType, AliasNormalized, Locale)
        INCLUDE (EntityID, CanonicalCode, CanonicalName);
END;
GO

MERGE ai.EntityAlias AS target
USING (VALUES
    (N'default', N'model', N'ABC', N'ABC', N'Acceptance Chair', N'ABC', N'abc', N'en', N'{"source":"local-test-seed"}'),
    (N'default', N'model', N'ABC', N'ABC', N'Acceptance Chair', N'ABC', N'abc', N'vi', N'{"source":"local-test-seed"}'),
    (N'default', N'model', N'XYZ', N'XYZ', N'Acceptance Table', N'XYZ', N'xyz', N'en', N'{"source":"local-test-seed"}'),
    (N'default', N'model', N'XYZ', N'XYZ', N'Acceptance Table', N'XYZ', N'xyz', N'vi', N'{"source":"local-test-seed"}'),
    (N'default', N'model', N'SET-DINING-001', N'SET-DINING-001', N'Acceptance Dining Set', N'SET-DINING-001', N'set-dining-001', N'en', N'{"source":"local-test-seed"}'),
    (N'default', N'model', N'SET-DINING-001', N'SET-DINING-001', N'Acceptance Dining Set', N'SET-DINING-001', N'set-dining-001', N'vi', N'{"source":"local-test-seed"}')
) AS source (TenantID, EntityType, EntityID, CanonicalCode, CanonicalName, AliasText, AliasNormalized, Locale, Metadata)
ON ISNULL(target.TenantID, N'') = ISNULL(source.TenantID, N'')
   AND target.EntityType = source.EntityType
   AND target.EntityID = source.EntityID
   AND target.AliasNormalized = source.AliasNormalized
   AND ISNULL(target.Locale, N'') = ISNULL(source.Locale, N'')
WHEN MATCHED THEN UPDATE SET
    CanonicalCode = source.CanonicalCode,
    CanonicalName = source.CanonicalName,
    AliasText = source.AliasText,
    Metadata = source.Metadata,
    IsActive = 1,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (TenantID, EntityType, EntityID, CanonicalCode, CanonicalName, AliasText, AliasNormalized, Locale, Metadata)
    VALUES (source.TenantID, source.EntityType, source.EntityID, source.CanonicalCode, source.CanonicalName, source.AliasText, source.AliasNormalized, source.Locale, source.Metadata);
GO

MERGE ai.KnowledgeChunk AS target
USING (VALUES
    (NULL, N'capability', N'model.count', N'model', N'en', N'Model count', N'Use model.count when the user asks how many models exist, count models, total product models, or asks in Vietnamese: có bao nhiêu model.', N'{"capabilityKey":"model.count"}'),
    (NULL, N'capability', N'model.count', N'model', N'vi', N'Đếm model', N'Dùng model.count khi người dùng hỏi có bao nhiêu model, tổng số model, số lượng model, hoặc count models.', N'{"capabilityKey":"model.count"}'),
    (NULL, N'capability', N'model.overview.by-code', N'model', N'en', N'Model overview by code', N'Use model.overview.by-code for model details, model information, product model overview, thông tin model, or xem thông tin model by model code.', N'{"capabilityKey":"model.overview.by-code"}'),
    (NULL, N'capability', N'model.overview.by-code', N'model', N'vi', N'Thông tin model theo mã', N'Dùng model.overview.by-code cho thông tin model, chi tiết model, xem thông tin model theo mã model.', N'{"capabilityKey":"model.overview.by-code"}'),
    (NULL, N'capability', N'model.pieces.by-code', N'model', N'en', N'Model pieces by code', N'Use model.pieces.by-code for product model pieces, components, child models, gồm những piece nào, or parts in a model.', N'{"capabilityKey":"model.pieces.by-code"}'),
    (NULL, N'capability', N'model.pieces.by-code', N'model', N'vi', N'Piece của model theo mã', N'Dùng model.pieces.by-code khi hỏi model gồm những piece nào, thành phần model, chi tiết piece, hoặc child model.', N'{"capabilityKey":"model.pieces.by-code"}'),
    (NULL, N'capability', N'model.materials.by-code', N'model', N'en', N'Model materials by code', N'Use model.materials.by-code for materials, nguyên liệu, vật tư, material list, or show materials for a model code.', N'{"capabilityKey":"model.materials.by-code"}'),
    (NULL, N'capability', N'model.materials.by-code', N'model', N'vi', N'Nguyên liệu model theo mã', N'Dùng model.materials.by-code khi hỏi nguyên liệu, vật tư, danh sách material, hoặc materials của một model.', N'{"capabilityKey":"model.materials.by-code"}'),
    (NULL, N'capability', N'model.compare', N'model', N'en', N'Compare models', N'Use model.compare when the user asks to compare two or more model codes, compare model ABC and XYZ, or so sánh model.', N'{"capabilityKey":"model.compare"}'),
    (NULL, N'capability', N'model.compare', N'model', N'vi', N'So sánh model', N'Dùng model.compare khi người dùng hỏi so sánh model, so sánh hai mã model, hoặc compare models.', N'{"capabilityKey":"model.compare"}'),
    (NULL, N'capability', N'model.packaging.by-code', N'model', N'en', N'Model packaging by code', N'Use model.packaging.by-code for packaging, carton, packing options, qnt40hc, or đóng gói của model.', N'{"capabilityKey":"model.packaging.by-code"}'),
    (NULL, N'capability', N'model.packaging.by-code', N'model', N'vi', N'Đóng gói model theo mã', N'Dùng model.packaging.by-code khi hỏi đóng gói, carton, packaging option, hoặc qnt40hc của model.', N'{"capabilityKey":"model.packaging.by-code"}')
) AS source (TenantID, ChunkType, ObjectKey, Domain, Locale, Title, ContentText, Metadata)
ON ISNULL(target.TenantID, N'') = ISNULL(source.TenantID, N'')
   AND target.ChunkType = source.ChunkType
   AND target.ObjectKey = source.ObjectKey
   AND ISNULL(target.Locale, N'') = ISNULL(source.Locale, N'')
WHEN MATCHED THEN UPDATE SET
    Domain = source.Domain,
    Title = source.Title,
    ContentText = source.ContentText,
    Metadata = source.Metadata,
    IsActive = 1,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (TenantID, ChunkType, ObjectKey, Domain, Locale, Title, ContentText, Metadata)
    VALUES (source.TenantID, source.ChunkType, source.ObjectKey, source.Domain, source.Locale, source.Title, source.ContentText, source.Metadata);
GO

IF OBJECT_ID(N'ai.ToolRoutingTrace', N'U') IS NULL
BEGIN
    CREATE TABLE ai.ToolRoutingTrace
    (
        TraceID bigint IDENTITY(1,1) CONSTRAINT PK_ai_ToolRoutingTrace PRIMARY KEY,
        CorrelationID uniqueidentifier NOT NULL,
        TenantID nvarchar(100) NOT NULL,
        UserID nvarchar(100) NOT NULL,
        Locale nvarchar(20) NULL,
        UserMessageHash varbinary(32) NULL,
        UserMessageRedacted nvarchar(max) NULL,
        CandidateDomainsJson nvarchar(max) NULL,
        CandidateToolsJson nvarchar(max) NULL,
        HardSignalsJson nvarchar(max) NULL,
        AdvertisedFunctionToolsJson nvarchar(max) NULL,
        SelectedTool nvarchar(200) NULL,
        SelectedFunction nvarchar(200) NULL,
        CandidateDomainCount int NULL,
        CandidateCapabilityCount int NULL,
        AdvertisedToolCount int NULL,
        ArgumentsJson nvarchar(max) NULL,
        ArgumentsBeforeNormalizationJson nvarchar(max) NULL,
        ArgumentsAfterNormalizationJson nvarchar(max) NULL,
        ValidationResultJson nvarchar(max) NULL,
        AdapterType nvarchar(100) NULL,
        [RowCount] int NULL,
        AnswerMode nvarchar(50) NULL,
        LatencyMs int NULL,
        LatencyByStageJson nvarchar(max) NULL,
        ModelProvider nvarchar(100) NULL,
        Success bit NOT NULL,
        ErrorCode nvarchar(100) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_ai_ToolRoutingTrace_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_ai_ToolRoutingTrace_CandidateDomainsJson CHECK (CandidateDomainsJson IS NULL OR ISJSON(CandidateDomainsJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_CandidateToolsJson CHECK (CandidateToolsJson IS NULL OR ISJSON(CandidateToolsJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_HardSignalsJson CHECK (HardSignalsJson IS NULL OR ISJSON(HardSignalsJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_AdvertisedFunctionToolsJson CHECK (AdvertisedFunctionToolsJson IS NULL OR ISJSON(AdvertisedFunctionToolsJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_ArgumentsJson CHECK (ArgumentsJson IS NULL OR ISJSON(ArgumentsJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_ArgumentsBeforeNormalizationJson CHECK (ArgumentsBeforeNormalizationJson IS NULL OR ISJSON(ArgumentsBeforeNormalizationJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_ArgumentsAfterNormalizationJson CHECK (ArgumentsAfterNormalizationJson IS NULL OR ISJSON(ArgumentsAfterNormalizationJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_ValidationResultJson CHECK (ValidationResultJson IS NULL OR ISJSON(ValidationResultJson) = 1),
        CONSTRAINT CK_ai_ToolRoutingTrace_LatencyByStageJson CHECK (LatencyByStageJson IS NULL OR ISJSON(LatencyByStageJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'ai.ToolRoutingTrace', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'HardSignalsJson') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD HardSignalsJson nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'AdvertisedFunctionToolsJson') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD AdvertisedFunctionToolsJson nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'ArgumentsBeforeNormalizationJson') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD ArgumentsBeforeNormalizationJson nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'ArgumentsAfterNormalizationJson') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD ArgumentsAfterNormalizationJson nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'AdapterType') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD AdapterType nvarchar(100) NULL;
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'RowCount') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD [RowCount] int NULL;
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'LatencyByStageJson') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD LatencyByStageJson nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'ModelProvider') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD ModelProvider nvarchar(100) NULL;
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'SelectedFunction') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD SelectedFunction nvarchar(200) NULL;
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'CandidateDomainCount') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD CandidateDomainCount int NULL;
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'CandidateCapabilityCount') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD CandidateCapabilityCount int NULL;
    IF COL_LENGTH(N'ai.ToolRoutingTrace', N'AdvertisedToolCount') IS NULL
        ALTER TABLE ai.ToolRoutingTrace ADD AdvertisedToolCount int NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ai_ToolRoutingTrace_TenantCreated' AND object_id = OBJECT_ID(N'ai.ToolRoutingTrace'))
BEGIN
    CREATE INDEX IX_ai_ToolRoutingTrace_TenantCreated
        ON ai.ToolRoutingTrace (TenantID, CreatedAt DESC)
        INCLUDE (Success, SelectedTool, SelectedFunction, CandidateDomainCount, CandidateCapabilityCount, AdvertisedToolCount, ErrorCode, AnswerMode);
END;
GO

PRINT N'Agent semantic knowledge and routing trace tables are present.';
GO
