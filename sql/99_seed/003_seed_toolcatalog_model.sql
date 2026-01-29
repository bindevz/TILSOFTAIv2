SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'model_get_overview')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (
        ToolName,
        SpName,
        IsEnabled,
        RequiredRoles,
        JsonSchema,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_overview',
        'ai_model_get_overview',
        1,
        NULL,
        '{"type":"object","required":["modelId"],"properties":{"modelId":{"type":"integer","minimum":1}},"additionalProperties":false}',
        'Call when the user asks for a model summary or packaging/logistics metrics. Interpret DefaultCbm (Model.Cbm), Qnt40HC (Model.Qnt40HC), BoxInSet (Packaging.BoxInSet), and FSC/RCS flags. Follow-up: if PieceCount > 0 call model_get_pieces; if materials are needed call model_get_materials; if packaging details are needed call model_get_packaging; if comparing multiple models call model_compare_models. Safety: use only the provided modelId; avoid PII; keep scope tight.',
        'Get model overview, logistics metrics, and piece counts.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_get_overview' AND Language = 'en')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_overview',
        'en',
        'Call when the user asks for a model summary or packaging/logistics metrics. Interpret DefaultCbm (Model.Cbm), Qnt40HC (Model.Qnt40HC), BoxInSet (Packaging.BoxInSet), and FSC/RCS flags. Follow-up: if PieceCount > 0 call model_get_pieces; if materials are needed call model_get_materials; if packaging details are needed call model_get_packaging; if comparing multiple models call model_compare_models. Safety: use only the provided modelId; avoid PII; keep scope tight.',
        'Get model overview, logistics metrics, and piece counts.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_get_overview' AND Language = 'vi')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_overview',
        'vi',
        'Goi khi nguoi dung hoi tom tat model hoac chi so dong goi/logistics. Dien giai DefaultCbm (Model.Cbm), Qnt40HC (Model.Qnt40HC), BoxInSet (Packaging.BoxInSet), va co FSC/RCS. Theo sau: neu PieceCount > 0 goi model_get_pieces; neu can vat lieu goi model_get_materials; neu can dong goi goi model_get_packaging; neu so sanh nhieu model goi model_compare_models. An toan: chi dung modelId duoc cung cap; tranh PII; gioi han pham vi.',
        'Lay tong quan model, chi so logistics, va so luong piece.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'model_get_pieces')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (
        ToolName,
        SpName,
        IsEnabled,
        RequiredRoles,
        JsonSchema,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_pieces',
        'ai_model_get_pieces',
        1,
        NULL,
        '{"type":"object","required":["modelId"],"properties":{"modelId":{"type":"integer","minimum":1}},"additionalProperties":false}',
        'Call when you need the piece list or hierarchy for a model. Use ChildModelId to detect nested sets and recursively call model_get_pieces for child model ids until the recursion policy limit. Follow-up: if materials are requested for a piece model, call model_get_materials with that child model id. Safety: do not traverse beyond max recursion depth.',
        'List model pieces and nested model references.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_get_pieces' AND Language = 'en')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_pieces',
        'en',
        'Call when you need the piece list or hierarchy for a model. Use ChildModelId to detect nested sets and recursively call model_get_pieces for child model ids until the recursion policy limit. Follow-up: if materials are requested for a piece model, call model_get_materials with that child model id. Safety: do not traverse beyond max recursion depth.',
        'List model pieces and nested model references.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_get_pieces' AND Language = 'vi')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_pieces',
        'vi',
        'Goi khi can danh sach piece hoac phan cap cho model. Dung ChildModelId de nhan biet bo long nhau va goi de quy model_get_pieces cho model con den khi dat gioi han de quy. Theo sau: neu can vat lieu cho piece model, goi model_get_materials voi model con do. An toan: khong vuot qua do sau de quy toi da.',
        'Liet ke piece model va tham chieu model long nhau.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'model_get_materials')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (
        ToolName,
        SpName,
        IsEnabled,
        RequiredRoles,
        JsonSchema,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_materials',
        'ai_model_get_materials',
        1,
        NULL,
        '{"type":"object","required":["modelId"],"properties":{"modelId":{"type":"integer","minimum":1}},"additionalProperties":false}',
        'Call when the user asks for material composition or sustainability flags. Group results by ProductWizardSectionNM and cite IsFSCEnabled/IsRCSEnabled with MaterialGroupID. Follow-up: use model_compare_models for cross-model comparisons. Safety: avoid inference; rely on returned metrics.',
        'Get model material configuration and quantities.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_get_materials' AND Language = 'en')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_materials',
        'en',
        'Call when the user asks for material composition or sustainability flags. Group results by ProductWizardSectionNM and cite IsFSCEnabled/IsRCSEnabled with MaterialGroupID. Follow-up: use model_compare_models for cross-model comparisons. Safety: avoid inference; rely on returned metrics.',
        'Get model material configuration and quantities.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_get_materials' AND Language = 'vi')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_materials',
        'vi',
        'Goi khi nguoi dung hoi thanh phan vat lieu hoac co FSC/RCS. Nhom ket qua theo ProductWizardSectionNM va neu IsFSCEnabled/IsRCSEnabled cung MaterialGroupID. Theo sau: dung model_compare_models de so sanh nhieu model. An toan: khong suy dien; dua vao chi so tra ve.',
        'Lay cau hinh vat lieu model va so luong.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'model_compare_models')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (
        ToolName,
        SpName,
        IsEnabled,
        RequiredRoles,
        JsonSchema,
        Instruction,
        Description
    )
    VALUES
    (
        'model_compare_models',
        'ai_model_compare_models',
        1,
        NULL,
        '{"type":"object","required":["modelIds"],"properties":{"modelIds":{"type":"array","minItems":2,"items":{"type":"integer","minimum":1}}},"additionalProperties":false}',
        'Call when the user asks to compare two or more models (packaging or loadability differences). Explain differences using DefaultCbm, Qnt40HC, BoxInSet, and CbmPer40HC; note FSC/RCS flags when relevant. Follow-up: if needed, drill into pieces, materials, or packaging for each model. Safety: do not speculate; stick to computed values.',
        'Compare models across logistics and packaging metrics.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_compare_models' AND Language = 'en')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_compare_models',
        'en',
        'Call when the user asks to compare two or more models (packaging or loadability differences). Explain differences using DefaultCbm, Qnt40HC, BoxInSet, and CbmPer40HC; note FSC/RCS flags when relevant. Follow-up: if needed, drill into pieces, materials, or packaging for each model. Safety: do not speculate; stick to computed values.',
        'Compare models across logistics and packaging metrics.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_compare_models' AND Language = 'vi')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_compare_models',
        'vi',
        'Goi khi nguoi dung muon so sanh hai hoac nhieu model (dong goi hoac loadability). Giai thich khac biet bang DefaultCbm, Qnt40HC, BoxInSet, va CbmPer40HC; neu co thi nhan FSC/RCS. Theo sau: neu can, di sau vao pieces, materials, hoac packaging cho tung model. An toan: khong suy doan; chi dung gia tri tinh toan.',
        'So sanh model theo chi so logistics va dong goi.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'model_get_packaging')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (
        ToolName,
        SpName,
        IsEnabled,
        RequiredRoles,
        JsonSchema,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_packaging',
        'ai_model_get_packaging',
        1,
        NULL,
        '{"type":"object","required":["modelId"],"properties":{"modelId":{"type":"integer","minimum":1}},"additionalProperties":false}',
        'Call when the user asks for packaging method, carton dimensions, CBM, BoxInSet, or container quantities. Uses the default packaging option for the model. Safety: use only the provided modelId; avoid PII.',
        'Get default packaging metrics for a model.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_get_packaging' AND Language = 'en')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_packaging',
        'en',
        'Call when the user asks for packaging method, carton dimensions, CBM, BoxInSet, or container quantities. Uses the default packaging option for the model. Safety: use only the provided modelId; avoid PII.',
        'Get default packaging metrics for a model.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_get_packaging' AND Language = 'vi')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_get_packaging',
        'vi',
        'Goi khi nguoi dung hoi phuong phap dong goi, kich thuoc thung, CBM, BoxInSet, hoac so luong container. Su dung packaging option mac dinh cua model. An toan: chi dung modelId duoc cung cap; tranh PII.',
        'Lay chi so dong goi mac dinh cua model.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'model_count')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (
        ToolName,
        SpName,
        IsEnabled,
        RequiredRoles,
        JsonSchema,
        Instruction,
        Description
    )
    VALUES
    (
        'model_count',
        'ai_model_count',
        1,
        NULL,
        '{"type":"object","properties":{"season":{"type":"string"}},"additionalProperties":false}',
        'Call when the user asks for model counts overall or by season. Optionally pass season to filter; results return counts grouped by season.',
        'Count models by season.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_count' AND Language = 'en')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_count',
        'en',
        'Call when the user asks for model counts overall or by season. Optionally pass season to filter; results return counts grouped by season.',
        'Count models by season.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'model_count' AND Language = 'vi')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation
    (
        ToolName,
        Language,
        Instruction,
        Description
    )
    VALUES
    (
        'model_count',
        'vi',
        'Goi khi nguoi dung hoi dem so model tong hoac theo season. Co the truyen season de loc; ket qua tra ve dem theo season.',
        'Dem so model theo season.'
    );
END;
GO
