SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_get_overview
    @TenantId nvarchar(50),
    @Language nvarchar(10) = NULL,
    @ModelId int
AS
BEGIN
    SET NOCOUNT ON;

    IF @ModelId IS NULL
    BEGIN
        RAISERROR('modelId is required.', 16, 1);
        RETURN;
    END;

    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();

    DECLARE @RowCount int = (
        SELECT COUNT(1)
        FROM dbo.v_Model_Overview
        WHERE TenantId = @TenantId
          AND ModelId = @ModelId
    );

    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS rowCount,
                    @ModelId AS modelId,
                    NULLIF(LTRIM(RTRIM(@Language)), '') AS language
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            columns = (
                SELECT
                    [name],
                    [type],
                    [descriptionKey]
                FROM (VALUES
                    ('ModelId', 'int', NULL),
                    ('ModelCode', 'nvarchar(4)', NULL),
                    ('Name', 'nvarchar(255)', 'Model.Name'),
                    ('Season', 'nvarchar(9)', NULL),
                    ('PieceCount', 'int', 'Model.PieceCount'),
                    ('DefaultCbm', 'decimal(18,4)', 'Model.Cbm'),
                    ('Qnt40HC', 'int', 'Model.Qnt40HC'),
                    ('BoxInSet', 'int', 'Packaging.BoxInSet'),
                    ('HasFsc', 'bit', NULL),
                    ('HasRcs', 'bit', NULL)
                ) AS columns([name], [type], [descriptionKey])
                FOR JSON PATH
            ),
            rows = (
                SELECT
                    ModelId,
                    ModelCode,
                    Name,
                    Season,
                    PieceCount,
                    DefaultCbm,
                    Qnt40HC,
                    BoxInSet,
                    HasFsc,
                    HasRcs
                FROM dbo.v_Model_Overview
                WHERE TenantId = @TenantId
                  AND ModelId = @ModelId
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_get_pieces
    @TenantId nvarchar(50),
    @Language nvarchar(10) = NULL,
    @ModelId int
AS
BEGIN
    SET NOCOUNT ON;

    IF @ModelId IS NULL
    BEGIN
        RAISERROR('modelId is required.', 16, 1);
        RETURN;
    END;

    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();

    DECLARE @RowCount int = (
        SELECT COUNT(1)
        FROM dbo.v_Model_Pieces
        WHERE TenantId = @TenantId
          AND ParentModelId = @ModelId
    );

    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS rowCount,
                    @ModelId AS modelId,
                    NULLIF(LTRIM(RTRIM(@Language)), '') AS language
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            columns = (
                SELECT
                    [name],
                    [type],
                    [descriptionKey]
                FROM (VALUES
                    ('ModelPieceId', 'int', NULL),
                    ('ChildModelId', 'int', NULL),
                    ('ChildModelCode', 'nvarchar(4)', NULL),
                    ('ChildModelName', 'nvarchar(255)', 'Model.Name'),
                    ('Quantity', 'int', NULL),
                    ('RowIndex', 'int', NULL)
                ) AS columns([name], [type], [descriptionKey])
                FOR JSON PATH
            ),
            rows = (
                SELECT
                    ModelPieceId,
                    ChildModelId,
                    ChildModelCode,
                    ChildModelName,
                    Quantity,
                    RowIndex
                FROM dbo.v_Model_Pieces
                WHERE TenantId = @TenantId
                  AND ParentModelId = @ModelId
                ORDER BY ISNULL(RowIndex, 9999), ModelPieceId
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_get_packaging
    @TenantId nvarchar(50),
    @Language nvarchar(10) = NULL,
    @ModelId int
AS
BEGIN
    SET NOCOUNT ON;

    IF @ModelId IS NULL
    BEGIN
        RAISERROR('modelId is required.', 16, 1);
        RETURN;
    END;

    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();

    DECLARE @RowCount int = (
        SELECT COUNT(1)
        FROM dbo.v_Model_Packaging_Default
        WHERE TenantId = @TenantId
          AND ModelId = @ModelId
    );

    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS rowCount,
                    @ModelId AS modelId,
                    NULLIF(LTRIM(RTRIM(@Language)), '') AS language
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            columns = (
                SELECT
                    [name],
                    [type],
                    [descriptionKey]
                FROM (VALUES
                    ('MethodCode', 'nvarchar(3)', NULL),
                    ('BoxInSet', 'int', 'Packaging.BoxInSet'),
                    ('Cbm', 'decimal(18,4)', 'Model.Cbm'),
                    ('Qnt20DC', 'int', NULL),
                    ('Qnt40DC', 'int', NULL),
                    ('Qnt40HC', 'int', 'Model.Qnt40HC'),
                    ('NetWeight', 'decimal(18,4)', NULL),
                    ('GrossWeight', 'decimal(18,4)', NULL),
                    ('CartonBoxDimL', 'varchar(50)', NULL),
                    ('CartonBoxDimW', 'varchar(50)', NULL),
                    ('CartonBoxDimH', 'varchar(50)', NULL),
                    ('PackagingRemark', 'varchar(255)', NULL)
                ) AS columns([name], [type], [descriptionKey])
                FOR JSON PATH
            ),
            rows = (
                SELECT
                    MethodCode,
                    BoxInSet,
                    Cbm,
                    Qnt20DC,
                    Qnt40DC,
                    Qnt40HC,
                    NetWeight,
                    GrossWeight,
                    CartonBoxDimL,
                    CartonBoxDimW,
                    CartonBoxDimH,
                    PackingRemark AS PackagingRemark
                FROM dbo.v_Model_Packaging_Default
                WHERE TenantId = @TenantId
                  AND ModelId = @ModelId
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_get_materials
    @TenantId nvarchar(50),
    @Language nvarchar(10) = NULL,
    @ModelId int
AS
BEGIN
    SET NOCOUNT ON;

    IF @ModelId IS NULL
    BEGIN
        RAISERROR('modelId is required.', 16, 1);
        RETURN;
    END;

    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();

    DECLARE @RowCount int = (
        SELECT COUNT(1)
        FROM dbo.v_Model_Materials
        WHERE TenantId = @TenantId
          AND ModelId = @ModelId
    );

    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS rowCount,
                    @ModelId AS modelId,
                    NULLIF(LTRIM(RTRIM(@Language)), '') AS language
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            columns = (
                SELECT
                    [name],
                    [type],
                    [descriptionKey]
                FROM (VALUES
                    ('ProductWizardSectionID', 'int', NULL),
                    ('ProductWizardSectionNM', 'nvarchar(255)', 'Material.Section'),
                    ('MaterialGroupID', 'int', NULL),
                    ('IsFSCEnabled', 'bit', NULL),
                    ('IsRCSEnabled', 'bit', NULL)
                ) AS columns([name], [type], [descriptionKey])
                FOR JSON PATH
            ),
            rows = (
                SELECT
                    ProductWizardSectionID,
                    ProductWizardSectionNM,
                    MaterialGroupID,
                    IsFSCEnabled,
                    IsRCSEnabled
                FROM dbo.v_Model_Materials
                WHERE TenantId = @TenantId
                  AND ModelId = @ModelId
                ORDER BY ISNULL(DisplayOrder, 9999), ProductWizardSectionNM
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_compare_models
    @TenantId nvarchar(50),
    @Language nvarchar(10) = NULL,
    @ModelIdsJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Ids TABLE (ModelId int NOT NULL);

    IF @ModelIdsJson IS NOT NULL AND LTRIM(RTRIM(@ModelIdsJson)) <> ''
    BEGIN
        INSERT INTO @Ids (ModelId)
        SELECT DISTINCT TRY_CAST([value] AS int)
        FROM OPENJSON(@ModelIdsJson)
        WHERE TRY_CAST([value] AS int) IS NOT NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM @Ids)
    BEGIN
        RAISERROR('modelIds are required.', 16, 1);
        RETURN;
    END;

    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();

    ;WITH Selected AS
    (
        SELECT
            o.TenantId,
            o.ModelId,
            o.ModelCode,
            o.Name,
            o.Season,
            o.DefaultCbm,
            o.Qnt40HC,
            o.BoxInSet,
            o.HasFsc,
            o.HasRcs
        FROM dbo.v_Model_Overview AS o
        INNER JOIN @Ids AS ids
            ON ids.ModelId = o.ModelId
        WHERE o.TenantId = @TenantId
    )
    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    (SELECT COUNT(1) FROM Selected) AS rowCount,
                    NULLIF(LTRIM(RTRIM(@Language)), '') AS language
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            columns = (
                SELECT
                    [name],
                    [type],
                    [descriptionKey]
                FROM (VALUES
                    ('ModelId', 'int', NULL),
                    ('ModelCode', 'nvarchar(4)', NULL),
                    ('Name', 'nvarchar(255)', 'Model.Name'),
                    ('Season', 'nvarchar(9)', NULL),
                    ('DefaultCbm', 'decimal(18,4)', 'Model.Cbm'),
                    ('Qnt40HC', 'int', 'Model.Qnt40HC'),
                    ('BoxInSet', 'int', 'Packaging.BoxInSet'),
                    ('CbmPer40HC', 'decimal(18,4)', 'Model.CbmPer40HC'),
                    ('CbmDeltaFromMax', 'decimal(18,4)', 'Model.CbmDelta'),
                    ('HasFsc', 'bit', NULL),
                    ('HasRcs', 'bit', NULL)
                ) AS columns([name], [type], [descriptionKey])
                FOR JSON PATH
            ),
            rows = (
                SELECT
                    s.ModelId,
                    s.ModelCode,
                    s.Name,
                    s.Season,
                    s.DefaultCbm,
                    s.Qnt40HC,
                    s.BoxInSet,
                    CASE WHEN s.Qnt40HC IS NULL OR s.Qnt40HC = 0 THEN NULL ELSE s.DefaultCbm / NULLIF(s.Qnt40HC, 0) END AS CbmPer40HC,
                    s.DefaultCbm - (SELECT MAX(DefaultCbm) FROM Selected) AS CbmDeltaFromMax,
                    s.HasFsc,
                    s.HasRcs
                FROM Selected AS s
                ORDER BY s.ModelId
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ai_model_count
    @TenantId nvarchar(50),
    @Language nvarchar(10) = NULL,
    @Season nvarchar(9) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @GeneratedAtUtc datetime2(3) = sysutcdatetime();
    DECLARE @SeasonFilter nvarchar(9) = NULLIF(LTRIM(RTRIM(@Season)), '');

    DECLARE @RowCount int = (
        SELECT COUNT(DISTINCT Season)
        FROM dbo.v_Model_Overview
        WHERE TenantId = @TenantId
          AND (@SeasonFilter IS NULL OR Season = @SeasonFilter)
    );

    SELECT (
        SELECT
            meta = (
                SELECT
                    @TenantId AS tenantId,
                    @GeneratedAtUtc AS generatedAtUtc,
                    @RowCount AS rowCount,
                    NULLIF(LTRIM(RTRIM(@Language)), '') AS language
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
            ),
            columns = (
                SELECT
                    [name],
                    [type],
                    [descriptionKey]
                FROM (VALUES
                    ('Season', 'nvarchar(9)', NULL),
                    ('ModelCount', 'int', NULL)
                ) AS columns([name], [type], [descriptionKey])
                FOR JSON PATH
            ),
            rows = (
                SELECT
                    Season,
                    COUNT(1) AS ModelCount
                FROM dbo.v_Model_Overview
                WHERE TenantId = @TenantId
                  AND (@SeasonFilter IS NULL OR Season = @SeasonFilter)
                GROUP BY Season
                ORDER BY Season
                FOR JSON PATH
            )
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS ResultJson;
END;
GO
