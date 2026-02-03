namespace TILSOFTAI.Domain.Telemetry
{
    public static class TelemetryConstants
    {
        public const string ServiceName = "TILSOFTAI";
        
        public static class Spans
        {
            public const string ChatPipeline = "tilsoft.chat.pipeline";
            public const string ToolExecute = "tilsoft.tool.execute";
            public const string LlmRequest = "tilsoft.llm.request";
            public const string SqlExecute = "tilsoft.sql.execute";
        }

        public static class Attributes
        {
            public const string TenantId = "tilsoft.tenant_id";
            public const string UserId = "tilsoft.user_id";
            public const string ConversationId = "tilsoft.conversation_id";
            
            public const string ToolName = "tilsoft.tool.name";
            public const string ToolCategory = "tilsoft.tool.category";
            
            public const string LlmModel = "tilsoft.llm.model";
            public const string LlmPromptTokens = "tilsoft.llm.tokens.prompt";
            public const string LlmCompletionTokens = "tilsoft.llm.tokens.completion";
            public const string LlmTotalTokens = "tilsoft.llm.tokens.total";
            
            public const string SqlProcedure = "tilsoft.sql.procedure";
        }
    }
}
