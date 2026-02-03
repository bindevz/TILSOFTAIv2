using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Middlewares
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        private readonly LoggingOptions _options;

        public RequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestLoggingMiddleware> logger,
            IOptions<LoggingOptions> options)
        {
            _next = next;
            _logger = logger;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_options.EnableRequestResponseLogging)
            {
                await _next(context);
                return;
            }

            // Skip health checks to avoid noise
            if (context.Request.Path.StartsWithSegments("/health"))
            {
                await _next(context);
                return;
            }

            // Basic sampling for high volume
            if (_options.SamplingRate < 1.0 && new Random().NextDouble() > _options.SamplingRate)
            {
                await _next(context);
                return;
            }

            var start = Stopwatch.GetTimestamp();
            
            _logger.LogInformation(
                "Request started: {Method} {Path}{Query}",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString);

            try
            {
                await _next(context);
            }
            finally
            {
                var elapsedMs = GetElapsedMilliseconds(start, Stopwatch.GetTimestamp());
                var statusCode = context.Response.StatusCode;

                if (statusCode >= 500)
                {
                    _logger.LogError(
                        "Request failed: {Method} {Path} responded {StatusCode} in {Elapsed}ms",
                        context.Request.Method,
                        context.Request.Path,
                        statusCode,
                        elapsedMs);
                }
                else
                {
                    _logger.LogInformation(
                        "Request completed: {Method} {Path} responded {StatusCode} in {Elapsed}ms",
                        context.Request.Method,
                        context.Request.Path,
                        statusCode,
                        elapsedMs);
                }
            }
        }

        private static double GetElapsedMilliseconds(long start, long stop)
        {
            return (stop - start) * 1000 / (double)Stopwatch.Frequency;
        }
    }
}
