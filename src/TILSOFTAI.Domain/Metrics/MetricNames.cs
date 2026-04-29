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
        
        // Cache write background queue metrics
        public const string CacheWriteSuccessTotal = "tilsoftai_cache_write_success_total";
        public const string CacheWriteFailuresTotal = "tilsoftai_cache_write_failures_total";
        public const string CacheWriteDroppedTotal = "tilsoftai_cache_write_dropped_total";
        
        // Governance pipeline metrics
        public const string GovernanceAllowTotal = "tilsoftai_governance_allow_total";
        public const string GovernanceDenyTotal = "tilsoftai_governance_deny_total";
        
        // Streaming delta coalescing metrics
        public const string ChatStreamDeltasInTotal = "tilsoftai_chat_stream_deltas_in_total";
        public const string ChatStreamDeltasOutTotal = "tilsoftai_chat_stream_deltas_out_total";
        public const string ChatStreamDeltaFlushTotal = "tilsoftai_chat_stream_delta_flush_total";
        public const string ChatStreamDropTotal = "tilsoftai_chat_stream_drop_total";

        // supervisor-driven runtime observability
        public const string RuntimeSupervisorExecutionsTotal = "tilsoftai_runtime_supervisor_executions_total";
        public const string RuntimeNativeExecutionsTotal = "tilsoftai_runtime_native_executions_total";
        public const string RuntimeBridgeFallbackTotal = "tilsoftai_runtime_bridge_fallback_total";
        public const string RuntimeApprovalExecutionsTotal = "tilsoftai_runtime_approval_executions_total";
        public const string RuntimeCapabilityInvocationsTotal = "tilsoftai_runtime_capability_invocations_total";
        public const string RuntimeAdapterFailuresTotal = "tilsoftai_runtime_adapter_failures_total";
        public const string RuntimeExecutionDurationSeconds = "tilsoftai_runtime_execution_duration_seconds";

        // Agent Framework routing rollout and evaluation observability
        public const string AgentRoutingRequestsTotal = "tilsoftai_agent_routing_requests_total";
        public const string AgentRoutingHandledTotal = "tilsoftai_agent_routing_handled_total";
        public const string AgentRoutingFailuresTotal = "tilsoftai_agent_routing_failures_total";
        public const string AgentRoutingClarificationsTotal = "tilsoftai_agent_routing_clarifications_total";
        public const string AgentRoutingCandidateTools = "tilsoftai_agent_routing_candidate_tools";
        public const string AgentRoutingLatencySeconds = "tilsoftai_agent_routing_latency_seconds";
        public const string AgentRoutingStageLatencySeconds = "tilsoftai_agent_routing_stage_latency_seconds";
        public const string AgentRoutingCandidateCount = "tilsoftai_agent_routing_candidate_count";
        public const string AgentRoutingAdvertisedToolCount = "tilsoftai_agent_routing_advertised_tool_count";
        public const string AgentRoutingAgentDurationMs = "tilsoftai_agent_routing_agent_duration_ms";
        public const string AgentRoutingAnswerComposerDurationMs = "tilsoftai_agent_routing_answer_composer_duration_ms";
        public const string AgentRoutingRowCount = "tilsoftai_agent_routing_row_count";
        public const string AgentRoutingFallbackTotal = "tilsoftai_agent_routing_fallback_total";
        public const string AgentRoutingFollowUpTotal = "tilsoftai_agent_routing_follow_up_total";
        public const string AgentRoutingValidationFailureTotal = "tilsoftai_agent_routing_validation_failure_total";
        public const string CapabilityFacadeDurationMs = "tilsoftai_capability_facade_duration_ms";
        // platform catalog control-plane observability
        public const string PlatformCatalogSourceModeTotal = "tilsoftai_platform_catalog_source_mode_total";
        public const string PlatformCatalogMutationTotal = "tilsoftai_platform_catalog_mutations_total";
        public const string PlatformCatalogPromotionGateTotal = "tilsoftai_platform_catalog_promotion_gate_total";
        public const string PlatformCatalogCertificationEvidenceTotal = "tilsoftai_platform_catalog_certification_evidence_total";
    }
}
