# TILSOFTAI.Modules.Analytics

Deep Analytics module implementing Schema RAG workflow for LLM-driven data analysis.

## Overview

This module provides tools for LLM to:
1. **Discover schemas** via catalog tools (Schema RAG)
2. **Validate plans** before execution
3. **Execute analytics** via atomic engine

## Tools

| Tool | SP Name | Purpose |
|------|---------|---------|
| `catalog_search` | `ai_catalog_search` | Search datasets/fields by query |
| `catalog_get_dataset` | `ai_catalog_get_dataset` | Get full schema for a dataset |
| `analytics_validate_plan` | `ai_analytics_validate_plan` | Validate plan before execution |

## Workflow

```
User Question
    -> Normalize (deterministic)
    -> catalog_search (find datasets)
    -> catalog_get_dataset (get schema)
    -> Build atomic plan (LLM)
    -> analytics_validate_plan
    -> atomic_execute_plan
    -> Insight Assembly
    -> Response
```

## Configuration

```json
{
  "Analytics": {
    "MaxRows": 200,
    "MaxGroupBy": 4,
    "MaxMetrics": 3,
    "MaxJoins": 1,
    "MaxPlanRetries": 2,
    "EnableInsightCache": true
  }
}
```
