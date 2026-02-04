/*******************************************************************************
* TILSOFTAI Analytics Module - Tool Catalog Seeds
* Purpose: Register analytics tools for LLM
*******************************************************************************/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- catalog_search tool
IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'catalog_search')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (ToolName, SpName, IsEnabled, RequiredRoles, JsonSchema, Instruction, Description)
    VALUES
    (
        'catalog_search',
        'ai_catalog_search',
        1,
        NULL,
        '{
            "type": "object",
            "required": ["query"],
            "additionalProperties": false,
            "properties": {
                "query": {
                    "type": "string",
                    "description": "Search terms to find relevant datasets and fields (e.g., ''model count season'')",
                    "minLength": 2,
                    "maxLength": 500
                },
                "topK": {
                    "type": "integer",
                    "description": "Maximum number of results per category (datasets/fields)",
                    "minimum": 1,
                    "maximum": 20,
                    "default": 5
                },
                "domain": {
                    "type": "string",
                    "description": "Search domain scope",
                    "enum": ["internal", "all"],
                    "default": "internal"
                }
            }
        }',
        'Use this tool FIRST when you need to find datasets or fields for analytics queries. 
        
WHEN TO CALL:
- User asks analytics questions (count, sum, breakdown, top N, trends)
- You do not know which dataset/fields to use
- You need to discover available metrics or dimensions

HOW TO USE:
1. Extract key entities from user question (e.g., "model", "order", "season")
2. Compose search query from entity + metric hints
3. Review results: datasets[] shows available data sources, fields[] shows queryable columns
4. Use hints[] for guidance on best matches

FOLLOW-UP ACTIONS:
- If good dataset found: call catalog_get_dataset to get full schema
- If no matches: try broader terms or ask user for clarification
- NEVER guess schema without calling this tool first',
        'Search for datasets and fields in the data catalog by keywords. Returns matching datasets, fields, and usage hints.'
    );
END;
GO

-- catalog_search translations
IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'catalog_search' AND Language = 'en')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation (ToolName, Language, Instruction, Description)
    VALUES
    (
        'catalog_search',
        'en',
        'Use this tool FIRST when you need to find datasets or fields for analytics queries. Extract key entities from user question, compose search query, and review results before proceeding.',
        'Search for datasets and fields in the data catalog by keywords.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'catalog_search' AND Language = 'vi')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation (ToolName, Language, Instruction, Description)
    VALUES
    (
        'catalog_search',
        'vi',
        'Dùng tool này TRƯỚC TIÊN khi cần tìm dataset hoặc field cho câu hỏi phân tích. Trích xuất entity chính từ câu hỏi, tạo query tìm kiếm, và xem kết quả trước khi tiếp tục.',
        'Tìm kiếm dataset và field trong catalog theo từ khóa.'
    );
END;
GO

-- catalog_get_dataset tool
IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'catalog_get_dataset')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (ToolName, SpName, IsEnabled, RequiredRoles, JsonSchema, Instruction, Description)
    VALUES
    (
        'catalog_get_dataset',
        'ai_catalog_get_dataset',
        1,
        NULL,
        '{
            "type": "object",
            "required": ["datasetKey"],
            "additionalProperties": false,
            "properties": {
                "datasetKey": {
                    "type": "string",
                    "description": "The exact datasetKey from catalog_search results",
                    "minLength": 1,
                    "maxLength": 200
                }
            }
        }',
        'Use this tool to get FULL SCHEMA of a dataset after finding it via catalog_search.

WHEN TO CALL:
- After catalog_search returned matching dataset(s)
- Before creating an atomic_execute_plan
- When you need to know exact field names, types, and allowed aggregations

WHAT YOU GET:
- dataset: grain (what each row represents), timeColumn (for time filters)
- fields[]: all queryable fields with their properties
  - fieldKey: use this in atomic plan
  - dataType: string/int/decimal/datetime
  - semanticType: dimension/measure/identifier
  - allowedAggregations: which ops are valid (count, sum, avg, etc.)
  - isFilterable/isGroupable: can be used in where/groupBy
  - securityTag: PII fields may be masked
- joins[]: available relationships to other datasets (max 1 hop)

FOLLOW-UP ACTIONS:
- Use schema to build atomic_execute_plan
- Only use fields that exist in this schema
- Only use aggregations allowed for each field',
        'Get full schema and metadata for a specific dataset including all fields, types, and allowed operations.'
    );
END;
GO

-- catalog_get_dataset translations
IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'catalog_get_dataset' AND Language = 'en')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation (ToolName, Language, Instruction, Description)
    VALUES
    (
        'catalog_get_dataset',
        'en',
        'Get full schema of a dataset after finding it via catalog_search. Use schema to build atomic plan with correct field names and allowed aggregations.',
        'Get full schema and metadata for a specific dataset.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'catalog_get_dataset' AND Language = 'vi')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation (ToolName, Language, Instruction, Description)
    VALUES
    (
        'catalog_get_dataset',
        'vi',
        'Lấy schema đầy đủ của dataset sau khi tìm qua catalog_search. Dùng schema để tạo atomic plan với đúng tên field và aggregation cho phép.',
        'Lấy schema và metadata đầy đủ cho một dataset cụ thể.'
    );
END;
GO

-- analytics_validate_plan tool
IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'analytics_validate_plan')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (ToolName, SpName, IsEnabled, RequiredRoles, JsonSchema, Instruction, Description)
    VALUES
    (
        'analytics_validate_plan',
        'ai_analytics_validate_plan',
        1,
        NULL,
        '{
            "type": "object",
            "required": ["planJson"],
            "additionalProperties": false,
            "properties": {
                "planJson": {
                    "type": "object",
                    "description": "The atomic plan to validate before execution"
                }
            }
        }',
        'Use this tool to validate an atomic plan BEFORE executing it.

WHEN TO CALL:
- After constructing atomic plan from schema
- Before calling atomic_execute_plan
- When plan is complex (multiple filters, groupBy, metrics)

VALIDATION CHECKS:
- datasetKey exists and is accessible
- All fields exist in dataset
- Aggregation ops are allowed for each field
- Limits are within bounds (maxRows=200, maxGroupBy=4, maxMetrics=3)
- Security tags are respected

IF VALIDATION FAILS:
- Check validation.errorCode and errorMessage
- Review suggestions[] for fixes
- If retryable=true, fix the plan and validate again (max 2 retries)
- If retryable=false, inform user of limitation',
        'Validate an atomic plan before execution. Returns validation result with error details and suggestions.'
    );
END;
GO
