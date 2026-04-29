SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT N'Optional seed: inserts clearly marked test-only model data for the local no-auth default tenant only when absent.';
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Model WHERE TenantId = N'default')
BEGIN
    DECLARE @TenantId nvarchar(50) = 'default';
    DECLARE @Lang nvarchar(10) = 'en';
    DECLARE @MatWood int, @MatSteel int, @MatFabric int, @MatFoam int;
    DECLARE @Chair int, @Table int, @Set int;

    INSERT INTO dbo.Material (TenantId, Language, MaterialCode, Name, Category, Section, Description, DensityKgPerM3)
    VALUES
        (@TenantId, @Lang, 'MAT-OAK', 'Oak Wood', 'Wood', 'Frame', 'test-only seeded model data; not ERP production data', 750),
        (@TenantId, @Lang, 'MAT-STL', 'Stainless Steel', 'Metal', 'Hardware', 'test-only seeded model data; not ERP production data', 7850),
        (@TenantId, @Lang, 'MAT-LIN', 'Linen Fabric', 'Fabric', 'Upholstery', 'test-only seeded model data; not ERP production data', 0),
        (@TenantId, @Lang, 'MAT-FOAM', 'High Density Foam', 'Foam', 'Upholstery', 'test-only seeded model data; not ERP production data', 50);

    SELECT @MatWood = MaterialId FROM dbo.Material WHERE TenantId = @TenantId AND MaterialCode = 'MAT-OAK';
    SELECT @MatSteel = MaterialId FROM dbo.Material WHERE TenantId = @TenantId AND MaterialCode = 'MAT-STL';
    SELECT @MatFabric = MaterialId FROM dbo.Material WHERE TenantId = @TenantId AND MaterialCode = 'MAT-LIN';
    SELECT @MatFoam = MaterialId FROM dbo.Material WHERE TenantId = @TenantId AND MaterialCode = 'MAT-FOAM';

    INSERT INTO dbo.Model (TenantId, Language, ModelCode, Name, Description, TotalCbm, TotalWeightKg, PieceCount, Qnt40HC)
    VALUES (@TenantId, @Lang, 'ABC', 'Acceptance Chair', 'test-only seeded model data; not ERP production data', 0.1500, 8.5000, 1, 450);
    SET @Chair = SCOPE_IDENTITY();

    INSERT INTO dbo.ModelMaterial (TenantId, ModelId, MaterialId, Section, Quantity, Unit, WeightKg)
    VALUES
        (@TenantId, @Chair, @MatWood, 'Frame', 0.0200, 'm3', 5.0000),
        (@TenantId, @Chair, @MatFabric, 'Seat', 1.5000, 'm2', 0.5000),
        (@TenantId, @Chair, @MatFoam, 'Seat', 0.0500, 'm3', 1.0000);

    INSERT INTO dbo.ModelPackagingOption (TenantId, ModelId, OptionName, PackagingType, UnitsPerCarton, CartonCbm, CartonWeightKg, Qnt40HC)
    VALUES (@TenantId, @Chair, 'Standard Box', 'Carton', 2, 0.3200, 18.0000, 450);

    INSERT INTO dbo.Model (TenantId, Language, ModelCode, Name, Description, TotalCbm, TotalWeightKg, PieceCount, Qnt40HC)
    VALUES (@TenantId, @Lang, 'XYZ', 'Acceptance Table', 'test-only seeded model data; not ERP production data', 0.4500, 45.0000, 1, 140);
    SET @Table = SCOPE_IDENTITY();

    INSERT INTO dbo.ModelMaterial (TenantId, ModelId, MaterialId, Section, Quantity, Unit, WeightKg)
    VALUES
        (@TenantId, @Table, @MatWood, 'Top', 0.1500, 'm3', 40.0000),
        (@TenantId, @Table, @MatSteel, 'Hardware', 2.0000, 'kg', 2.0000);

    INSERT INTO dbo.ModelPackagingOption (TenantId, ModelId, OptionName, PackagingType, UnitsPerCarton, CartonCbm, CartonWeightKg, Qnt40HC)
    VALUES (@TenantId, @Table, 'Flat Pack', 'Carton', 1, 0.4800, 48.0000, 140);

    INSERT INTO dbo.Model (TenantId, Language, ModelCode, Name, Description, TotalCbm, TotalWeightKg, PieceCount, Qnt40HC)
    VALUES (@TenantId, @Lang, 'SET-DINING-001', 'Acceptance Dining Set', 'test-only seeded model data; not ERP production data', 1.0500, 79.0000, 5, 60);
    SET @Set = SCOPE_IDENTITY();

    INSERT INTO dbo.ModelPiece (TenantId, ModelId, PieceName, Quantity, ChildModelId, Sequence)
    VALUES
        (@TenantId, @Set, 'Dining Table', 1, @Table, 1),
        (@TenantId, @Set, 'Dining Chair', 4, @Chair, 2);

    INSERT INTO dbo.ModelPackagingOption (TenantId, ModelId, OptionName, PackagingType, UnitsPerCarton, CartonCbm, CartonWeightKg, Qnt40HC)
    VALUES (@TenantId, @Set, 'Set Consolidation', 'Carton', 1, 1.1500, 85.0000, 60);

    PRINT N'Inserted test-only seeded model data for tenant default; not ERP production data.';
END
ELSE
BEGIN
    PRINT N'Model rows already exist for tenant default. Optional test seed skipped; existing data treated as local source data.';
END;
GO
