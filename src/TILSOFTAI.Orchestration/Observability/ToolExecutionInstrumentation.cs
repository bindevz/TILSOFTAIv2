using System.Diagnostics;
using TILSOFTAI.Domain.Telemetry;

namespace TILSOFTAI.Orchestration.Observability
{
    public class ToolExecutionInstrumentation
    {
        private readonly ITelemetryService _telemetry;

        public ToolExecutionInstrumentation(ITelemetryService telemetry)
        {
            _telemetry = telemetry;
        }

        public Activity? StartExecution(string toolName, string category)
        {
            var activity = _telemetry.StartActivity(TelemetryConstants.Spans.ToolExecute, ActivityKind.Internal);
            activity?.SetTag(TelemetryConstants.Attributes.ToolName, toolName);
            activity?.SetTag(TelemetryConstants.Attributes.ToolCategory, category);
            return activity;
        }
    }
}
