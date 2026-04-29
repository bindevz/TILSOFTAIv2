SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Enable Demo Mode
IF OBJECT_ID('dbo.__DemoModelSchemaEnabled', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.__DemoModelSchemaEnabled (Id int);
    PRINT 'Enabled Demo Model Schema.';
END;
GO

-- 2. Create Tables (Idempotent copy from 001 to ensure existence)
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
    CREATE INDEX IX_Model_Tenant_Language ON dbo.Model (TenantId, Language);
    PRINT 'Created dbo.Model';
END;

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
     CREATE INDEX IX_ModelPiece_Tenant_Model ON dbo.ModelPiece (TenantId, ModelId);
    PRINT 'Created dbo.ModelPiece';
END;

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
    PRINT 'Created dbo.Material';
END;

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
    PRINT 'Created dbo.ModelMaterial';
END;

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
    PRINT 'Created dbo.ModelPackagingOption';
END;
GO

-- 3. Seed Data
-- Only seed if empty for this tenant
IF NOT EXISTS (SELECT 1 FROM dbo.Model WHERE TenantId = 'demo')
BEGIN
    PRINT 'Seeding Demo Model Data...';

    DECLARE @TenantId nvarchar(50) = 'demo';
    DECLARE @Lang nvarchar(10) = 'en';
    
    -- Variables for IDs
    DECLARE @Mat_Wood int, @Mat_Metal int, @Mat_Fabric int, @Mat_Foam int;
    DECLARE @Mod_Chair int, @Mod_Table int, @Mod_Set int;

    -- Materials
    INSERT INTO dbo.Material (TenantId, Language, MaterialCode, Name, Category, DensityKgPerM3) VALUES
    (@TenantId, @Lang, 'MAT-OAK', 'Oak Wood', 'Wood', 750),
    (@TenantId, @Lang, 'MAT-STL', 'Stainless Steel', 'Metal', 7850),
    (@TenantId, @Lang, 'MAT-LIN', 'Linen Fabric', 'Fabric', 0),
    (@TenantId, @Lang, 'MAT-FOAM', 'High Density Foam', 'Foam', 50);

    SELECT @Mat_Wood = MaterialId FROM dbo.Material WHERE MaterialCode = 'MAT-OAK' AND TenantId = @TenantId;
    SELECT @Mat_Metal = MaterialId FROM dbo.Material WHERE MaterialCode = 'MAT-STL' AND TenantId = @TenantId;
    SELECT @Mat_Fabric = MaterialId FROM dbo.Material WHERE MaterialCode = 'MAT-LIN' AND TenantId = @TenantId;
    SELECT @Mat_Foam = MaterialId FROM dbo.Material WHERE MaterialCode = 'MAT-FOAM' AND TenantId = @TenantId;

    -- Model 1: Chair
    INSERT INTO dbo.Model (TenantId, Language, ModelCode, Name, TotalCbm, TotalWeightKg, PieceCount, Qnt40HC)
    VALUES (@TenantId, @Lang, 'CHAIR-001', 'Modern Dining Chair', 0.15, 8.5, 1, 450);
    SET @Mod_Chair = SCOPE_IDENTITY();

    INSERT INTO dbo.ModelMaterial (TenantId, ModelId, MaterialId, Quantity, Unit, WeightKg) VALUES
    (@TenantId, @Mod_Chair, @Mat_Wood, 0.02, 'm3', 5.0),
    (@TenantId, @Mod_Chair, @Mat_Fabric, 1.5, 'm2', 0.5),
    (@TenantId, @Mod_Chair, @Mat_Foam, 0.05, 'm3', 1.0);

    INSERT INTO dbo.ModelPackagingOption (TenantId, ModelId, OptionName, UnitsPerCarton, CartonCbm, CartonWeightKg)
    VALUES (@TenantId, @Mod_Chair, 'Standard Box', 2, 0.32, 18.0);

    -- Model 2: Table
    INSERT INTO dbo.Model (TenantId, Language, ModelCode, Name, TotalCbm, TotalWeightKg, PieceCount, Qnt40HC)
    VALUES (@TenantId, @Lang, 'TABLE-001', 'Large Oak Dining Table', 0.45, 45.0, 1, 140);
    SET @Mod_Table = SCOPE_IDENTITY();

    INSERT INTO dbo.ModelMaterial (TenantId, ModelId, MaterialId, Quantity, Unit, WeightKg) VALUES
    (@TenantId, @Mod_Table, @Mat_Wood, 0.15, 'm3', 40.0),
    (@TenantId, @Mod_Table, @Mat_Metal, 2.0, 'kg', 2.0);

    INSERT INTO dbo.ModelPackagingOption (TenantId, ModelId, OptionName, UnitsPerCarton, CartonCbm, CartonWeightKg)
    VALUES (@TenantId, @Mod_Table, 'Flat Pack', 1, 0.48, 48.0);

    -- Model 3: Dining Set (1 Table + 4 Chairs)
    INSERT INTO dbo.Model (TenantId, Language, ModelCode, Name, TotalCbm, TotalWeightKg, PieceCount, Qnt40HC)
    VALUES (@TenantId, @Lang, 'SET-DINING-001', '5-Piece Dining Set', 1.05, 79.0, 5, 60); -- CBM = 0.45 + 4*0.15 = 1.05
    SET @Mod_Set = SCOPE_IDENTITY();

    -- Pieces (Hierarchy)
    INSERT INTO dbo.ModelPiece (TenantId, ModelId, PieceName, Quantity, ChildModelId, Sequence) VALUES
    (@TenantId, @Mod_Set, 'Dining Table', 1, @Mod_Table, 1),
    (@TenantId, @Mod_Set, 'Dining Chair', 4, @Mod_Chair, 2);

    INSERT INTO dbo.ModelPackagingOption (TenantId, ModelId, OptionName, UnitsPerCarton, CartonCbm, CartonWeightKg)
    VALUES (@TenantId, @Mod_Set, 'Set Consolidation', 1, 1.15, 85.0);
    
    PRINT 'Seed data inserted successfully.';
END
ELSE
BEGIN
    PRINT 'Demo data already exists for tenant ''demo''. Skipping seed.';
END
GO
