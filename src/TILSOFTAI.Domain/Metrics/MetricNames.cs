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
        
        // SQL Connection Pool
        public const string SqlConnectionOpenTotal = "tilsoftai_sql_connection_open_total";
        public const string SqlConnectionTimeoutTotal = "tilsoftai_sql_connection_timeout_total";
        public const string SqlPoolActiveConnections = "tilsoftai_sql_pool_active_connections";
        public const string SqlPoolIdleConnections = "tilsoftai_sql_pool_idle_connections";
        
        // PATCH 31.05: Cache write background queue metrics
        public const string CacheWriteSuccessTotal = "tilsoftai_cache_write_success_total";
        public const string CacheWriteFailuresTotal = "tilsoftai_cache_write_failures_total";
        public const string CacheWriteDroppedTotal = "tilsoftai_cache_write_dropped_total";
        
        // PATCH 31.06: Governance pipeline metrics
        public const string GovernanceAllowTotal = "tilsoftai_governance_allow_total";
        public const string GovernanceDenyTotal = "tilsoftai_governance_deny_total";
        
        // PATCH 33.01: Streaming delta coalescing metrics
        public const string ChatStreamDeltasInTotal = "tilsoftai_chat_stream_deltas_in_total";
        public const string ChatStreamDeltasOutTotal = "tilsoftai_chat_stream_deltas_out_total";
        public const string ChatStreamDeltaFlushTotal = "tilsoftai_chat_stream_delta_flush_total";
        public const string ChatStreamDropTotal = "tilsoftai_chat_stream_drop_total";
    }
}
