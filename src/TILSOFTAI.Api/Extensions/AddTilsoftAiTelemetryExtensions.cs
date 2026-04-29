using Microsoft.Extensions.Logging.Configuration;
using OpenTelemetry;
using TILSOFTAI.Api.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Audit;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Domain.Resilience;
using TILSOFTAI.Domain.Telemetry;
using TILSOFTAI.Infrastructure.Audit;
using TILSOFTAI.Infrastructure.Errors;
using TILSOFTAI.Infrastructure.Logging;
using TILSOFTAI.Infrastructure.Localization;
using TILSOFTAI.Infrastructure.Metrics;
using TILSOFTAI.Infrastructure.Observability;
using TILSOFTAI.Infrastructure.Resilience;
using TILSOFTAI.Infrastructure.Telemetry;
using TILSOFTAI.Orchestration.Observability;

namespace TILSOFTAI.Api.Extensions;

public static class AddTilsoftAiTelemetryExtensions
{
    public static IServiceCollection AddTilsoftAiTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        ConfigureOpenTelemetry(services, configuration);

        services.AddSingleton<AuditLogger>();
        services.AddSingleton<IAuditLogger>(sp => sp.GetRequiredService<AuditLogger>());
        services.AddSingleton<IAuditSink, SqlAuditSink>();
        services.AddSingleton<IAuditSink, FileAuditSink>();
        services.AddHostedService<AuditBackgroundService>();
        services.AddSingleton<IErrorCatalog, InMemoryErrorCatalog>();
        services.AddSingleton<TILSOFTAI.Orchestration.Observability.ILogRedactor, BasicLogRedactor>();

        services.AddSingleton<IMetricsService, PrometheusMetricsService>();
        services.AddSingleton<RuntimeMetricsCollector>();
        services.AddSingleton<CircuitBreakerRegistry>();
        services.AddSingleton<RetryPolicyRegistry>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<LlmInstrumentation>();
        services.AddSingleton<ToolExecutionInstrumentation>();

        services.AddSingleton<LogRedactor>();
        services.AddSingleton<TILSOFTAI.Infrastructure.Logging.ILogRedactor>(sp => sp.GetRequiredService<LogRedactor>());
        services.AddSingleton<JsonLogFormatter>();
        services.AddSingleton<StructuredLoggerProvider>();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<StructuredLoggerProvider>());
        });

        services.AddHostedService<ObservabilityPurgeHostedService>();
        services.AddHostedService<ErrorCatalogCoverageGuard>();
        services.AddTransient<CircuitBreakerDelegatingHandler>(sp =>
        {
            var registry = sp.GetRequiredService<CircuitBreakerRegistry>();
            var policy = registry.GetOrCreate("llm");
            return new CircuitBreakerDelegatingHandler(policy);
        });

        services.AddTransient<RetryDelegatingHandler>(sp =>
        {
            var registry = sp.GetRequiredService<RetryPolicyRegistry>();
            var policy = registry.GetOrCreate("llm");
            return new RetryDelegatingHandler(policy);
        });

        return services;
    }

    private static void ConfigureOpenTelemetry(IServiceCollection services, IConfiguration configuration)
    {
        var telemetryEnabled = configuration.GetValue<bool>("OpenTelemetry:Enabled");
        if (!telemetryEnabled)
        {
            return;
        }

        var builder = services.AddOpenTelemetry();
        var otelOptions = new OpenTelemetryOptions();
        configuration.GetSection(ConfigurationSectionNames.OpenTelemetry).Bind(otelOptions);

        OpenTelemetryConfigurator.Configure(builder, otelOptions);
    }
}
