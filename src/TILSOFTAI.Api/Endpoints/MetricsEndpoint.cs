using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using Prometheus;

namespace TILSOFTAI.Api.Endpoints
{
    public static class MetricsEndpoint
    {
        public static async Task HandleAsync(HttpContext context, IOptions<MetricsOptions> options)
        {
            if (!options.Value.Enabled)
            {
                context.Response.StatusCode = 404;
                return;
            }

            if (options.Value.RequireAuthentication && context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = 401;
                return;
            }
            
            // Check roles if configured
            if (options.Value.AllowedRoles.Length > 0 && options.Value.RequireAuthentication)
            {
                 bool hasRole = false;
                 foreach (var role in options.Value.AllowedRoles)
                 {
                     if (context.User.IsInRole(role))
                     {
                         hasRole = true;
                         break;
                     }
                 }
                 
                 if (!hasRole)
                 {
                     context.Response.StatusCode = 403;
                     return;
                 }
            }

            // Expose Prometheus metrics
            // We use the default registry for now as PrometheusMetricsService uses static Metrics class from prometheus-net
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain; version=0.0.4";
            
            await using var stream = context.Response.Body;
            await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream, context.RequestAborted);
        }
    }
}
