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
                { "tenant_id", tenantId },
                { "status_code", "0" } // Placeholder, will be set after request completes
            };
            
            // Note: We only increment HttpRequestsTotal AFTER the request completes
            // to ensure the status_code label is populated correctly

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
