# Mapping Notes — Model Schema -> Semantic Views

Mục tiêu: giữ nguyên schema doanh nghiệp, tạo layer “semantic views” để LLM tiêu thụ dễ, tránh JOIN phức tạp trong prompt.

## 1) Nguồn schema
- dbo.Model
- dbo.ModelPiece
- dbo.ModelPackagingMethodOption
- dbo.ModelMaterialConfig
- dbo.ProductWizardSection (+ MaterialConfig nếu cần)

## 2) Semantic Views đề xuất
### v_Model_Overview
Mỗi dòng = 1 model.
Cột gợi ý:
- ModelID, ModelUD, ModelNM, Season
- PieceCount (COUNT ModelPiece)
- DefaultCBM, Qnt40HC (từ option IsDefault=1, fallback option mới nhất)
- HasFSC, HasRCS (từ ProductWizardSection flags)

### v_Model_Pieces
- ParentModelID, PieceModelID, PieceModelUD, PieceModelNM, Quantity, RowIndex

### v_Model_Packaging_Default
- ModelID, MethodCode, BoxInSet, CBM, Qnt20DC, Qnt40DC, Qnt40HC, NetWeight, GrossWeight, CartonBoxDimL/W/H, PackagingRemark

### v_Model_Materials
- ModelID, ProductWizardSectionID, ProductWizardSectionNM, IsFSCEnabled, IsRCSEnabled, MaterialGroupID

## 3) JSON envelope
Tất cả ai_* SP trả về meta/columns/rows.
columns[].descriptionKey tham chiếu MetadataDictionary.

## 4) Tách concern
- View chỉ “phẳng hoá”.
- SP chỉ apply filter/limit/sort + envelope.
- C# không map DTO, chỉ pass-through JSON.
