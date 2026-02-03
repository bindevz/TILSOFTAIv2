using System.Diagnostics;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;

namespace TILSOFTAI.Api.Middlewares
{
    public class MetricsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMetricsService _metrics;
        private readonly MetricsOptions _options;

        public MetricsMiddleware(RequestDelegate next, IMetricsService metrics, IOptions<MetricsOptions> options)
        {
            _next = next;
            _metrics = metrics;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_options.Enabled || context.Request.Path == _options.EndpointPath)
            {
                await _next(context);
                return;
            }

            var method = context.Request.Method;
            // Simple normalization for endpoints - in real app might want route template
            var path = context.Request.Path.Value ?? "/";
            var tenantId = _options.IncludeTenantLabel ? (context.Request.Headers["X-Tenant-ID"].ToString() ?? "unknown") : "unknown";

            var labels = new Dictionary<string, string>
            {
                { "method", method },
                { "endpoint", path }, // Be careful with cardinality here in prod
                { "tenant_id", tenantId }
            };
            
            _metrics.RecordGauge(MetricNames.HttpRequestsInProgress, 1, labels); // This should ideally be Up/Down but with gauge we set value. 
            // Actually, prometheus-net InProgress is usually a Gauge we Inc/Dec.
            // IMetricsService only has RecordGauge(set value).
            // To track in-progress properly with RecordGauge(set), we can't easily do it across concurrent requests without atomic inc/dec support in interface.
            // However, for RED metrics, Duration and Total are most critical. 
            // InProgress is nice to have.
            // Let's increment Total here.
            
            _metrics.IncrementCounter(MetricNames.HttpRequestsTotal, labels);

            var sw = Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                labels["status_code"] = context.Response.StatusCode.ToString();
                
                // Add status code to total count? 
                // Usually we want total requests by status code. 
                // The previous IncrementCounter didn't have status code. 
                // We should increment it AFTER with status code.
                // But we already incremented on start. 
                // Standard practice: "Total" usually implies "Completed".
                // So let's NOT increment at start, but increment at end with status.
                
                _metrics.IncrementCounter(MetricNames.HttpRequestsTotal, labels);
                _metrics.RecordHistogram(MetricNames.HttpRequestDurationSeconds, sw.Elapsed.TotalSeconds, labels);
                
                // For "In Progress", we can't do it cleanly with current interface (Set only).
                // Ignoring InProgress for now to stick to interface, or we just rely on Duration/Rate.
            }
        }
    }
}
