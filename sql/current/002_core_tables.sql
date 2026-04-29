SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.Model', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Model
    (
        ModelId int IDENTITY(1,1) NOT NULL,
        TenantId nvarchar(50) NOT NULL,
        Language nvarchar(10) NOT NULL,
        ModelCode nvarchar(50) NOT NULL,
        Name nvarchar(200) NOT NULL,
        Description nvarchar(2000) NULL,
        PieceCount int NOT NULL CONSTRAINT DF_Model_PieceCount DEFAULT (0),
        TotalCbm decimal(18,4) NOT NULL CONSTRAINT DF_Model_TotalCbm DEFAULT (0),
        TotalWeightKg decimal(18,4) NOT NULL CONSTRAINT DF_Model_TotalWeightKg DEFAULT (0),
        LoadabilityIndex decimal(18,4) NULL,
        Qnt40HC int NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Model_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_Model PRIMARY KEY (ModelId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Model_Tenant_Language' AND object_id = OBJECT_ID('dbo.Model'))
    CREATE INDEX IX_Model_Tenant_Language ON dbo.Model (TenantId, Language);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Model_Tenant_Code' AND object_id = OBJECT_ID('dbo.Model'))
    CREATE INDEX IX_Model_Tenant_Code ON dbo.Model (TenantId, ModelCode);
GO

IF OBJECT_ID('dbo.ModelPiece', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ModelPiece
    (
        ModelPieceId int IDENTITY(1,1) NOT NULL,
        TenantId nvarchar(50) NOT NULL,
        ModelId int NOT NULL,
        PieceName nvarchar(200) NOT NULL,
        Quantity int NOT NULL CONSTRAINT DF_ModelPiece_Quantity DEFAULT (1),
        ChildModelId int NULL,
        Sequence int NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ModelPiece_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_ModelPiece PRIMARY KEY (ModelPieceId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ModelPiece_Tenant_Model' AND object_id = OBJECT_ID('dbo.ModelPiece'))
    CREATE INDEX IX_ModelPiece_Tenant_Model ON dbo.ModelPiece (TenantId, ModelId);
GO

IF OBJECT_ID('dbo.Material', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Material
    (
        MaterialId int IDENTITY(1,1) NOT NULL,
        TenantId nvarchar(50) NOT NULL,
        Language nvarchar(10) NOT NULL,
        MaterialCode nvarchar(50) NOT NULL,
        Name nvarchar(200) NOT NULL,
        Category nvarchar(100) NULL,
        Section nvarchar(100) NULL,
        Description nvarchar(2000) NULL,
        DensityKgPerM3 decimal(18,4) NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Material_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_Material PRIMARY KEY (MaterialId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Material_Tenant_Language' AND object_id = OBJECT_ID('dbo.Material'))
    CREATE INDEX IX_Material_Tenant_Language ON dbo.Material (TenantId, Language);
GO

IF OBJECT_ID('dbo.ModelMaterial', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ModelMaterial
    (
        ModelMaterialId int IDENTITY(1,1) NOT NULL,
        TenantId nvarchar(50) NOT NULL,
        ModelId int NOT NULL,
        MaterialId int NOT NULL,
        Section nvarchar(100) NULL,
        Quantity decimal(18,4) NOT NULL CONSTRAINT DF_ModelMaterial_Quantity DEFAULT (0),
        Unit nvarchar(50) NULL,
        WeightKg decimal(18,4) NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ModelMaterial_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_ModelMaterial PRIMARY KEY (ModelMaterialId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ModelMaterial_Tenant_Model' AND object_id = OBJECT_ID('dbo.ModelMaterial'))
    CREATE INDEX IX_ModelMaterial_Tenant_Model ON dbo.ModelMaterial (TenantId, ModelId);
GO

IF OBJECT_ID('dbo.ModelPackagingOption', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ModelPackagingOption
    (
        PackagingOptionId int IDENTITY(1,1) NOT NULL,
        TenantId nvarchar(50) NOT NULL,
        ModelId int NOT NULL,
        OptionName nvarchar(200) NOT NULL,
        PackagingType nvarchar(100) NULL,
        UnitsPerCarton int NULL,
        CartonCbm decimal(18,4) NULL,
        CartonWeightKg decimal(18,4) NULL,
        LoadabilityIndex decimal(18,4) NULL,
        Qnt40HC int NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ModelPackagingOption_UpdatedAtUtc DEFAULT sysutcdatetime(),
        CONSTRAINT PK_ModelPackagingOption PRIMARY KEY (PackagingOptionId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ModelPackagingOption_Tenant_Model' AND object_id = OBJECT_ID('dbo.ModelPackagingOption'))
    CREATE INDEX IX_ModelPackagingOption_Tenant_Model ON dbo.ModelPackagingOption (TenantId, ModelId);
GO

CREATE OR ALTER VIEW dbo.vw_ModelSemantic
AS
SELECT
    m.TenantId,
    m.Language,
    m.ModelId,
    m.ModelCode,
    m.Name,
    m.Description,
    m.TotalCbm,
    m.TotalWeightKg,
    m.LoadabilityIndex,
    m.Qnt40HC,
    m.PieceCount,
    COUNT(DISTINCT mp.ModelPieceId) AS BoxInSet,
    MAX(p.OptionName) AS PackagingName,
    MAX(p.CartonCbm) AS CartonCbm,
    MAX(p.CartonWeightKg) AS CartonWeightKg
FROM dbo.Model m
LEFT JOIN dbo.ModelPiece mp
    ON mp.TenantId = m.TenantId
   AND mp.ModelId = m.ModelId
LEFT JOIN dbo.ModelPackagingOption p
    ON p.TenantId = m.TenantId
   AND p.ModelId = m.ModelId
GROUP BY
    m.TenantId,
    m.Language,
    m.ModelId,
    m.ModelCode,
    m.Name,
    m.Description,
    m.TotalCbm,
    m.TotalWeightKg,
    m.LoadabilityIndex,
    m.Qnt40HC,
    m.PieceCount;
GO

PRINT N'Core model tables/views are present. Existing ERP tables were not dropped.';
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
        TargetSystemId nvarchar(100) NOT NULL CONSTRAINT DF_ai_Capability_TargetSystemId DEFAULT (N'sql'),
        StoredProcedureName sysname NULL,
        StoredProcedure sysname NULL,
        ExecutionMode nvarchar(50) NOT NULL,
        ArgumentContract nvarchar(max) NULL,
        ResultSchema nvarchar(max) NULL,
        AnswerPolicy nvarchar(max) NULL,
        SensitivityPolicy nvarchar(max) NULL,
        RequiredRoles nvarchar(max) NULL,
        AllowedTenants nvarchar(max) NULL,
        AllowMultiCall bit NOT NULL CONSTRAINT DF_ai_Capability_AllowMultiCall DEFAULT (0),
        IsEnabled bit NOT NULL CONSTRAINT DF_ai_Capability_IsEnabled DEFAULT (1),
        IsActive bit NOT NULL CONSTRAINT DF_ai_Capability_IsActive DEFAULT (1),
        Version int NOT NULL CONSTRAINT DF_ai_Capability_Version DEFAULT (1),
        VersionNo int NOT NULL CONSTRAINT DF_ai_Capability_VersionNo DEFAULT (1),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ai_Capability_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ai_Capability_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_ai_Capability_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UX_ai_Capability_CapabilityKey UNIQUE (CapabilityKey),
        CONSTRAINT UX_ai_Capability_FunctionName UNIQUE (FunctionName),
        CONSTRAINT CK_ai_Capability_ArgumentContract_Json CHECK (ArgumentContract IS NULL OR ISJSON(ArgumentContract) = 1),
        CONSTRAINT CK_ai_Capability_ResultSchema_Json CHECK (ResultSchema IS NULL OR ISJSON(ResultSchema) = 1),
        CONSTRAINT CK_ai_Capability_AnswerPolicy_Json CHECK (AnswerPolicy IS NULL OR ISJSON(AnswerPolicy) = 1),
        CONSTRAINT CK_ai_Capability_SensitivityPolicy_Json CHECK (SensitivityPolicy IS NULL OR ISJSON(SensitivityPolicy) = 1),
        CONSTRAINT CK_ai_Capability_RequiredRoles_Json CHECK (RequiredRoles IS NULL OR ISJSON(RequiredRoles) = 1),
        CONSTRAINT CK_ai_Capability_AllowedTenants_Json CHECK (AllowedTenants IS NULL OR ISJSON(AllowedTenants) = 1)
    );
END;
GO

IF OBJECT_ID(N'ai.Capability', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'ai.Capability', N'TargetSystemId') IS NULL
        ALTER TABLE ai.Capability ADD TargetSystemId nvarchar(100) NOT NULL CONSTRAINT DF_ai_Capability_TargetSystemId DEFAULT (N'sql');
    IF COL_LENGTH(N'ai.Capability', N'StoredProcedureName') IS NULL
        ALTER TABLE ai.Capability ADD StoredProcedureName sysname NULL;
    IF COL_LENGTH(N'ai.Capability', N'StoredProcedure') IS NULL
        ALTER TABLE ai.Capability ADD StoredProcedure sysname NULL;
    IF COL_LENGTH(N'ai.Capability', N'IsEnabled') IS NULL
        ALTER TABLE ai.Capability ADD IsEnabled bit NOT NULL CONSTRAINT DF_ai_Capability_IsEnabled DEFAULT (1);
    IF COL_LENGTH(N'ai.Capability', N'IsActive') IS NULL
        ALTER TABLE ai.Capability ADD IsActive bit NOT NULL CONSTRAINT DF_ai_Capability_IsActive DEFAULT (1);
    IF COL_LENGTH(N'ai.Capability', N'Version') IS NULL
        ALTER TABLE ai.Capability ADD Version int NOT NULL CONSTRAINT DF_ai_Capability_Version DEFAULT (1);
    IF COL_LENGTH(N'ai.Capability', N'VersionNo') IS NULL
        ALTER TABLE ai.Capability ADD VersionNo int NOT NULL CONSTRAINT DF_ai_Capability_VersionNo DEFAULT (1);
    IF COL_LENGTH(N'ai.Capability', N'CreatedAtUtc') IS NULL
        ALTER TABLE ai.Capability ADD CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ai_Capability_CreatedAtUtc DEFAULT (SYSUTCDATETIME());
    IF COL_LENGTH(N'ai.Capability', N'UpdatedAtUtc') IS NULL
        ALTER TABLE ai.Capability ADD UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ai_Capability_UpdatedAtUtc DEFAULT (SYSUTCDATETIME());
    IF COL_LENGTH(N'ai.Capability', N'UpdatedAt') IS NULL
        ALTER TABLE ai.Capability ADD UpdatedAt datetime2 NOT NULL CONSTRAINT DF_ai_Capability_UpdatedAt DEFAULT (SYSUTCDATETIME());
    IF COL_LENGTH(N'ai.Capability', N'ArgumentContract') IS NULL
        ALTER TABLE ai.Capability ADD ArgumentContract nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.Capability', N'ResultSchema') IS NULL
        ALTER TABLE ai.Capability ADD ResultSchema nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.Capability', N'AnswerPolicy') IS NULL
        ALTER TABLE ai.Capability ADD AnswerPolicy nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.Capability', N'SensitivityPolicy') IS NULL
        ALTER TABLE ai.Capability ADD SensitivityPolicy nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.Capability', N'RequiredRoles') IS NULL
        ALTER TABLE ai.Capability ADD RequiredRoles nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.Capability', N'AllowedTenants') IS NULL
        ALTER TABLE ai.Capability ADD AllowedTenants nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.Capability', N'AllowMultiCall') IS NULL
        ALTER TABLE ai.Capability ADD AllowMultiCall bit NOT NULL CONSTRAINT DF_ai_Capability_AllowMultiCall DEFAULT (0);
END;
GO

IF OBJECT_ID(N'ai.CapabilityText', N'U') IS NULL
BEGIN
    CREATE TABLE ai.CapabilityText
    (
        CapabilityTextID bigint IDENTITY(1,1) CONSTRAINT PK_ai_CapabilityText PRIMARY KEY,
        CapabilityKey nvarchar(200) NOT NULL,
        Locale nvarchar(20) NOT NULL,
        Name nvarchar(300) NULL,
        ShortName nvarchar(300) NULL,
        Description nvarchar(max) NOT NULL,
        NegativeDescription nvarchar(max) NULL,
        UseWhen nvarchar(max) NULL,
        DoNotUseWhen nvarchar(max) NULL,
        AliasesJson nvarchar(max) NULL,
        IsDefault bit NOT NULL CONSTRAINT DF_ai_CapabilityText_IsDefault DEFAULT (0),
        BusinessNotes nvarchar(max) NULL,
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_ai_CapabilityText_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ai_CapabilityText_Capability FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey),
        CONSTRAINT UX_ai_CapabilityText_KeyLocale UNIQUE (CapabilityKey, Locale),
        CONSTRAINT CK_ai_CapabilityText_AliasesJson CHECK (AliasesJson IS NULL OR ISJSON(AliasesJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'ai.CapabilityText', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'ai.CapabilityText', N'Name') IS NULL
        ALTER TABLE ai.CapabilityText ADD Name nvarchar(300) NULL;
    IF COL_LENGTH(N'ai.CapabilityText', N'ShortName') IS NULL
        ALTER TABLE ai.CapabilityText ADD ShortName nvarchar(300) NULL;
    IF COL_LENGTH(N'ai.CapabilityText', N'NegativeDescription') IS NULL
        ALTER TABLE ai.CapabilityText ADD NegativeDescription nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.CapabilityText', N'UseWhen') IS NULL
        ALTER TABLE ai.CapabilityText ADD UseWhen nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.CapabilityText', N'DoNotUseWhen') IS NULL
        ALTER TABLE ai.CapabilityText ADD DoNotUseWhen nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.CapabilityText', N'AliasesJson') IS NULL
        ALTER TABLE ai.CapabilityText ADD AliasesJson nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.CapabilityText', N'IsDefault') IS NULL
        ALTER TABLE ai.CapabilityText ADD IsDefault bit NOT NULL CONSTRAINT DF_ai_CapabilityText_IsDefault DEFAULT (0);
END;
GO

IF OBJECT_ID(N'ai.CapabilityArgument', N'U') IS NULL
BEGIN
    CREATE TABLE ai.CapabilityArgument
    (
        ArgumentID bigint IDENTITY(1,1) CONSTRAINT PK_ai_CapabilityArgument PRIMARY KEY,
        CapabilityKey nvarchar(200) NOT NULL,
        ArgumentName nvarchar(128) NOT NULL,
        ModelFacingName nvarchar(128) NULL,
        SqlParameterName nvarchar(128) NULL,
        ProcParameterName nvarchar(128) NOT NULL,
        Type nvarchar(50) NULL,
        DataType nvarchar(50) NOT NULL,
        IsRequired bit NOT NULL,
        EnumJson nvarchar(max) NULL,
        Format nvarchar(100) NULL,
        RegexPattern nvarchar(400) NULL,
        MinLength int NULL,
        MaxLength int NULL,
        MinValue decimal(18,4) NULL,
        MaxValue decimal(18,4) NULL,
        DefaultSource nvarchar(100) NULL,
        ValidationRule nvarchar(max) NULL,
        ClarificationPolicy nvarchar(max) NULL,
        SortOrder int NOT NULL CONSTRAINT DF_ai_CapabilityArgument_SortOrder DEFAULT (0),
        DisplayOrder int NOT NULL CONSTRAINT DF_ai_CapabilityArgument_DisplayOrder DEFAULT (0),
        IsActive bit NOT NULL CONSTRAINT DF_ai_CapabilityArgument_IsActive DEFAULT (1),
        CONSTRAINT FK_ai_CapabilityArgument_Capability FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey),
        CONSTRAINT UX_ai_CapabilityArgument_KeyName UNIQUE (CapabilityKey, ArgumentName),
        CONSTRAINT CK_ai_CapabilityArgument_EnumJson CHECK (EnumJson IS NULL OR ISJSON(EnumJson) = 1),
        CONSTRAINT CK_ai_CapabilityArgument_ValidationRule_Json CHECK (ValidationRule IS NULL OR ISJSON(ValidationRule) = 1),
        CONSTRAINT CK_ai_CapabilityArgument_ClarificationPolicy_Json CHECK (ClarificationPolicy IS NULL OR ISJSON(ClarificationPolicy) = 1)
    );
END;
GO

IF OBJECT_ID(N'ai.CapabilityArgument', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'ai.CapabilityArgument', N'ModelFacingName') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD ModelFacingName nvarchar(128) NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'SqlParameterName') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD SqlParameterName nvarchar(128) NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'ProcParameterName') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD ProcParameterName nvarchar(128) NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'Type') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD Type nvarchar(50) NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'DataType') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD DataType nvarchar(50) NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'EnumJson') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD EnumJson nvarchar(max) NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'Format') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD Format nvarchar(100) NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'RegexPattern') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD RegexPattern nvarchar(400) NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'MinLength') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD MinLength int NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'MaxLength') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD MaxLength int NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'MinValue') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD MinValue decimal(18,4) NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'MaxValue') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD MaxValue decimal(18,4) NULL;
    IF COL_LENGTH(N'ai.CapabilityArgument', N'SortOrder') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD SortOrder int NOT NULL CONSTRAINT DF_ai_CapabilityArgument_SortOrder DEFAULT (0);
    IF COL_LENGTH(N'ai.CapabilityArgument', N'DisplayOrder') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD DisplayOrder int NOT NULL CONSTRAINT DF_ai_CapabilityArgument_DisplayOrder DEFAULT (0);
    IF COL_LENGTH(N'ai.CapabilityArgument', N'IsActive') IS NULL
        ALTER TABLE ai.CapabilityArgument ADD IsActive bit NOT NULL CONSTRAINT DF_ai_CapabilityArgument_IsActive DEFAULT (1);
END;
GO

IF OBJECT_ID(N'ai.CapabilityArgumentText', N'U') IS NULL
BEGIN
    CREATE TABLE ai.CapabilityArgumentText
    (
        ArgumentTextID bigint IDENTITY(1,1) CONSTRAINT PK_ai_CapabilityArgumentText PRIMARY KEY,
        CapabilityKey nvarchar(200) NOT NULL,
        ArgumentName nvarchar(128) NOT NULL,
        Locale nvarchar(20) NOT NULL,
        Description nvarchar(max) NOT NULL,
        AliasesJson nvarchar(max) NULL,
        ClarificationQuestion nvarchar(max) NULL,
        ExamplesJson nvarchar(max) NULL,
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_ai_CapabilityArgumentText_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ai_CapabilityArgumentText_CapabilityArgument FOREIGN KEY (CapabilityKey, ArgumentName) REFERENCES ai.CapabilityArgument(CapabilityKey, ArgumentName),
        CONSTRAINT UX_ai_CapabilityArgumentText_KeyNameLocale UNIQUE (CapabilityKey, ArgumentName, Locale),
        CONSTRAINT CK_ai_CapabilityArgumentText_AliasesJson CHECK (AliasesJson IS NULL OR ISJSON(AliasesJson) = 1),
        CONSTRAINT CK_ai_CapabilityArgumentText_ExamplesJson CHECK (ExamplesJson IS NULL OR ISJSON(ExamplesJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'ai.CapabilityResultSchema', N'U') IS NULL
BEGIN
    CREATE TABLE ai.CapabilityResultSchema
    (
        CapabilityKey nvarchar(200) NOT NULL CONSTRAINT PK_ai_CapabilityResultSchema PRIMARY KEY,
        SchemaJson nvarchar(max) NOT NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ai_CapabilityResultSchema_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ai_CapabilityResultSchema_Capability FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey),
        CONSTRAINT CK_ai_CapabilityResultSchema_SchemaJson CHECK (ISJSON(SchemaJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'ai.CapabilityAnswerPolicy', N'U') IS NULL
BEGIN
    CREATE TABLE ai.CapabilityAnswerPolicy
    (
        CapabilityKey nvarchar(200) NOT NULL CONSTRAINT PK_ai_CapabilityAnswerPolicy PRIMARY KEY,
        PolicyJson nvarchar(max) NOT NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ai_CapabilityAnswerPolicy_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ai_CapabilityAnswerPolicy_Capability FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey),
        CONSTRAINT CK_ai_CapabilityAnswerPolicy_PolicyJson CHECK (ISJSON(PolicyJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'ai.CapabilitySensitivityPolicy', N'U') IS NULL
BEGIN
    CREATE TABLE ai.CapabilitySensitivityPolicy
    (
        CapabilityKey nvarchar(200) NOT NULL CONSTRAINT PK_ai_CapabilitySensitivityPolicy PRIMARY KEY,
        PolicyJson nvarchar(max) NOT NULL,
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ai_CapabilitySensitivityPolicy_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ai_CapabilitySensitivityPolicy_Capability FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey),
        CONSTRAINT CK_ai_CapabilitySensitivityPolicy_PolicyJson CHECK (ISJSON(PolicyJson) = 1)
    );
END;
GO

IF OBJECT_ID(N'ai.CapabilityExample', N'U') IS NULL
BEGIN
    CREATE TABLE ai.CapabilityExample
    (
        CapabilityExampleID bigint IDENTITY(1,1) CONSTRAINT PK_ai_CapabilityExample PRIMARY KEY,
        CapabilityKey nvarchar(200) NOT NULL,
        Locale nvarchar(20) NOT NULL,
        Utterance nvarchar(max) NOT NULL,
        ArgumentsJson nvarchar(max) NULL,
        SortOrder int NOT NULL CONSTRAINT DF_ai_CapabilityExample_SortOrder DEFAULT (0),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ai_CapabilityExample_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ai_CapabilityExample_Capability FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey),
        CONSTRAINT CK_ai_CapabilityExample_ArgumentsJson CHECK (ArgumentsJson IS NULL OR ISJSON(ArgumentsJson) = 1)
    );
END;
GO

PRINT N'SQL-backed capability catalog tables are present.';
GO
