namespace TILSOFTAI.Domain.Metrics
{
    public static class MetricNames
    {
        public const string HttpRequestsTotal = "tilsoftai_http_requests_total";
        public const string HttpRequestDurationSeconds = "tilsoftai_http_request_duration_seconds";
        public const string HttpRequestsInProgress = "tilsoftai_http_requests_in_progress";
        public const string ChatPipelineDurationSeconds = "tilsoftai_chat_pipeline_duration_seconds";
        public const string ToolExecutionsTotal = "tilsoftai_tool_executions_total";
        public const string ToolExecutionDurationSeconds = "tilsoftai_tool_execution_duration_seconds";
        public const string LlmRequestsTotal = "tilsoftai_llm_requests_total";
        public const string LlmRequestDurationSeconds = "tilsoftai_llm_request_duration_seconds";
        public const string LlmTokensTotal = "tilsoftai_llm_tokens_total";
        public const string ConversationsActive = "tilsoftai_conversations_active";
        public const string CacheHitsTotal = "tilsoftai_cache_hits_total";
        public const string CacheMissesTotal = "tilsoftai_cache_misses_total";
        public const string ErrorsTotal = "tilsoftai_errors_total";
        public const string RetryAttemptsTotal = "tilsoftai_retry_attempts_total";
        public const string RetryExhaustedTotal = "tilsoftai_retry_exhausted_total";
    }
}
