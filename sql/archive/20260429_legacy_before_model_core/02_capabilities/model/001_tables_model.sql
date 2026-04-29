-- DEMO ONLY: create table dbo.__DemoModelSchemaEnabled to allow demo schema creation.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Model', 'U') IS NULL
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

IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Model_Tenant_Language' AND object_id = OBJECT_ID('dbo.Model'))
BEGIN
    CREATE INDEX IX_Model_Tenant_Language ON dbo.Model (TenantId, Language);
END;
GO

IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Model_Tenant_Code' AND object_id = OBJECT_ID('dbo.Model'))
BEGIN
    CREATE INDEX IX_Model_Tenant_Code ON dbo.Model (TenantId, ModelCode);
END;
GO

IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.ModelPiece', 'U') IS NULL
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

IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ModelPiece_Tenant_Model' AND object_id = OBJECT_ID('dbo.ModelPiece'))
BEGIN
    CREATE INDEX IX_ModelPiece_Tenant_Model ON dbo.ModelPiece (TenantId, ModelId);
END;
GO

IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Material', 'U') IS NULL
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

IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Material_Tenant_Language' AND object_id = OBJECT_ID('dbo.Material'))
BEGIN
    CREATE INDEX IX_Material_Tenant_Language ON dbo.Material (TenantId, Language);
END;
GO

IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.ModelMaterial', 'U') IS NULL
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

IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ModelMaterial_Tenant_Model' AND object_id = OBJECT_ID('dbo.ModelMaterial'))
BEGIN
    CREATE INDEX IX_ModelMaterial_Tenant_Model ON dbo.ModelMaterial (TenantId, ModelId);
END;
GO

IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.ModelPackagingOption', 'U') IS NULL
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

IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ModelPackagingOption_Tenant_Model' AND object_id = OBJECT_ID('dbo.ModelPackagingOption'))
BEGIN
    CREATE INDEX IX_ModelPackagingOption_Tenant_Model ON dbo.ModelPackagingOption (TenantId, ModelId);
END;
GO
