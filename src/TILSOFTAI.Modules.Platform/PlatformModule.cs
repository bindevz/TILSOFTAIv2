using TILSOFTAI.Orchestration.Modules;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.Modules.Platform.Tools;

namespace TILSOFTAI.Modules.Platform;

public sealed class PlatformModule : ITilsoftModule
{
    public string Name => "Platform";

    public void Register(IToolRegistry toolRegistry, INamedToolHandlerRegistry handlerRegistry)
    {
        if (toolRegistry is null)
        {
            throw new ArgumentNullException(nameof(toolRegistry));
        }

        if (handlerRegistry is null)
        {
            throw new ArgumentNullException(nameof(handlerRegistry));
        }

        // tool.list - List enabled tools
        toolRegistry.Register(new ToolDefinition
        {
            Name = "tool.list",
            Description = "List available tools for the tenant.",
            Instruction = Instructions.ToolList,
            JsonSchema = Schemas.EmptySchema,
            SpName = "ai_tool_list",
            RequiredRoles = Array.Empty<string>(),
            Module = Name,
            IsEnabled = true
        });

        // atomic_execute_plan - Execute atomic data plan
        toolRegistry.Register(new ToolDefinition
        {
            Name = "atomic_execute_plan",
            Description = "Execute an atomic plan against the dataset catalog and return meta/columns/rows.",
            Instruction = Instructions.AtomicExecutePlan,
            JsonSchema = Schemas.AtomicPlanSchema,
            SpName = "ai_atomic_execute_plan",
            RequiredRoles = Array.Empty<string>(),
            Module = Name,
            IsEnabled = true
        });

        // diagnostics_run - Run diagnostics rule
        toolRegistry.Register(new ToolDefinition
        {
            Name = "diagnostics_run",
            Description = "Execute diagnostics rule and return validation results.",
            Instruction = Instructions.DiagnosticsRun,
            JsonSchema = Schemas.DiagnosticsSchema,
            SpName = "ai_diagnostics_run",
            RequiredRoles = Array.Empty<string>(),
            Module = Name,
            IsEnabled = true
        });

        // action_request_write - Create pending write action request
        toolRegistry.Register(new ToolDefinition
        {
            Name = "action_request_write",
            Description = "Request a write action for human approval (does not execute immediately).",
            Instruction = Instructions.ActionRequestWrite,
            JsonSchema = Schemas.ActionRequestSchema,
            SpName = "ai_action_request_write",
            RequiredRoles = Array.Empty<string>(),
            Module = Name,
            IsEnabled = true
        });

        // Register handlers
        handlerRegistry.Register("tool.list", typeof(ToolListToolHandler));
        handlerRegistry.Register("atomic_execute_plan", typeof(AtomicExecutePlanToolHandler));
        handlerRegistry.Register("diagnostics_run", typeof(DiagnosticsRunToolHandler));
        handlerRegistry.Register("action_request_write", typeof(ActionRequestWriteToolHandler));
    }

    private static class Schemas
    {
        public const string EmptySchema = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";
        public const string AtomicPlanSchema = "{\"type\":\"object\",\"required\":[\"plan\"],\"properties\":{\"plan\":{\"type\":\"object\"}},\"additionalProperties\":false}";
        public const string DiagnosticsSchema = "{\"type\":\"object\",\"required\":[\"module\",\"ruleKey\"],\"properties\":{\"module\":{\"type\":\"string\"},\"ruleKey\":{\"type\":\"string\"},\"inputJson\":{\"type\":[\"string\",\"null\"]}},\"additionalProperties\":false}";
        public const string ActionRequestSchema = "{\"type\":\"object\",\"required\":[\"proposedToolName\",\"proposedSpName\",\"argsJson\"],\"properties\":{\"proposedToolName\":{\"type\":\"string\"},\"proposedSpName\":{\"type\":\"string\"},\"argsJson\":{\"type\":\"string\"}},\"additionalProperties\":false}";
    }

    private static class Instructions
    {
        public const string ToolList = "Return the list of enabled tools and their schemas.";
        public const string AtomicExecutePlan = "Execute an atomic data plan. Provide plan with datasetKey, select fields, optional where/groupBy/orderBy/limit/offset/timeRange/drilldown. Tenant scope is enforced.";
        public const string DiagnosticsRun = "Run a diagnostics rule to validate data or configuration. Provide module name, ruleKey, and optional inputJson. Returns validation results.";
        public const string ActionRequestWrite = "Request a write action for human approval. Provide proposedToolName, proposedSpName, and argsJson. This does NOT execute the action immediately - it creates a pending request for approval. Returns actionId and status.";
    }
}
