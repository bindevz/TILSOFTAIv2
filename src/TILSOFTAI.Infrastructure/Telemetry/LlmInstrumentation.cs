using System.Diagnostics;
using TILSOFTAI.Domain.Telemetry;

namespace TILSOFTAI.Infrastructure.Telemetry
{
    public class LlmInstrumentation
    {
        private readonly ITelemetryService _telemetry;

        public LlmInstrumentation(ITelemetryService telemetry)
        {
            _telemetry = telemetry;
        }

        public Activity? StartRequest(string model)
        {
            var activity = _telemetry.StartActivity(TelemetryConstants.Spans.LlmRequest, ActivityKind.Client);
            activity?.SetTag(TelemetryConstants.Attributes.LlmModel, model);
            return activity;
        }

        public void RecordUsage(int inputTokens, int outputTokens, int totalTokens)
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                activity.SetTag(TelemetryConstants.Attributes.LlmPromptTokens, inputTokens);
                activity.SetTag(TelemetryConstants.Attributes.LlmCompletionTokens, outputTokens);
                activity.SetTag(TelemetryConstants.Attributes.LlmTotalTokens, totalTokens);
            }
        }
    }
}
