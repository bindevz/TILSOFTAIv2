using System.Diagnostics;
using TILSOFTAI.Domain.Telemetry;

namespace TILSOFTAI.Infrastructure.Telemetry
{
    public class TelemetryService : ITelemetryService
    {
        private static readonly ActivitySource _activitySource = new(TelemetryConstants.ServiceName);

        public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        {
            var activity = _activitySource.StartActivity(name, kind);
            
            // Auto-enrich with ambient LogContext if available (correlation props)
            // Note: Since LogContext uses AsyncLocal, its values might already be propagated if Activity mirrors it.
            // But we can ensure standard attributes here.
             if (activity != null)
            {
                var context = Domain.Logging.LogContext.Current;
                if (!string.IsNullOrEmpty(context.TenantId)) activity.SetTag(TelemetryConstants.Attributes.TenantId, context.TenantId);
                if (!string.IsNullOrEmpty(context.UserId)) activity.SetTag(TelemetryConstants.Attributes.UserId, context.UserId);
                if (!string.IsNullOrEmpty(context.ConversationId)) activity.SetTag(TelemetryConstants.Attributes.ConversationId, context.ConversationId);
            }

            return activity;
        }

        public void AddEvent(string name, Dictionary<string, object>? attributes = null)
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                var tags = new ActivityTagsCollection();
                if (attributes != null)
                {
                    foreach (var kvp in attributes)
                    {
                        tags.Add(kvp.Key, kvp.Value);
                    }
                }
                activity.AddEvent(new ActivityEvent(name, tags: tags));
            }
        }

        public void SetStatus(ActivityStatusCode code, string? description = null)
        {
            Activity.Current?.SetStatus(code, description);
        }

        public void RecordException(Exception ex)
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                var tags = new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message },
                    { "exception.stacktrace", ex.ToString() }
                };
                activity.AddEvent(new ActivityEvent("exception", tags: tags));
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
        }
    }
}
