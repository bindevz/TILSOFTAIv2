using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.SqlClient;
using OpenTelemetry.Instrumentation.StackExchangeRedis;
using OpenTelemetry.Instrumentation.Http;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Telemetry;

namespace TILSOFTAI.Infrastructure.Telemetry
{
    public static class OpenTelemetryConfigurator
    {
        public static void Configure(OpenTelemetryBuilder builder, OpenTelemetryOptions options)
        {
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(
                    options.ServiceName,
                    serviceVersion: options.ServiceVersion);

            builder.WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(TelemetryConstants.ServiceName)
                    .SetSampler(CreateSampler(options));

                if (options.EnableHttpClientInstrumentation)
                {
                    tracerProviderBuilder.AddHttpClientInstrumentation();
                }

                if (options.EnableSqlInstrumentation)
                {
                    tracerProviderBuilder.AddSqlClientInstrumentation();
                    /*(o =>
                    {
                        o.SetDbStatementForText = true;
                        o.RecordException = true;
                    });*/
                }
                
                /*
                if (options.EnableRedisInstrumentation)
                {
                    tracerProviderBuilder.AddRedisInstrumentation();
                }
                */

                tracerProviderBuilder.AddAspNetCoreInstrumentation();
                /*(o =>
                {
                     o.RecordException = true;
                });*/

                switch (options.ExporterType?.ToLowerInvariant())
                {
                    case "otlp":
                        tracerProviderBuilder.AddOtlpExporter(o =>
                        {
                            if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                            {
                                o.Endpoint = new Uri(options.OtlpEndpoint);
                            }
                        });
                        break;
                    case "console":
                        tracerProviderBuilder.AddConsoleExporter();
                        break;
                    case "none":
                        break;
                    default:
                        tracerProviderBuilder.AddConsoleExporter();
                        break;
                }
            });
        }

        private static OpenTelemetry.Trace.Sampler CreateSampler(OpenTelemetryOptions options)
        {
            return options.SamplerType switch
            {
                TraceSamplerType.AlwaysOn => new AlwaysOnSampler(),
                TraceSamplerType.AlwaysOff => new AlwaysOffSampler(),
                TraceSamplerType.Ratio => new TraceIdRatioBasedSampler(options.SamplingRatio),
                TraceSamplerType.ParentBased => new ParentBasedSampler(new TraceIdRatioBasedSampler(options.SamplingRatio)),
                _ => new AlwaysOnSampler()
            };
        }
    }
}
