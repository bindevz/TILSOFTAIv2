SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'tool.list')
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
        'tool.list',
        'ai_tool_list',
        1,
        NULL,
        '{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}',
        'Return the list of enabled tools and their schemas.',
        'List available tools for the tenant.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'tool.list' AND Language = 'en')
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
        'tool.list',
        'en',
        'Return the list of enabled tools and their schemas.',
        'List available tools for the tenant.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'tool.list' AND Language = 'vi')
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
        'tool.list',
        'vi',
        'Tra ve danh sach cong cu dang bat va schema cua chung.',
        'Liet ke cong cu co san cho tenant.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'atomic_execute_plan')
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
        'atomic_execute_plan',
        'ai_atomic_execute_plan',
        1,
        NULL,
        '{\"type\":\"object\",\"required\":[\"planJson\"],\"additionalProperties\":false,\"properties\":{\"planJson\":{\"type\":\"object\"}}}',
        'Execute an atomic data plan. Provide planJson with datasetKey, select fields, optional where/groupBy/orderBy/limit/offset/timeRange/drilldown. Tenant scope is enforced.',
        'Execute an atomic plan against the dataset catalog and return meta/columns/rows.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'atomic_execute_plan' AND Language = 'en')
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
        'atomic_execute_plan',
        'en',
        'Execute an atomic data plan. Provide planJson with datasetKey, select fields, optional where/groupBy/orderBy/limit/offset/timeRange/drilldown. Tenant scope is enforced.',
        'Execute an atomic plan against the dataset catalog and return meta/columns/rows.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'atomic_execute_plan' AND Language = 'vi')
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
        'atomic_execute_plan',
        'vi',
        'Thuc thi atomic plan. planJson gom datasetKey, select, where/groupBy/orderBy/limit/offset/timeRange/drilldown. Luon trong pham vi tenant.',
        'Thuc thi atomic plan theo dataset catalog va tra ve meta/columns/rows.'
    );
END;
GO
