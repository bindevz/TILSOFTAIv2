using System.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TILSOFTAI.Api.Auth;
using TILSOFTAI.Api.Health;
using TILSOFTAI.Api.Middlewares;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Api.Tools;
using TILSOFTAI.Domain.Caching;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Sensitivity;
using TILSOFTAI.Domain.Security;
using TILSOFTAI.Infrastructure.Actions;
using TILSOFTAI.Infrastructure.Caching;
using TILSOFTAI.Infrastructure.ExecutionContext;
using TILSOFTAI.Infrastructure.Errors;
using TILSOFTAI.Infrastructure.Normalization;
using TILSOFTAI.Infrastructure.Modules;
using TILSOFTAI.Infrastructure.Conversations;
using TILSOFTAI.Infrastructure.Atomic;
using TILSOFTAI.Infrastructure.Llm;
using TILSOFTAI.Infrastructure.Metadata;
using TILSOFTAI.Infrastructure.Prompting;
using TILSOFTAI.Infrastructure.Sensitivity;
using TILSOFTAI.Infrastructure.Sql;
using TILSOFTAI.Infrastructure.Tools;
using TILSOFTAI.Orchestration;
using TILSOFTAI.Orchestration.Actions;
using TILSOFTAI.Orchestration.Caching;
using TILSOFTAI.Orchestration.Compaction;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Normalization;
using TILSOFTAI.Orchestration.Policies;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Atomic;
using TILSOFTAI.Orchestration.Planning;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.Modules.Core.Tools;
using TILSOFTAI.Infrastructure.Observability;
using TILSOFTAI.Orchestration.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace TILSOFTAI.Api.Extensions;

public static class AddTilsoftAiExtensions
{
    public static IServiceCollection AddTilsoftAi(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterOptions(services, configuration);
        ConfigureOpenTelemetry(services, configuration);

        services.AddSingleton<ExecutionContextAccessor>();
        services.AddSingleton<IExecutionContextAccessor>(sp => sp.GetRequiredService<ExecutionContextAccessor>());
        services.AddTransient<ExecutionContextMiddleware>();
        services.AddSingleton<IdentityResolutionPolicy>();
        services.AddSingleton<IErrorCatalog, InMemoryErrorCatalog>();
        services.AddSingleton<ILogRedactor, BasicLogRedactor>();
        services.AddSingleton<ISensitivityClassifier, BasicSensitivityClassifier>();
        services.AddSingleton<ISqlErrorLogWriter, SqlErrorLogWriter>();
        services.AddSingleton<ChatStreamEnvelopeFactory>();
        services.AddHttpClient("jwks", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AuthOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.JwksRequestTimeoutSeconds));
        });
        services.AddSingleton<JwtSigningKeyProvider>();
        services.AddSingleton<IJwtSigningKeyProvider>(sp => sp.GetRequiredService<JwtSigningKeyProvider>());
        services.AddHostedService<JwtSigningKeyRefreshHostedService>();

        services.AddOrchestrationEngine();
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<INamedToolHandlerRegistry>(sp =>
        {
            var registry = new NamedToolHandlerRegistry();
            registry.Register("atomic_execute_plan", typeof(AtomicExecutePlanToolHandler));
            return registry;
        });
        services.AddSingleton<ToolCatalogSyncService>();
        services.AddSingleton<IToolCatalogResolver>(sp => sp.GetRequiredService<ToolCatalogSyncService>());
        services.AddSingleton<IJsonSchemaValidator, RealJsonSchemaValidator>();
        services.AddSingleton<ToolGovernance>();
        services.AddSingleton<ToolResultCompactor>();
        services.AddSingleton<SqlToolHandler>();
        services.AddSingleton<DiagnosticsToolHandler>();
        services.AddSingleton<IToolHandler, ToolHandlerRouter>();
        services.AddSingleton<ISqlExecutor, SqlExecutor>();
        services.AddSingleton<SqlContractValidator>();
        services.AddHostedService<SqlContractValidatorHostedService>();
        services.AddSingleton<IConversationStore, SqlConversationStore>();
        services.AddSingleton<IActionRequestStore, SqlActionRequestStore>();
        services.AddSingleton<ActionApprovalService>();
        services.AddSingleton<CacheStampedeGuard>();
        services.AddSingleton<SemanticCache>();
        services.AddHttpClient<OpenAiEmbeddingClient>();
        services.AddSingleton<IEmbeddingClient>(sp => sp.GetRequiredService<OpenAiEmbeddingClient>());
        services.AddSingleton<SqlVectorSemanticCache>();
        services.AddSingleton<ISemanticCache>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SemanticCacheOptions>>().Value;
            if (string.Equals(options.Mode, "SqlVector", StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<SqlVectorSemanticCache>();
            }
            return sp.GetRequiredService<SemanticCache>();
        });
        services.AddSingleton<INormalizationRuleProvider, SqlNormalizationRuleProvider>();
        services.AddSingleton<INormalizationService, NormalizationService>();
        // Context Pack Providers (Composite pattern)
        services.AddSingleton<MetadataDictionaryContextPackProvider>();
        services.AddSingleton<ToolCatalogContextPackProvider>();
        services.AddSingleton<AtomicCatalogContextPackProvider>();
        services.AddSingleton<IContextPackProvider>(sp => new CompositeContextPackProvider(new IContextPackProvider[]
        {
            sp.GetRequiredService<MetadataDictionaryContextPackProvider>(),
            sp.GetRequiredService<ToolCatalogContextPackProvider>(),
            sp.GetRequiredService<AtomicCatalogContextPackProvider>()
        }));
        services.AddSingleton<TokenBudgetPolicy>();
        services.AddSingleton<ContextPackBudgeter>();
        services.AddSingleton<RecursionPolicy>();
        services.AddSingleton<PromptBuilder>();
        services.AddHttpClient<OpenAiCompatibleLlmClient>();
        services.AddSingleton<ILlmClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            return string.Equals(options.Provider, "OpenAiCompatible", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<OpenAiCompatibleLlmClient>()
                : sp.GetRequiredService<NullLlmClient>();
        });
        services.AddSingleton<NullLlmClient>();
        services.AddSingleton<IAtomicCatalogProvider, SqlAtomicCatalogProvider>();
        services.AddSingleton<PlanOptimizer>();
        services.AddSingleton<AtomicDataEngine>();

        services.AddSingleton<IModuleLoader, ModuleLoader>();
        services.AddHostedService<ModuleLoaderHostedService>();
        services.AddHostedService<ObservabilityPurgeHostedService>();

        ConfigureRedis(services, configuration);
        ConfigureAuthentication(services, configuration);

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        
        services.AddRateLimiter(options =>
        {
            var rateLimitOpts = configuration.GetSection("RateLimit").Get<RateLimitOptions>() 
                             ?? new RateLimitOptions();
            
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var factory = new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = rateLimitOpts.PermitLimit,
                    QueueLimit = rateLimitOpts.QueueLimit,
                    Window = TimeSpan.FromSeconds(rateLimitOpts.WindowSeconds)
                };
                
                var partitionKey = httpContext.User.Identity?.Name 
                                   ?? httpContext.Connection.RemoteIpAddress?.ToString() 
                                   ?? "anonymous";
                                   
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => factory);
            });
        });

        services.AddControllers();
        services.AddSignalR();
        services.AddHealthChecks()
            .AddCheck<SqlHealthCheck>("sql", tags: new[] { "ready" })
            .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" });

        return services;
    }

    private static void RegisterOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SqlOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Sql))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Sql:ConnectionString is required.")
            .Validate(options => options.CommandTimeoutSeconds > 0, "Sql:CommandTimeoutSeconds must be > 0.")
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Redis))
            .Validate(options => options.DefaultTtlMinutes >= 30, "Redis:DefaultTtlMinutes must be >= 30.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Redis:ConnectionString is required when Redis is enabled.")
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Auth))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Auth:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Auth:Audience is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.JwksUrl), "Auth:JwksUrl is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.TenantClaimName), "Auth:TenantClaimName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.UserIdClaimName), "Auth:UserIdClaimName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.TrustedGatewayClaimName), "Auth:TrustedGatewayClaimName is required.")
            .Validate(options => options.JwksRefreshIntervalMinutes > 0, "Auth:JwksRefreshIntervalMinutes must be > 0.")
            .Validate(options => options.JwksRefreshFailureBackoffSeconds > 0, "Auth:JwksRefreshFailureBackoffSeconds must be > 0.")
            .Validate(options => options.JwksRefreshMaxBackoffSeconds >= options.JwksRefreshFailureBackoffSeconds,
                "Auth:JwksRefreshMaxBackoffSeconds must be >= Auth:JwksRefreshFailureBackoffSeconds.")
            .Validate(options => options.JwksRequestTimeoutSeconds > 0, "Auth:JwksRequestTimeoutSeconds must be > 0.")
            .ValidateOnStart();

        services.AddOptions<ChatOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Chat))
            .Validate(options => options.MaxSteps > 0, "Chat:MaxSteps must be > 0.")
            .Validate(options => options.MaxTokens > 0, "Chat:MaxTokens must be > 0.")
            .Validate(options => options.MaxToolCallsPerRequest > 0, "Chat:MaxToolCallsPerRequest must be > 0.")
            .Validate(options => options.MaxRecursiveDepth > 0, "Chat:MaxRecursiveDepth must be > 0.")
            .Validate(options => options.MaxInputChars > 0, "Chat:MaxInputChars must be > 0.")
            .Validate(options => options.MaxRequestBytes > 0, "Chat:MaxRequestBytes must be > 0.")
            .ValidateOnStart();

        services.AddOptions<LocalizationOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Localization))
            .Validate(options => !string.IsNullOrWhiteSpace(options.DefaultLanguage), "Localization:DefaultLanguage is required.")
            .ValidateOnStart();

        services.AddOptions<GovernanceOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Governance))
            .Validate(options => options.ModelCallableSpPrefix == "ai_",
                "Governance:ModelCallableSpPrefix must be 'ai_'.")
            .Validate(options => options.InternalSpPrefix == "app_",
                "Governance:InternalSpPrefix must be 'app_'.")
            .ValidateOnStart();

        services.AddOptions<ModulesOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Modules))
            .ValidateOnStart();

        services.AddOptions<ObservabilityOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Observability))
            .ValidateOnStart();

        services.AddOptions<SensitiveDataOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.SensitiveData))
            .Validate(options => Enum.IsDefined(typeof(SensitiveHandlingMode), options.HandlingMode),
                "SensitiveData:HandlingMode must be Redact, MetadataOnly, or DisablePersistence.")
            .ValidateOnStart();

        services.AddOptions<ErrorHandlingOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.ErrorHandling))
            .Validate(options => options.MaxDetailLength > 0, "ErrorHandling:MaxDetailLength must be > 0.")
            .ValidateOnStart();

        services.AddOptions<ToolCatalogContextPackOptions>()
            .Bind(configuration.GetSection("ToolCatalogContextPack"))
            .Validate(options => options.MaxTools > 0, "ToolCatalogContextPack:MaxTools must be > 0.")
            .Validate(options => options.MaxTotalTokens > 0, "ToolCatalogContextPack:MaxTotalTokens must be > 0.")
            .Validate(options => options.MaxInstructionTokensPerTool > 0, "ToolCatalogContextPack:MaxInstructionTokensPerTool must be > 0.")
            .Validate(options => options.MaxDescriptionTokensPerTool > 0, "ToolCatalogContextPack:MaxDescriptionTokensPerTool must be > 0.")
            .ValidateOnStart();

        services.AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.OpenTelemetry))
            .ValidateOnStart();

        services.AddOptions<SemanticCacheOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.SemanticCache))
            .ValidateOnStart();

        services.AddOptions<AtomicOptions>()
            .Bind(configuration.GetSection("Atomic"))
            .Validate(options => options.MaxLimit > 0, "Atomic:MaxLimit must be > 0.")
            .Validate(options => options.MaxJoins > 0, "Atomic:MaxJoins must be > 0.")
            .Validate(options => options.MaxTimeRangeDays > 0, "Atomic:MaxTimeRangeDays must be > 0.")
            .ValidateOnStart();

        services.AddOptions<StreamingOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Streaming))
            .Validate(options => options.ChannelCapacity > 0, "Streaming:ChannelCapacity must be > 0.")
            .ValidateOnStart();

        services.AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Llm))
            .ValidateOnStart();

        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection("RateLimit"))
            .Validate(options => options.PermitLimit > 0, "RateLimit:PermitLimit must be > 0.")
            .Validate(options => options.WindowSeconds > 0, "RateLimit:WindowSeconds must be > 0.")
            .Validate(options => options.QueueLimit >= 0, "RateLimit:QueueLimit must be >= 0.")
            .ValidateOnStart();
    }

    private static void ConfigureRedis(IServiceCollection services, IConfiguration configuration)
    {
        var redisOptions = configuration.GetSection(ConfigurationSectionNames.Redis).Get<RedisOptions>() ?? new RedisOptions();

        if (redisOptions.Enabled)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisOptions.ConnectionString;
            });

            services.AddSingleton<IRedisCacheProvider>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
                var cache = sp.GetRequiredService<IDistributedCache>();
                return new RedisCacheProvider(cache, TimeSpan.FromMinutes(options.DefaultTtlMinutes));
            });
        }
        else
        {
            services.AddSingleton<IRedisCacheProvider, NullRedisCacheProvider>();
        }
    }

    private static void ConfigureAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var authOptions = configuration.GetSection(ConfigurationSectionNames.Auth).Get<AuthOptions>() ?? new AuthOptions();
        services.AddTilsoftJwtAuthentication(Options.Create(authOptions));
    }

    private static void ConfigureOpenTelemetry(IServiceCollection services, IConfiguration configuration)
    {
        var telemetryOptions = configuration.GetSection(ConfigurationSectionNames.OpenTelemetry)
            .Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();

        if (!telemetryOptions.Enabled)
        {
            return;
        }

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                var resourceBuilder = ResourceBuilder.CreateDefault()
                    .AddService(
                        telemetryOptions.ServiceName,
                        serviceVersion: telemetryOptions.ServiceVersion);

                builder.SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddSqlClientInstrumentation();

                var exporterType = (telemetryOptions.ExporterType ?? "console").Trim().ToLowerInvariant();
                switch (exporterType)
                {
                    case "otlp":
                        builder.AddOtlpExporter(options =>
                        {
                            if (!string.IsNullOrWhiteSpace(telemetryOptions.OtlpEndpoint))
                            {
                                options.Endpoint = new Uri(telemetryOptions.OtlpEndpoint);
                            }
                        });
                        break;
                    case "console":
                        builder.AddConsoleExporter();
                        break;
                    case "none":
                        break;
                    default:
                        builder.AddConsoleExporter();
                        break;
                }
            });
    }
}
