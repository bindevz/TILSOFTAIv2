using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TILSOFTAI.Domain.Logging;

namespace TILSOFTAI.Infrastructure.Logging
{
    public class LogEnricher
    {
        private readonly RequestDelegate _next;

        public LogEnricher(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Initialize the log context
            var logContext = new LogContext
            {
                TraceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
                SpanId = Activity.Current?.SpanId.ToString(),
                RequestId = context.TraceIdentifier,
                CorrelationId = context.Request.Headers["X-Correlation-ID"].ToString()
            };

            // If correlation ID is missing, usage RequestId as fallback
            if (string.IsNullOrEmpty(logContext.CorrelationId))
            {
                logContext.CorrelationId = logContext.RequestId;
            }

            LogContext.Current = logContext;

            await _next(context);
        }
    }
}
