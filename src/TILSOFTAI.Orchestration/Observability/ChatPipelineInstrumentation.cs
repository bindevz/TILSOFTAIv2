using System.Diagnostics;
using TILSOFTAI.Domain.Telemetry;

namespace TILSOFTAI.Orchestration.Observability
{
    public class ChatPipelineInstrumentation
    {
        private readonly ITelemetryService _telemetry;

        public ChatPipelineInstrumentation(ITelemetryService telemetry)
        {
            _telemetry = telemetry;
        }

        public Activity? StartPipeline(string conversationId, string tenantId)
        {
            var activity = _telemetry.StartActivity(TelemetryConstants.Spans.ChatPipeline, ActivityKind.Server);
            activity?.SetTag(TelemetryConstants.Attributes.ConversationId, conversationId);
            activity?.SetTag(TelemetryConstants.Attributes.TenantId, tenantId);
            return activity;
        }
    }
}
