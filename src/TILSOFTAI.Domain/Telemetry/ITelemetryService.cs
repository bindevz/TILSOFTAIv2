using System.Diagnostics;

namespace TILSOFTAI.Domain.Telemetry
{
    public interface ITelemetryService
    {
        Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal);
        void AddEvent(string name, Dictionary<string, object>? attributes = null);
        void SetStatus(ActivityStatusCode code, string? description = null);
        void RecordException(Exception ex);
    }
}
