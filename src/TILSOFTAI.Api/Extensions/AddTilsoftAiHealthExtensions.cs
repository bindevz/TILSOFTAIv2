using TILSOFTAI.Api.Health;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

namespace TILSOFTAI.Api.Extensions;

public static class AddTilsoftAiHealthExtensions
{
    public static IServiceCollection AddTilsoftAiHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");

        var healthChecksBuilder = services.AddHealthChecks()
            .AddCheck<SqlHealthCheck>("sql", tags: new[] { "ready", "db" })
            .AddCheck<CircuitBreakerHealthCheck>("circuits", tags: new[] { "ready", "resilience" })
            .AddCheck<PlatformCatalogHealthCheck>("platform-catalog", tags: new[] { "ready", "catalog" })
            .AddCheck<NativeRuntimeHealthCheck>("native-runtime", tags: new[] { "ready", "runtime", "native" })
            .AddCheck<OfficialAgentFrameworkHealthCheck>("official-agent-framework", tags: new[] { "ready", "runtime", "agent-framework" });

        if (redisEnabled)
        {
            healthChecksBuilder.AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready", "cache" });
        }

        return services;
    }
}
