/*Note: đây là bảng thông tin model*/
CREATE TABLE [dbo].[Model](
	[ModelID] [int] IDENTITY(1,1) NOT NULL,
	[ModelUD] [varchar](4) NULL,
	[ModelNM] [varchar](255) NULL,
	[Season] [varchar](9) NULL,
	[ProductTypeID] [int] NULL,
	[RangeName] [varchar](50) NULL,
	[ClientID] [int] NULL,
 CONSTRAINT [PK_Model] PRIMARY KEY CLUSTERED 
(
	[ModelID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [ModelUDUnique] UNIQUE NONCLUSTERED 
(
	[ModelUD] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[Model]  WITH CHECK ADD  CONSTRAINT [FK__Model__Requested__3A9DE7CF] FOREIGN KEY([RequestedBy])
REFERENCES [dbo].[Employee] ([EmployeeID])
GO

ALTER TABLE [dbo].[Model]  WITH CHECK ADD  CONSTRAINT [FK__Model__UpgradeFr__38B59F5D] FOREIGN KEY([UpgradeFromModelID])
REFERENCES [dbo].[Model] ([ModelID])
GO

ALTER TABLE [dbo].[Model] CHECK CONSTRAINT [FK__Model__UpgradeFr__38B59F5D]
GO

ALTER TABLE [dbo].[Model]  WITH CHECK ADD  CONSTRAINT [FK_Model_Client] FOREIGN KEY([ClientID])
REFERENCES [dbo].[Client] ([ClientID])
GO

ALTER TABLE [dbo].[Model] CHECK CONSTRAINT [FK_Model_Client]
GO

/*Bảng này là Piece của model (nếu model là SET thì có nhiều piece*/
CREATE TABLE [dbo].[ModelPiece](
	[ModelPieceID] [int] IDENTITY(1,1) NOT NULL,
	[ModelID] [int] NULL, -- Model cha
	[PieceModelID] [int] NULL, --Model con hay gọi là Piece của Model.
	[Quantity] [int] NULL, --Số lượng Piece của model
	[RowIndex] [int] NULL,
 CONSTRAINT [PK_ModelPiece] PRIMARY KEY CLUSTERED 
(
	[ModelPieceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ModelPiece]  WITH CHECK ADD  CONSTRAINT [FK_ModelPiece_Model] FOREIGN KEY([PieceModelID])
REFERENCES [dbo].[Model] ([ModelID])
GO

ALTER TABLE [dbo].[ModelPiece] CHECK CONSTRAINT [FK_ModelPiece_Model]
GO

ALTER TABLE [dbo].[ModelPiece]  WITH CHECK ADD  CONSTRAINT [FK_ModelPiece_ModelPiece] FOREIGN KEY([ModelID])
REFERENCES [dbo].[Model] ([ModelID])
ON DELETE CASCADE
GO

ALTER TABLE [dbo].[ModelPiece] CHECK CONSTRAINT [FK_ModelPiece_ModelPiece]

/*định nghĩa phuong pháp đóng gói*/
CREATE TABLE [dbo].[ModelPackagingMethodOption](
	[ModelPackagingMethodOptionID] [int] IDENTITY(1,1) NOT NULL,
	[ModelID] [int] NULL,
	[PackagingMethodID] [int] NULL,
	[IsDefault] [bit] NULL,
	[Description] [varchar](255) NULL,
	[CartonBoxDimL] [varchar](50) NULL,
	[CartonBoxDimW] [varchar](50) NULL,
	[CartonBoxDimH] [varchar](50) NULL,
	[UpdatedBy] [int] NULL,
	[UpdatedDate] [datetime] NULL,
	[IsConfirmed] [bit] NULL,
	[ConfirmedBy] [int] NULL,
	[ConfirmedDate] [datetime] NULL,
	[Qnt20DC] [int] NULL,
	[Qnt40DC] [int] NULL,
	[Qnt40HC] [int] NULL,
	[QntInBox] [int] NULL,
	[NetWeight] [varchar](50) NULL,
	[GrossWeight] [varchar](50) NULL,
	[CBM] [varchar](100) NULL,
	[PackingRemark] [varchar](255) NULL,
	[LoadAbilityTypeID] [int] NULL,
	[ClientSpecialPackagingMethodID] [int] NULL,
	[BoxInSet] [int] NULL,
	[MethodCode] [varchar](3) NULL,
	[FileUD] [varchar](50) NULL,
	[ModelPackagingMethodOptionUD] [varchar](3) NULL,
	[EstLoadAbility40HC] [int] NULL,
	[EstLoadAbilityUpdatedBy] [int] NULL,
	[EstLoadAbilityUpdatedDate] [datetime] NULL,
	[DeadLine] [datetime] NULL,
	[DeadLineRemark] [nvarchar](500) NULL,
	[ModelPackagingMethodStatus] [varchar](50) NULL,
	[PackagingMethodOptionUD] [varchar](1) NULL,
	[PackagingMethodDescription] [varchar](255) NULL,
 CONSTRAINT [PK_ModelPackagingMethodOption_1] PRIMARY KEY CLUSTERED 
(
	[ModelPackagingMethodOptionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ModelPackagingMethodOption]  WITH CHECK ADD  CONSTRAINT [FK_ModelPackagingMethodOption_Model] FOREIGN KEY([ModelID])
REFERENCES [dbo].[Model] ([ModelID])
ON DELETE CASCADE
GO

ALTER TABLE [dbo].[ModelPackagingMethodOption] CHECK CONSTRAINT [FK_ModelPackagingMethodOption_Model]
GO

ALTER TABLE [dbo].[ModelPackagingMethodOption]  WITH CHECK ADD  CONSTRAINT [FK_ModelPackagingMethodOption_PackagingMethod] FOREIGN KEY([PackagingMethodID])
REFERENCES [dbo].[PackagingMethod] ([PackagingMethodID])
GO

ALTER TABLE [dbo].[ModelPackagingMethodOption] CHECK CONSTRAINT [FK_ModelPackagingMethodOption_PackagingMethod]
GO

/*Bảng này thể hiện là Model được làm từ những thành phần gì*/
CREATE TABLE [dbo].[ModelMaterialConfig](
	[ModelMaterialConfigID] [int] IDENTITY(1,1) NOT NULL,
	[ModelID] [int] NULL,
	[ProductWizardSectionID] [int] NULL,
 CONSTRAINT [PK_ModelMaterialConfig] PRIMARY KEY CLUSTERED 
(
	[ModelMaterialConfigID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ModelMaterialConfig]  WITH CHECK ADD  CONSTRAINT [FK_ModelMaterialConfig_Model] FOREIGN KEY([ModelID])
REFERENCES [dbo].[Model] ([ModelID])
GO

ALTER TABLE [dbo].[ModelMaterialConfig] CHECK CONSTRAINT [FK_ModelMaterialConfig_Model]
GO

/*Bảng này định nghĩa tên hiển thị từ [ModelMaterialConfig]*/
CREATE TABLE [dbo].[ProductWizardSection](
	[ProductWizardSectionID] [int] IDENTITY(1,1) NOT NULL,
	[ProductWizardSectionNM] [nvarchar](255) NULL,
	[ProductWizardSectionNM2] [nvarchar](255) NULL,
	[MaterialGroupID] [int] NULL,
	[DisplayOrder] [int] NULL,
	[ParentID] [int] NULL,
	[IsLimitedOption] [bit] NULL,
	[IsRCSEnabled] [bit] NULL,
	[IsFSCEnabled] [bit] NULL,
	[SectionGroupID] [int] NULL,
 CONSTRAINT [PK_ProductWizardSection] PRIMARY KEY CLUSTERED 
(
	[ProductWizardSectionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ProductWizardSection]  WITH CHECK ADD  CONSTRAINT [FK_ProductWizardSection_MaterialGroup1] FOREIGN KEY([MaterialGroupID])
REFERENCES [dbo].[MaterialGroup] ([MaterialGroupID])
GO

ALTER TABLE [dbo].[ProductWizardSection] CHECK CONSTRAINT [FK_ProductWizardSection_MaterialGroup1]
GO

ALTER TABLE [dbo].[ProductWizardSection]  WITH CHECK ADD  CONSTRAINT [FK_ProductWizardSection_ProductWizardSection] FOREIGN KEY([ParentID])
REFERENCES [dbo].[ProductWizardSection] ([ProductWizardSectionID])
GO

ALTER TABLE [dbo].[ProductWizardSection] CHECK CONSTRAINT [FK_ProductWizardSection_ProductWizardSection]

/*Bảng này định nghĩa Mỗi [ProductWizardSection] liên quan tới nhóm Nuyên vật liệu cụ thể*/
CREATE TABLE [dbo].[ProductWizardSectionMaterialGroup](
	[ProductWizardSectionMaterialGroupID] [int] IDENTITY(1,1) NOT NULL,
	[ProductWizardSectionID] [int] NULL,
	[MaterialGroupID] [int] NULL,
 CONSTRAINT [PK_ProductWizardSectionMaterialGroup] PRIMARY KEY CLUSTERED 
(
	[ProductWizardSectionMaterialGroupID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ProductWizardSectionMaterialGroup]  WITH CHECK ADD  CONSTRAINT [FK_ProductWizardSectionMaterialGroup_MaterialGroup] FOREIGN KEY([MaterialGroupID])
REFERENCES [dbo].[MaterialGroup] ([MaterialGroupID])
GO

ALTER TABLE [dbo].[ProductWizardSectionMaterialGroup] CHECK CONSTRAINT [FK_ProductWizardSectionMaterialGroup_MaterialGroup]
GO

ALTER TABLE [dbo].[ProductWizardSectionMaterialGroup]  WITH CHECK ADD  CONSTRAINT [FK_ProductWizardSectionMaterialGroup_ProductWizardSection] FOREIGN KEY([ProductWizardSectionID])
REFERENCES [dbo].[ProductWizardSection] ([ProductWizardSectionID])
GO

ALTER TABLE [dbo].[ProductWizardSectionMaterialGroup] CHECK CONSTRAINT [FK_ProductWizardSectionMaterialGroup_ProductWizardSection]

/*Bảng này cho biết với Mỗi MaterialGroupID thì có nguyên vật liệu cụ thể nào*/
CREATE TABLE [dbo].[MaterialConfig](
	[MaterialConfigID] [int] IDENTITY(1,1) NOT NULL,
	[MaterialGroupID] [int] NULL,
	[MaterialID] [int] NULL,
	[MaterialTypeID] [int] NULL,
	[MaterialColorID] [int] NULL,
	[PhotoImage] [varchar](50) NULL,
	[IsRCSEnabled] [bit] NULL,
	[IsFSCEnabled] [bit] NULL,
	[IsEnabled] [bit] NULL,
	[UpdatedBy] [int] NULL,
	[UpdatedDate] [datetime] NULL
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[MaterialConfig]  WITH CHECK ADD  CONSTRAINT [FK_MaterialConfig_Material2] FOREIGN KEY([MaterialID])
REFERENCES [dbo].[Material2] ([MaterialID])
GO

ALTER TABLE [dbo].[MaterialConfig] CHECK CONSTRAINT [FK_MaterialConfig_Material2]
GO

ALTER TABLE [dbo].[MaterialConfig]  WITH CHECK ADD  CONSTRAINT [FK_MaterialConfig_MaterialColor2] FOREIGN KEY([MaterialColorID])
REFERENCES [dbo].[MaterialColor2] ([MaterialColorID])
GO

ALTER TABLE [dbo].[MaterialConfig] CHECK CONSTRAINT [FK_MaterialConfig_MaterialColor2]
GO

ALTER TABLE [dbo].[MaterialConfig]  WITH CHECK ADD  CONSTRAINT [FK_MaterialConfig_MaterialGroup] FOREIGN KEY([MaterialGroupID])
REFERENCES [dbo].[MaterialGroup] ([MaterialGroupID])
GO

ALTER TABLE [dbo].[MaterialConfig] CHECK CONSTRAINT [FK_MaterialConfig_MaterialGroup]
GO

ALTER TABLE [dbo].[MaterialConfig]  WITH CHECK ADD  CONSTRAINT [FK_MaterialConfig_MaterialType2] FOREIGN KEY([MaterialTypeID])
REFERENCES [dbo].[MaterialType2] ([MaterialTypeID])
GO

ALTER TABLE [dbo].[MaterialConfig] CHECK CONSTRAINT [FK_MaterialConfig_MaterialType2]
GO

/*SQL mẫu để lấy dữ liệu*/
--table: material, material type
SELECT DISTINCT
	ProductWizardSectionMaterialGroup.ProductWizardSectionID
	,MaterialConfig.MaterialID
	,MaterialConfig.MaterialTypeID	
	,'' AS MaterialUD
	,'' AS MaterialTypeUD
	,Material2.MaterialNM
	,MaterialType2.MaterialTypeNM
	,CAST(MaterialConfig.MaterialID AS VARCHAR) + '_' + CAST(ISNULL(MaterialConfig.MaterialTypeID,0) AS VARCHAR) AS MaterialMaterialTypeKeyID
	,Material2.MaterialNM + IIF(ISNULL(MaterialType2.MaterialTypeNM,'') = 'N/A', '', ' ' + MaterialType2.MaterialTypeNM) AS MaterialMaterialTypeKeyNM
FROM 
	MaterialConfig

	LEFT JOIN Material2
		ON MaterialConfig.MaterialID = Material2.MaterialID

	LEFT JOIN MaterialType2
		ON MaterialConfig.MaterialTypeID = MaterialType2.MaterialTypeID

	LEFT JOIN ProductWizardSectionMaterialGroup
		ON MaterialConfig.MaterialGroupID = ProductWizardSectionMaterialGroup.MaterialGroupID

	LEFT JOIN ModelMaterialConfig
		ON (
			ModelMaterialConfig.ModelID = @ModelID
			AND ProductWizardSectionMaterialGroup.ProductWizardSectionID = ModelMaterialConfig.ProductWizardSectionID
		)

--table:material color 
	SELECT
		MaterialColorOptionTable.*
		,ISNULL(MaterialConfigRCSTable.IsRCSEnabled, CAST(0 AS BIT)) AS IsRCSEnabled
		,ISNULL(MaterialConfigFSCTable.IsFSCEnabled, CAST(0 AS BIT)) AS IsFSCEnabled
	FROM(
		SELECT DISTINCT
			CAST(MaterialConfig.MaterialID AS VARCHAR) + '_' + CAST(ISNULL(MaterialConfig.MaterialTypeID,0) AS VARCHAR) AS MaterialMaterialTypeKeyID
			,MaterialConfig.MaterialID
			,MaterialConfig.MaterialTypeID	
			,MaterialConfig.MaterialColorID
			,'' AS MaterialColorUD
			,MaterialColor2.MaterialColorNM

			,'' AS ImageFileUrl
			,'' AS ImageThumbnailUrl
		FROM 
			MaterialConfig

			LEFT JOIN Material2
				ON MaterialConfig.MaterialID = Material2.MaterialID

			LEFT JOIN MaterialType2
				ON MaterialConfig.MaterialTypeID = MaterialType2.MaterialTypeID

			LEFT JOIN MaterialColor2
				ON MaterialConfig.MaterialColorID = MaterialColor2.MaterialColorID
		WHERE	
			MaterialConfig.IsEnabled = 1
	) AS MaterialColorOptionTable
	OUTER APPLY (
		SELECT TOP 1 IsRCSEnabled FROM MaterialConfig 
		WHERE 
			MaterialID = MaterialColorOptionTable.MaterialID 
			AND MaterialTypeID = MaterialColorOptionTable.MaterialTypeID 
			AND MaterialColorID = MaterialColorOptionTable.MaterialColorID 
		ORDER BY IsRCSEnabled DESC		
	) AS MaterialConfigRCSTable
	OUTER APPLY (
		SELECT TOP 1 IsFSCEnabled FROM MaterialConfig 
		WHERE 
			MaterialID = MaterialColorOptionTable.MaterialID 
			AND MaterialTypeID = MaterialColorOptionTable.MaterialTypeID 
			AND MaterialColorID = MaterialColorOptionTable.MaterialColorID 
		ORDER BY IsFSCEnabled DESC			
	) AS MaterialConfigFSCTable
	ORDER BY
		MaterialColorOptionTable.MaterialColorNM
		
	--table: cushion color
	SELECT 
		CushionColor.CushionColorID
		,CushionColor.CushionColorUD
		,SupportMng_CushionType_View.CushionTypeNM + ' ' + CushionColor.CushionColorNM AS CushionColorNM
		,CushionColor.ImageFile
		,@MediaFullSizeUrl + Files.FileLocation AS ImageFileUrl
		,@MediaThumbnailUrl + Files.ThumbnailLocation AS ImageThumbnailUrl
		,CushionColor.CushionTypeID		
		,SupportMng_CushionType_View.CushionTypeNM
	FROM 
		CushionColor

		LEFT JOIN SupportMng_CushionType_View
			ON CushionColor.CushionTypeID = SupportMng_CushionType_View.CushionTypeID

		LEFT JOIN Files
			ON CushionColor.ImageFile = Files.FileUD					
	WHERE
		CushionColor.IsEnabled = 1
	ORDER BY
		SupportMng_CushionType_View.CushionTypeNM + ' ' + CushionColor.CushionColorNM

	--table: packaging method
	SELECT DISTINCT
		PackagingMethod.PackagingMethodID
		,PackagingMethod.PackagingMethodUD
		,PackagingMethod.PackagingMethodNM
	FROM
		ModelPackagingMethodOption
		LEFT JOIN PackagingMethod
			ON ModelPackagingMethodOption.PackagingMethodID = PackagingMethod.PackagingMethodID
	WHERE
		ModelPackagingMethodOption.ModelID = @ModelID
	ORDER BY
		PackagingMethod.PackagingMethodNM

	--table: packaging method option
	SELECT
		ModelPackagingMethodOption.ModelPackagingMethodOptionID
		,PackagingMethod.PackagingMethodID
		,PackagingMethod.PackagingMethodUD
		,PackagingMethod.PackagingMethodNM

		,ModelPackagingMethodOption.PackagingMethodOptionUD
		,REPLACE(ISNULL(ModelPackagingMethodOption.PackagingMethodDescription,''), '{fsc}', '') AS PackagingMethodOptionNM
		,ISNULL(ModelPackagingMethodOption.PackagingMethodDescription,'') AS PackagingMethodDescription		
	FROM 
		ModelPackagingMethodOption
		LEFT JOIN PackagingMethod
			ON (PackagingMethod.PackagingMethodID = ModelPackagingMethodOption.PackagingMethodID)
		LEFT JOIN dbo.OfferSeasonMng_table_function_getPackagingMethodBoxCount() as PackagingMethodBoxCountTable
			ON (PackagingMethodBoxCountTable.ModelPackagingMethodOptionID = ModelPackagingMethodOption.ModelPackagingMethodOptionID)
	WHERE
		ModelPackagingMethodOption.ModelID = @ModelID