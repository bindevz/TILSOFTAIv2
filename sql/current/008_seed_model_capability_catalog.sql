SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @NowUtc datetime2(3) = SYSUTCDATETIME();

MERGE ai.Capability AS target
USING (VALUES
    (N'model.count', N'model', N'model_count', N'sql', N'execute_tool', N'sql', N'ai_model_count', N'readonly', N'{"contractVersion":"1","requiredArguments":[],"allowedArguments":["season"],"allowAdditionalArguments":false,"arguments":[{"name":"season","type":"string","format":"season","minLength":1,"maxLength":50}]}', 1),
    (N'model.overview.by-code', N'model', N'model_overview_by_code', N'sql', N'execute_tool', N'sql', N'ai_model_get_overview', N'readonly', N'{"contractVersion":"1","requiredArguments":["modelCode"],"allowedArguments":["modelCode"],"allowAdditionalArguments":false,"arguments":[{"name":"modelCode","type":"string","format":"model-code","minLength":1,"maxLength":50}]}', 1),
    (N'model.pieces.by-code', N'model', N'model_pieces_by_code', N'sql', N'execute_tool', N'sql', N'ai_model_get_pieces', N'readonly', N'{"contractVersion":"1","requiredArguments":["modelCode"],"allowedArguments":["modelCode"],"allowAdditionalArguments":false,"arguments":[{"name":"modelCode","type":"string","format":"model-code","minLength":1,"maxLength":50}]}', 1),
    (N'model.materials.by-code', N'model', N'model_materials_by_code', N'sql', N'execute_tool', N'sql', N'ai_model_get_materials', N'readonly', N'{"contractVersion":"1","requiredArguments":["modelCode"],"allowedArguments":["modelCode"],"allowAdditionalArguments":false,"arguments":[{"name":"modelCode","type":"string","format":"model-code","minLength":1,"maxLength":50}]}', 1),
    (N'model.compare', N'model', N'model_compare', N'sql', N'execute_tool', N'sql', N'ai_model_compare', N'readonly', N'{"contractVersion":"1","requiredArguments":["modelCodes"],"allowedArguments":["modelCodes"],"allowAdditionalArguments":false,"arguments":[{"name":"modelCodes","type":"array","format":"model-code-list","minLength":2}]}', 1),
    (N'model.packaging.by-code', N'model', N'model_packaging_by_code', N'sql', N'execute_tool', N'sql', N'ai_model_get_packaging', N'readonly', N'{"contractVersion":"1","requiredArguments":["modelCode"],"allowedArguments":["modelCode"],"allowAdditionalArguments":false,"arguments":[{"name":"modelCode","type":"string","format":"model-code","minLength":1,"maxLength":50}]}', 1)
) AS source (CapabilityKey, Domain, FunctionName, AdapterType, Operation, TargetSystemId, StoredProcedureName, ExecutionMode, ArgumentContract, Version)
ON target.CapabilityKey = source.CapabilityKey
WHEN MATCHED THEN UPDATE SET
    Domain = source.Domain,
    BusinessArea = source.Domain,
    FunctionName = source.FunctionName,
    AdapterType = source.AdapterType,
    Operation = source.Operation,
    TargetSystemId = source.TargetSystemId,
    StoredProcedureName = source.StoredProcedureName,
    StoredProcedure = source.StoredProcedureName,
    ExecutionMode = source.ExecutionMode,
    ArgumentContract = source.ArgumentContract,
    RequiredRoles = N'[]',
    AllowedTenants = NULL,
    AllowMultiCall = CASE WHEN source.CapabilityKey IN (N'model.count', N'model.compare') THEN 1 ELSE 0 END,
    IsEnabled = 1,
    IsActive = 1,
    Version = source.Version,
    VersionNo = source.Version,
    UpdatedAtUtc = @NowUtc,
    UpdatedAt = @NowUtc
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, Domain, BusinessArea, FunctionName, AdapterType, Operation, TargetSystemId, StoredProcedureName, StoredProcedure, ExecutionMode, ArgumentContract, RequiredRoles, AllowMultiCall, IsEnabled, IsActive, Version, VersionNo, CreatedAtUtc, UpdatedAtUtc, UpdatedAt)
    VALUES (source.CapabilityKey, source.Domain, source.Domain, source.FunctionName, source.AdapterType, source.Operation, source.TargetSystemId, source.StoredProcedureName, source.StoredProcedureName, source.ExecutionMode, source.ArgumentContract, N'[]', CASE WHEN source.CapabilityKey IN (N'model.count', N'model.compare') THEN 1 ELSE 0 END, 1, 1, source.Version, source.Version, @NowUtc, @NowUtc, @NowUtc);
GO

MERGE ai.CapabilityText AS target
USING (VALUES
    (N'model.count', N'en-US', N'Model count', N'Count product models in the catalog.', N'Do not use for piece, material, packaging, or comparison details.', N'["count models","total models","how many models"]', 0),
    (N'model.count', N'vi-VN', N'Dem model', N'Dem so luong model trong catalog.', N'Khong dung de xem piece, material, dong goi, hoac so sanh.', N'["co bao nhieu model","tong so model","dem model"]', 1),
    (N'model.overview.by-code', N'en-US', N'Model overview', N'Return overview information for one model code.', N'Do not use when the user asks for pieces, materials, packaging, or comparison.', N'["model details","model info","overview"]', 0),
    (N'model.overview.by-code', N'vi-VN', N'Thong tin model', N'Tra thong tin tong quan cho mot ma model.', N'Khong dung khi nguoi dung hoi piece, material, dong goi, hoac so sanh.', N'["thong tin model","chi tiet model","xem model"]', 1),
    (N'model.pieces.by-code', N'en-US', N'Model pieces', N'Return pieces and components for one model code.', N'Do not use for materials, packaging, count, or overview-only questions.', N'["pieces","components","parts"]', 0),
    (N'model.pieces.by-code', N'vi-VN', N'Piece cua model', N'Tra danh sach piece va thanh phan cua mot ma model.', N'Khong dung cho material, dong goi, dem so, hoac thong tin tong quan.', N'["piece","thanh phan","gom nhung piece nao"]', 1),
    (N'model.materials.by-code', N'en-US', N'Model materials', N'Return materials for one model code.', N'Do not use for pieces, packaging, count, or comparison.', N'["materials","raw materials","material list"]', 0),
    (N'model.materials.by-code', N'vi-VN', N'Nguyen lieu model', N'Tra danh sach material hoac nguyen lieu cua mot ma model.', N'Khong dung cho piece, dong goi, dem so, hoac so sanh.', N'["material","nguyen lieu","vat tu"]', 1),
    (N'model.compare', N'en-US', N'Compare models', N'Compare two or more model codes.', N'Do not use for a single model overview, pieces, materials, or packaging list.', N'["compare models","compare model codes","model comparison"]', 0),
    (N'model.compare', N'vi-VN', N'So sanh model', N'So sanh hai hoac nhieu ma model.', N'Khong dung cho thong tin cua mot model, piece, material, hoac dong goi.', N'["so sanh model","compare model","doi chieu model"]', 1),
    (N'model.packaging.by-code', N'en-US', N'Model packaging', N'Return packaging options for one model code.', N'Do not use for overview, pieces, materials, count, or comparison.', N'["packaging","carton","qnt40hc"]', 0),
    (N'model.packaging.by-code', N'vi-VN', N'Dong goi model', N'Tra thong tin dong goi cua mot ma model.', N'Khong dung cho tong quan, piece, material, dem so, hoac so sanh.', N'["dong goi","carton","qnt40hc"]', 1)
) AS source (CapabilityKey, Locale, Name, Description, NegativeDescription, AliasesJson, IsDefault)
ON target.CapabilityKey = source.CapabilityKey AND target.Locale = source.Locale
WHEN MATCHED THEN UPDATE SET
    Name = source.Name,
    ShortName = source.Name,
    Description = source.Description,
    NegativeDescription = source.NegativeDescription,
    DoNotUseWhen = source.NegativeDescription,
    AliasesJson = source.AliasesJson,
    IsDefault = source.IsDefault,
    BusinessNotes = N'SQL-backed source of truth.',
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, Locale, Name, ShortName, Description, NegativeDescription, DoNotUseWhen, AliasesJson, IsDefault, BusinessNotes)
    VALUES (source.CapabilityKey, source.Locale, source.Name, source.Name, source.Description, source.NegativeDescription, source.NegativeDescription, source.AliasesJson, source.IsDefault, N'SQL-backed source of truth.');
GO

MERGE ai.CapabilityArgument AS target
USING (VALUES
    (N'model.count', N'season', N'season', N'$.season', N'season', N'string', 0, NULL, N'season', NULL, 1, 50, 1),
    (N'model.overview.by-code', N'modelCode', N'modelCode', N'$.modelCode', N'modelCode', N'string', 1, NULL, N'model-code', NULL, 1, 50, 1),
    (N'model.pieces.by-code', N'modelCode', N'modelCode', N'$.modelCode', N'modelCode', N'string', 1, NULL, N'model-code', NULL, 1, 50, 1),
    (N'model.materials.by-code', N'modelCode', N'modelCode', N'$.modelCode', N'modelCode', N'string', 1, NULL, N'model-code', NULL, 1, 50, 1),
    (N'model.compare', N'modelCodes', N'modelCodes', N'$.modelCodes', N'modelCodes', N'array', 1, NULL, N'model-code-list', NULL, 2, NULL, 1),
    (N'model.packaging.by-code', N'modelCode', N'modelCode', N'$.modelCode', N'modelCode', N'string', 1, NULL, N'model-code', NULL, 1, 50, 1)
) AS source (CapabilityKey, ArgumentName, ModelFacingName, SqlParameterName, ProcParameterName, Type, IsRequired, EnumJson, Format, RegexPattern, MinLength, MaxLength, SortOrder)
ON target.CapabilityKey = source.CapabilityKey AND target.ArgumentName = source.ArgumentName
WHEN MATCHED THEN UPDATE SET
    ModelFacingName = source.ModelFacingName,
    SqlParameterName = source.SqlParameterName,
    ProcParameterName = source.ProcParameterName,
    Type = source.Type,
    DataType = source.Type,
    IsRequired = source.IsRequired,
    EnumJson = source.EnumJson,
    Format = source.Format,
    RegexPattern = source.RegexPattern,
    MinLength = source.MinLength,
    MaxLength = source.MaxLength,
    ValidationRule = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(N'{}', N'$.format', source.Format), N'$.minLength', source.MinLength), N'$.maxLength', source.MaxLength),
    SortOrder = source.SortOrder,
    DisplayOrder = source.SortOrder,
    IsActive = 1
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, ArgumentName, ModelFacingName, SqlParameterName, ProcParameterName, Type, DataType, IsRequired, EnumJson, Format, RegexPattern, MinLength, MaxLength, ValidationRule, SortOrder, DisplayOrder, IsActive)
    VALUES (source.CapabilityKey, source.ArgumentName, source.ModelFacingName, source.SqlParameterName, source.ProcParameterName, source.Type, source.Type, source.IsRequired, source.EnumJson, source.Format, source.RegexPattern, source.MinLength, source.MaxLength, JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(N'{}', N'$.format', source.Format), N'$.minLength', source.MinLength), N'$.maxLength', source.MaxLength), source.SortOrder, source.SortOrder, 1);
GO

MERGE ai.CapabilityArgumentText AS target
USING (VALUES
    (N'model.count', N'season', N'en-US', N'Optional season filter.', N'["season"]', N'Which season should be counted?', N'["How many models are in season 2026?"]'),
    (N'model.count', N'season', N'vi-VN', N'Bo loc season neu co.', N'["season","mua"]', N'Ban muon dem season nao?', N'["Co bao nhieu model season 2026?"]'),
    (N'model.overview.by-code', N'modelCode', N'en-US', N'Model code to inspect.', N'["model","model code"]', N'Which model code do you want to view?', N'["Show model ABC details"]'),
    (N'model.overview.by-code', N'modelCode', N'vi-VN', N'Ma model can xem.', N'["model","ma model"]', N'Ban muon xem ma model nao?', N'["Cho toi xem thong tin model ABC"]'),
    (N'model.pieces.by-code', N'modelCode', N'en-US', N'Model code whose pieces should be listed.', N'["piece","component","model code"]', N'Which model code should I list pieces for?', N'["Model ABC has which pieces?"]'),
    (N'model.pieces.by-code', N'modelCode', N'vi-VN', N'Ma model can xem piece.', N'["piece","thanh phan","ma model"]', N'Ban muon xem piece cua ma model nao?', N'["Model ABC gom nhung piece nao?"]'),
    (N'model.materials.by-code', N'modelCode', N'en-US', N'Model code whose materials should be listed.', N'["material","materials","model code"]', N'Which model code should I list materials for?', N'["Show materials for model ABC"]'),
    (N'model.materials.by-code', N'modelCode', N'vi-VN', N'Ma model can xem nguyen lieu.', N'["material","vat tu","nguyen lieu"]', N'Ban muon xem material cua ma model nao?', N'["Nguyen lieu model ABC"]'),
    (N'model.compare', N'modelCodes', N'en-US', N'Two or more model codes to compare.', N'["compare","model codes"]', N'Which model codes should I compare?', N'["Compare model ABC and XYZ"]'),
    (N'model.compare', N'modelCodes', N'vi-VN', N'Hai hoac nhieu ma model can so sanh.', N'["so sanh","ma model"]', N'Ban muon so sanh nhung ma model nao?', N'["So sanh model ABC va XYZ"]'),
    (N'model.packaging.by-code', N'modelCode', N'en-US', N'Model code whose packaging should be listed.', N'["packaging","carton","qnt40hc"]', N'Which model code should I list packaging for?', N'["Show packaging for model ABC"]'),
    (N'model.packaging.by-code', N'modelCode', N'vi-VN', N'Ma model can xem dong goi.', N'["dong goi","carton","qnt40hc"]', N'Ban muon xem dong goi cua ma model nao?', N'["Dong goi model ABC"]')
) AS source (CapabilityKey, ArgumentName, Locale, Description, AliasesJson, ClarificationQuestion, ExamplesJson)
ON target.CapabilityKey = source.CapabilityKey AND target.ArgumentName = source.ArgumentName AND target.Locale = source.Locale
WHEN MATCHED THEN UPDATE SET
    Description = source.Description,
    AliasesJson = source.AliasesJson,
    ClarificationQuestion = source.ClarificationQuestion,
    ExamplesJson = source.ExamplesJson,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, ArgumentName, Locale, Description, AliasesJson, ClarificationQuestion, ExamplesJson)
    VALUES (source.CapabilityKey, source.ArgumentName, source.Locale, source.Description, source.AliasesJson, source.ClarificationQuestion, source.ExamplesJson);
GO

MERGE ai.ArgumentText AS target
USING (
    SELECT CapabilityKey, ArgumentName, Locale, Description, AliasesJson, ExamplesJson
    FROM ai.CapabilityArgumentText
) AS source
ON target.CapabilityKey = source.CapabilityKey AND target.ArgumentName = source.ArgumentName AND target.Locale = source.Locale
WHEN MATCHED THEN UPDATE SET
    Description = source.Description,
    Aliases = source.AliasesJson,
    Examples = source.ExamplesJson,
    UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, ArgumentName, Locale, Description, Aliases, Examples)
    VALUES (source.CapabilityKey, source.ArgumentName, source.Locale, source.Description, source.AliasesJson, source.ExamplesJson);
GO

DECLARE @ModelCodeColumns nvarchar(max) = N'[{"name":"ModelCode","label":"Model Code","labelVi":"Ma model","type":"string","role":"dimension"},{"name":"Name","label":"Name","labelVi":"Ten","type":"string"},{"name":"LineNo","label":"Line","labelVi":"Dong","type":"integer","role":"measure"}]';

MERGE ai.CapabilityResultSchema AS target
USING (VALUES
    (N'model.count', N'{"columns":[{"name":"Count","label":"Model Count","labelVi":"So model","type":"integer","role":"measure"},{"name":"ModelCount","label":"Model Count","labelVi":"So model","type":"integer","role":"measure"}]}'),
    (N'model.overview.by-code', N'{"columns":[{"name":"ModelCode","label":"Model Code","labelVi":"Ma model","type":"string","role":"dimension"},{"name":"Name","label":"Name","labelVi":"Ten","type":"string"},{"name":"Description","label":"Description","labelVi":"Mo ta","type":"string"},{"name":"TotalCbm","label":"Total CBM","labelVi":"Tong CBM","type":"decimal","role":"measure"},{"name":"TotalWeightKg","label":"Total Weight KG","labelVi":"Tong kg","type":"decimal","role":"measure"},{"name":"PieceCount","label":"Piece Count","labelVi":"So piece","type":"integer","role":"measure"},{"name":"BoxInSet","label":"Box In Set","labelVi":"Box trong set","type":"integer","role":"measure"}]}'),
    (N'model.pieces.by-code', N'{"columns":[{"name":"ModelPieceId","label":"Model Piece ID","labelVi":"ID piece","type":"integer","visible":false},{"name":"PieceName","label":"Piece Name","labelVi":"Ten piece","type":"string"},{"name":"Quantity","label":"Quantity","labelVi":"So luong","type":"integer","role":"measure"},{"name":"Sequence","label":"Sequence","labelVi":"Thu tu","type":"integer"}]}'),
    (N'model.materials.by-code', N'{"columns":[{"name":"MaterialCode","label":"Material Code","labelVi":"Ma material","type":"string"},{"name":"MaterialName","label":"Material Name","labelVi":"Ten material","type":"string"},{"name":"Category","label":"Category","labelVi":"Nhom","type":"string"},{"name":"Section","label":"Section","labelVi":"Khu vuc","type":"string"},{"name":"Quantity","label":"Quantity","labelVi":"So luong","type":"decimal","role":"measure"},{"name":"Unit","label":"Unit","labelVi":"Don vi","type":"string"},{"name":"WeightKg","label":"Weight KG","labelVi":"Kg","type":"decimal","role":"measure"}]}'),
    (N'model.compare', N'{"columns":[{"name":"ModelCode","label":"Model Code","labelVi":"Ma model","type":"string","role":"dimension"},{"name":"Name","label":"Name","labelVi":"Ten","type":"string"},{"name":"TotalCbm","label":"Total CBM","labelVi":"Tong CBM","type":"decimal","role":"measure"},{"name":"TotalWeightKg","label":"Total Weight KG","labelVi":"Tong kg","type":"decimal","role":"measure"},{"name":"PieceCount","label":"Piece Count","labelVi":"So piece","type":"integer","role":"measure"},{"name":"PackagingName","label":"Packaging Name","labelVi":"Ten dong goi","type":"string"}]}'),
    (N'model.packaging.by-code', N'{"columns":[{"name":"OptionName","label":"Option Name","labelVi":"Ten phuong an","type":"string"},{"name":"PackagingType","label":"Packaging Type","labelVi":"Loai dong goi","type":"string"},{"name":"UnitsPerCarton","label":"Units Per Carton","labelVi":"So luong/carton","type":"integer","role":"measure"},{"name":"CartonCbm","label":"Carton CBM","labelVi":"CBM carton","type":"decimal","role":"measure"},{"name":"CartonWeightKg","label":"Carton Weight KG","labelVi":"Kg carton","type":"decimal","role":"measure"},{"name":"Qnt40HC","label":"40HC Quantity","labelVi":"So luong 40HC","type":"integer","role":"measure"}]}')
) AS source (CapabilityKey, SchemaJson)
ON target.CapabilityKey = source.CapabilityKey
WHEN MATCHED THEN UPDATE SET SchemaJson = source.SchemaJson, UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (CapabilityKey, SchemaJson) VALUES (source.CapabilityKey, source.SchemaJson);
GO

MERGE ai.CapabilityAnswerPolicy AS target
USING (VALUES
    (N'model.count', N'{"maxRowsForChat":20,"maxRowsForNarration":20,"summary":{"mode":"ai","style":"business_concise","maxSentences":3,"includeFilters":true,"includeRowCount":true,"includeCaveats":true,"instructionsByLocale":{"vi-VN":"Tom tat du lieu ERP ngan gon cho nguoi dung noi bo. Chi dung du lieu duoc cung cap. Khong suy dien.","en-US":"Summarize ERP data concisely for internal users. Use only supplied data. Do not infer."},"forbiddenClaims":["Do not infer causes.","Do not recommend actions unless supported by data.","Do not mention data not present in supplied rows.","Do not expose hidden or masked fields."]},"table":{"enabled":true,"maxDisplayedRows":20,"includeRowCount":true,"includeTruncationNotice":true},"noData":{"includeUsedFilters":true},"followUp":{"includeMissingFields":true}}'),
    (N'model.overview.by-code', N'{"maxRowsForChat":20,"maxRowsForNarration":20,"summary":{"mode":"ai","style":"business_concise","maxSentences":3,"includeFilters":true,"includeRowCount":true,"includeCaveats":true,"instructionsByLocale":{"vi-VN":"Tom tat du lieu ERP ngan gon cho nguoi dung noi bo. Chi dung du lieu duoc cung cap. Khong suy dien.","en-US":"Summarize ERP data concisely for internal users. Use only supplied data. Do not infer."},"forbiddenClaims":["Do not infer causes.","Do not recommend actions unless supported by data.","Do not mention data not present in supplied rows.","Do not expose hidden or masked fields."]},"table":{"enabled":true,"maxDisplayedRows":20,"includeRowCount":true,"includeTruncationNotice":true},"noData":{"includeUsedFilters":true},"followUp":{"includeMissingFields":true}}'),
    (N'model.pieces.by-code', N'{"maxRowsForChat":20,"maxRowsForNarration":20,"summary":{"mode":"ai","style":"business_concise","maxSentences":3,"includeFilters":true,"includeRowCount":true,"includeCaveats":true,"instructionsByLocale":{"vi-VN":"Tom tat du lieu ERP ngan gon cho nguoi dung noi bo. Chi dung du lieu duoc cung cap. Khong suy dien.","en-US":"Summarize ERP data concisely for internal users. Use only supplied data. Do not infer."},"forbiddenClaims":["Do not infer causes.","Do not recommend actions unless supported by data.","Do not mention data not present in supplied rows.","Do not expose hidden or masked fields."]},"table":{"enabled":true,"maxDisplayedRows":20,"includeRowCount":true,"includeTruncationNotice":true},"noData":{"includeUsedFilters":true},"followUp":{"includeMissingFields":true}}'),
    (N'model.materials.by-code', N'{"maxRowsForChat":20,"maxRowsForNarration":20,"summary":{"mode":"ai","style":"business_concise","maxSentences":3,"includeFilters":true,"includeRowCount":true,"includeCaveats":true,"instructionsByLocale":{"vi-VN":"Tom tat du lieu ERP ngan gon cho nguoi dung noi bo. Chi dung du lieu duoc cung cap. Khong suy dien.","en-US":"Summarize ERP data concisely for internal users. Use only supplied data. Do not infer."},"forbiddenClaims":["Do not infer causes.","Do not recommend actions unless supported by data.","Do not mention data not present in supplied rows.","Do not expose hidden or masked fields."]},"table":{"enabled":true,"maxDisplayedRows":20,"includeRowCount":true,"includeTruncationNotice":true},"noData":{"includeUsedFilters":true},"followUp":{"includeMissingFields":true}}'),
    (N'model.compare', N'{"maxRowsForChat":20,"maxRowsForNarration":20,"summary":{"mode":"ai","style":"business_concise","maxSentences":3,"includeFilters":true,"includeRowCount":true,"includeCaveats":true,"instructionsByLocale":{"vi-VN":"Tom tat du lieu ERP ngan gon cho nguoi dung noi bo. Chi dung du lieu duoc cung cap. Khong suy dien.","en-US":"Summarize ERP data concisely for internal users. Use only supplied data. Do not infer."},"forbiddenClaims":["Do not infer causes.","Do not recommend actions unless supported by data.","Do not mention data not present in supplied rows.","Do not expose hidden or masked fields."]},"table":{"enabled":true,"maxDisplayedRows":20,"includeRowCount":true,"includeTruncationNotice":true},"noData":{"includeUsedFilters":true},"followUp":{"includeMissingFields":true}}'),
    (N'model.packaging.by-code', N'{"maxRowsForChat":20,"maxRowsForNarration":20,"summary":{"mode":"ai","style":"business_concise","maxSentences":3,"includeFilters":true,"includeRowCount":true,"includeCaveats":true,"instructionsByLocale":{"vi-VN":"Tom tat du lieu ERP ngan gon cho nguoi dung noi bo. Chi dung du lieu duoc cung cap. Khong suy dien.","en-US":"Summarize ERP data concisely for internal users. Use only supplied data. Do not infer."},"forbiddenClaims":["Do not infer causes.","Do not recommend actions unless supported by data.","Do not mention data not present in supplied rows.","Do not expose hidden or masked fields."]},"table":{"enabled":true,"maxDisplayedRows":20,"includeRowCount":true,"includeTruncationNotice":true},"noData":{"includeUsedFilters":true},"followUp":{"includeMissingFields":true}}')
) AS source (CapabilityKey, PolicyJson)
ON target.CapabilityKey = source.CapabilityKey
WHEN MATCHED THEN UPDATE SET PolicyJson = source.PolicyJson, UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (CapabilityKey, PolicyJson) VALUES (source.CapabilityKey, source.PolicyJson);
GO

MERGE ai.CapabilitySensitivityPolicy AS target
USING (VALUES
    (N'model.count', N'{"hiddenColumns":[],"maskColumns":[],"maskValue":"***"}'),
    (N'model.overview.by-code', N'{"hiddenColumns":["TenantId","ModelId"],"maskColumns":[],"maskValue":"***"}'),
    (N'model.pieces.by-code', N'{"hiddenColumns":["ModelId","ChildModelId"],"maskColumns":[],"maskValue":"***"}'),
    (N'model.materials.by-code', N'{"hiddenColumns":["ModelMaterialId"],"maskColumns":[],"maskValue":"***"}'),
    (N'model.compare', N'{"hiddenColumns":["ModelId"],"maskColumns":[],"maskValue":"***"}'),
    (N'model.packaging.by-code', N'{"hiddenColumns":["PackagingOptionId"],"maskColumns":[],"maskValue":"***"}')
) AS source (CapabilityKey, PolicyJson)
ON target.CapabilityKey = source.CapabilityKey
WHEN MATCHED THEN UPDATE SET PolicyJson = source.PolicyJson, UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (CapabilityKey, PolicyJson) VALUES (source.CapabilityKey, source.PolicyJson);
GO

MERGE ai.CapabilityExample AS target
USING (VALUES
    (N'model.count', N'vi-VN', N'Co bao nhieu model?', N'{}', 1),
    (N'model.count', N'en-US', N'How many models are there?', N'{}', 1),
    (N'model.overview.by-code', N'vi-VN', N'Cho toi xem thong tin model ABC', N'{"modelCode":"ABC"}', 1),
    (N'model.overview.by-code', N'en-US', N'Show model ABC details', N'{"modelCode":"ABC"}', 1),
    (N'model.pieces.by-code', N'vi-VN', N'Model ABC gom nhung piece nao?', N'{"modelCode":"ABC"}', 1),
    (N'model.pieces.by-code', N'en-US', N'What pieces are in model ABC?', N'{"modelCode":"ABC"}', 1),
    (N'model.materials.by-code', N'vi-VN', N'Nguyen lieu model ABC', N'{"modelCode":"ABC"}', 1),
    (N'model.materials.by-code', N'en-US', N'Show materials for model ABC', N'{"modelCode":"ABC"}', 1),
    (N'model.compare', N'vi-VN', N'So sanh model ABC va XYZ', N'{"modelCodes":["ABC","XYZ"]}', 1),
    (N'model.compare', N'en-US', N'Compare model ABC and XYZ', N'{"modelCodes":["ABC","XYZ"]}', 1),
    (N'model.packaging.by-code', N'vi-VN', N'Dong goi model ABC', N'{"modelCode":"ABC"}', 1),
    (N'model.packaging.by-code', N'en-US', N'Show packaging for model ABC', N'{"modelCode":"ABC"}', 1)
) AS source (CapabilityKey, Locale, Utterance, ArgumentsJson, SortOrder)
ON target.CapabilityKey = source.CapabilityKey AND target.Locale = source.Locale AND target.SortOrder = source.SortOrder
WHEN MATCHED THEN UPDATE SET
    Utterance = source.Utterance,
    ArgumentsJson = source.ArgumentsJson,
    UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (CapabilityKey, Locale, Utterance, ArgumentsJson, SortOrder)
    VALUES (source.CapabilityKey, source.Locale, source.Utterance, source.ArgumentsJson, source.SortOrder);
GO

UPDATE c
SET
    ResultSchema = rs.SchemaJson,
    AnswerPolicy = ap.PolicyJson,
    SensitivityPolicy = sp.PolicyJson,
    UpdatedAtUtc = SYSUTCDATETIME(),
    UpdatedAt = SYSUTCDATETIME()
FROM ai.Capability c
JOIN ai.CapabilityResultSchema rs ON rs.CapabilityKey = c.CapabilityKey
JOIN ai.CapabilityAnswerPolicy ap ON ap.CapabilityKey = c.CapabilityKey
JOIN ai.CapabilitySensitivityPolicy sp ON sp.CapabilityKey = c.CapabilityKey
WHERE c.Domain = N'model';
GO

PRINT N'Seeded SQL-backed catalog metadata for six active model capabilities.';
GO
