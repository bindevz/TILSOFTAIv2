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
        'analytics.read',
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
        'analytics.read',
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
        'analytics.read',
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
- If retryable=false, inform user of limitation

SECURITY:
- _roles is SERVER-INJECTED. Do NOT include _roles in your plan; the server handles authorization automatically.',
        'Validate an atomic plan before execution. Returns validation result with error details and suggestions.'
    );
END;
GO

-- analytics_execute_plan tool (PATCH 29.01)
IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalog WHERE ToolName = 'analytics_execute_plan')
BEGIN
    INSERT INTO dbo.ToolCatalog
    (ToolName, SpName, IsEnabled, RequiredRoles, JsonSchema, Instruction, Description)
    VALUES
    (
        'analytics_execute_plan',
        'ai_analytics_execute_plan',
        1,
        'analytics.read',
        '{
            "type": "object",
            "required": ["datasetKey", "metrics"],
            "additionalProperties": false,
            "properties": {
                "datasetKey": {
                    "type": "string",
                    "description": "The exact datasetKey from catalog_get_dataset",
                    "minLength": 1,
                    "maxLength": 200
                },
                "metrics": {
                    "type": "array",
                    "description": "Aggregation metrics to compute",
                    "minItems": 1,
                    "maxItems": 3,
                    "items": {
                        "type": "object",
                        "required": ["op"],
                        "properties": {
                            "field": {
                                "type": "string",
                                "description": "Field to aggregate (optional for count)"
                            },
                            "op": {
                                "type": "string",
                                "enum": ["count", "countDistinct", "sum", "avg", "min", "max"],
                                "description": "Aggregation operation"
                            },
                            "alias": {
                                "type": "string",
                                "description": "Output column name (auto-generated if omitted)"
                            }
                        }
                    }
                },
                "groupBy": {
                    "type": "array",
                    "description": "Fields to group by (max 4)",
                    "maxItems": 4,
                    "items": {
                        "type": "string"
                    }
                },
                "where": {
                    "type": "array",
                    "description": "Filter conditions",
                    "items": {
                        "type": "object",
                        "required": ["field", "op"],
                        "properties": {
                            "field": { "type": "string" },
                            "op": { "type": "string", "enum": ["eq", "ne", "gt", "gte", "lt", "lte", "like", "in", "between"] },
                            "value": { "type": "string" },
                            "values": { "type": "array", "items": { "type": "string" } }
                        }
                    }
                },
                "orderBy": {
                    "type": "array",
                    "description": "Sort by groupBy field or metric alias",
                    "items": {
                        "type": "object",
                        "required": ["field"],
                        "properties": {
                            "field": { "type": "string", "description": "groupBy field or metric alias" },
                            "dir": { "type": "string", "enum": ["asc", "desc"], "default": "desc" }
                        }
                    }
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum rows to return (default 200, max 200)",
                    "minimum": 1,
                    "maximum": 200,
                    "default": 200
                }
            }
        }',
        'Use this tool to EXECUTE a validated analytics plan with aggregate metrics.

WHEN TO CALL:
- After analytics_validate_plan returns isValid=true
- When you need totals, breakdowns, or aggregations (count, sum, avg, etc.)

WORKFLOW:
1. catalog_search → find dataset
2. catalog_get_dataset → get full schema  
3. analytics_validate_plan → validate your plan
4. analytics_execute_plan → execute and get results (THIS TOOL)

INPUTS:
- datasetKey: from catalog_get_dataset
- metrics: array of {field, op, alias}
  - op: count, countDistinct, sum, avg, min, max
  - alias: optional output column name
- groupBy: optional array of field keys (max 4)
- where: optional filter conditions
- orderBy: optional, reference groupBy field or metric alias
- limit: max 200 rows

OUTPUT:
- meta: rowCount, truncated, durationMs, freshness
- columns: column definitions
- rows: aggregated data rows
- warnings: any execution warnings

SECURITY:
- Requires analytics.read role
- Tenant isolation enforced
- Field-level aggregation rules respected
- IMPORTANT: _roles is SERVER-INJECTED. Do NOT include _roles in your plan; the server handles authorization automatically.',
        'Execute a validated analytics plan with aggregate metrics (count, sum, avg, etc.) and return results.'
    );
END;
GO

-- analytics_execute_plan translations
IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'analytics_execute_plan' AND Language = 'en')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation (ToolName, Language, Instruction, Description)
    VALUES
    (
        'analytics_execute_plan',
        'en',
        'Execute a validated analytics plan. Call after analytics_validate_plan returns isValid=true. Supports count, sum, avg, min, max, countDistinct with groupBy and filters.',
        'Execute analytics aggregation queries and return totals/breakdowns.'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ToolCatalogTranslation WHERE ToolName = 'analytics_execute_plan' AND Language = 'vi')
BEGIN
    INSERT INTO dbo.ToolCatalogTranslation (ToolName, Language, Instruction, Description)
    VALUES
    (
        'analytics_execute_plan',
        'vi',
        'Thực thi kế hoạch phân tích đã được xác thực. Gọi sau khi analytics_validate_plan trả về isValid=true. Hỗ trợ count, sum, avg, min, max, countDistinct với groupBy và bộ lọc.',
        'Thực thi truy vấn tổng hợp phân tích và trả về tổng/phân tích chi tiết.'
    );
END;
GO

/*******************************************************************************
* PATCH 29.07: Enforce analytics.read role for all analytics tools
*******************************************************************************/
UPDATE dbo.ToolCatalog 
SET RequiredRoles = 'analytics.read'
WHERE ToolName IN ('catalog_search', 'catalog_get_dataset', 'analytics_validate_plan', 'analytics_execute_plan')
  AND (RequiredRoles IS NULL OR RequiredRoles <> 'analytics.read');
GO
