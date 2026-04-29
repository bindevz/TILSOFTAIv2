using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Filters;
using TILSOFTAI.Api.Hubs;
using TILSOFTAI.Api.Middlewares;
using TILSOFTAI.Api.Options;
using TILSOFTAI.Api.Streaming;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Sensitivity;
using TILSOFTAI.Domain.Security;
using TILSOFTAI.Domain.Validation;
using TILSOFTAI.Infrastructure.Catalog;
using TILSOFTAI.Infrastructure.ExecutionContext;
using TILSOFTAI.Infrastructure.Http;
using TILSOFTAI.Infrastructure.Localization;
using TILSOFTAI.Infrastructure.Normalization;
using TILSOFTAI.Infrastructure.Sensitivity;
using TILSOFTAI.Infrastructure.Validation;
using TILSOFTAI.Orchestration;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Compaction;
using TILSOFTAI.Orchestration.Normalization;
using TILSOFTAI.Orchestration.Policies;

namespace TILSOFTAI.Api.Extensions;

public static class AddTilsoftAiOrchestrationExtensions
{
    public static IServiceCollection AddTilsoftAiOrchestration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ExecutionContextAccessor>();
        services.AddSingleton<IExecutionContextAccessor>(sp => sp.GetRequiredService<ExecutionContextAccessor>());
        services.AddTransient<ExecutionContextMiddleware>();
        services.AddSingleton<IdentityResolutionPolicy>();

        services.AddSingleton<PromptInjectionDetector>();
        services.AddSingleton<IInputValidator, InputValidator>();
        services.AddScoped<InputValidationFilter>();
        services.AddSingleton<ISensitivityClassifier, BasicSensitivityClassifier>();
        services.AddSingleton<ChatStreamEnvelopeFactory>();

        services.AddHttpClient("jwks", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AuthOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.JwksRequestTimeoutSeconds));
        });

        services.AddSingleton<IPlatformCatalogProvider, FilePlatformCatalogProvider>();
        services.AddSingleton<IPlatformCatalogMutationStore, SqlPlatformCatalogMutationStore>();
        services.AddSingleton<IPlatformCatalogControlPlane, PlatformCatalogControlPlane>();
        services.AddSingleton<IPlatformCatalogCertificationStore, SqlPlatformCatalogCertificationStore>();
        services.AddSingleton<IPlatformCatalogArtifactProvider, FileSystemCatalogArtifactProvider>();
        services.AddSingleton<IPlatformCatalogTrustStoreRecoveryStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CatalogCertificationOptions>>().Value;
            return string.Equals(options.SignerTrustStoreBackupBackend, "managed_sql", StringComparison.OrdinalIgnoreCase)
                ? new SqlPlatformCatalogTrustStoreRecoveryStorage(sp.GetRequiredService<IOptions<SqlOptions>>())
                : new FileSystemPlatformCatalogTrustStoreRecoveryStorage(sp.GetRequiredService<IOptions<CatalogCertificationOptions>>());
        });
        services.AddSingleton<IPlatformCatalogSignerTrustStore, FileSystemPlatformCatalogSignerTrustStore>();
        services.AddSingleton<IPlatformCatalogSignatureVerifier, RsaPlatformCatalogSignatureVerifier>();
        services.AddSingleton<IPlatformCatalogEvidenceVerifier, PlatformCatalogEvidenceVerifier>();
        services.AddSingleton<IPlatformCatalogPromotionManifestStore, SqlPlatformCatalogPromotionManifestStore>();
        services.AddSingleton<IPlatformCatalogArchiveStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CatalogCertificationOptions>>().Value;
            return string.Equals(options.DossierArchiveBackend, "managed_sql", StringComparison.OrdinalIgnoreCase)
                ? new SqlPlatformCatalogArchiveStorage(sp.GetRequiredService<IOptions<SqlOptions>>())
                : string.Equals(options.DossierArchiveBackend, "filesystem", StringComparison.OrdinalIgnoreCase)
                    ? new FileSystemPlatformCatalogArchiveStorage(sp.GetRequiredService<IOptions<CatalogCertificationOptions>>())
                    : new MirroredPlatformCatalogArchiveStorage(sp.GetRequiredService<IOptions<CatalogCertificationOptions>>());
        });
        services.AddSingleton<IPlatformCatalogDossierArchiveService, FileSystemPlatformCatalogDossierArchiveService>();
        services.AddSingleton<IPlatformCatalogPromotionManifestService, PlatformCatalogPromotionManifestService>();
        services.AddSingleton<IPlatformCatalogPromotionGate, PlatformCatalogPromotionGate>();
        services.AddHostedService<PlatformCatalogStartupReporter>();
        services.AddSingleton<ConfigurationExternalConnectionCatalog>();
        services.AddSingleton<PlatformExternalConnectionCatalog>();
        services.AddSingleton<IExternalConnectionCatalog, CompositeExternalConnectionCatalog>();

        services.AddSupervisorRuntime();
        services.AddSingleton<ToolResultCompactor>();
        services.AddSingleton<INormalizationRuleProvider, SqlNormalizationRuleProvider>();
        services.AddSingleton<INormalizationService, NormalizationService>();
        services.AddSingleton<RecursionPolicy>();

        services.AddSingleton<IConfigureOptions<RateLimiterOptions>, ConfigureRateLimiterOptions>();
        services.AddRateLimiter(_ => { });
        services.AddControllers();

        services.AddSingleton<HubIdentityResolutionPolicy>();
        services.AddSingleton<ExecutionContextHubFilter>();
        services.Configure<HubOptions>(options =>
        {
            options.AddFilter<ExecutionContextHubFilter>();
        });
        services.AddSignalR();

        var secretsProvider = configuration["Secrets:Provider"] ?? "Environment";

        services.AddSingleton<TILSOFTAI.Domain.Secrets.ISecretProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SecretsOptions>>();
            var cache = sp.GetRequiredService<IMemoryCache>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            TILSOFTAI.Domain.Secrets.ISecretProvider provider = secretsProvider switch
            {
                "AzureKeyVault" => new TILSOFTAI.Infrastructure.Secrets.AzureKeyVaultSecretProvider(
                    options, cache, loggerFactory.CreateLogger<TILSOFTAI.Infrastructure.Secrets.AzureKeyVaultSecretProvider>()),
                _ => new TILSOFTAI.Infrastructure.Secrets.EnvironmentSecretProvider()
            };

            return new TILSOFTAI.Infrastructure.Secrets.CachingSecretProvider(provider, cache, options);
        });

        return services;
    }
}
