using TILSOFTAI.Api.Options;
using TILSOFTAI.Domain.Caching;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Resilience;
using TILSOFTAI.Domain.Sensitivity;
using TILSOFTAI.Infrastructure.Catalog;
using TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;
using TILSOFTAI.Orchestration.Answering.Narration;
using TILSOFTAI.Orchestration.Caching;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Policies;

namespace TILSOFTAI.Api.Extensions;

public static class AddTilsoftAiOptionsExtensions
{
    public static IServiceCollection AddTilsoftAiOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SqlOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Sql))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Sql:ConnectionString is required.")
            .Validate(options => options.CommandTimeoutSeconds > 0, "Sql:CommandTimeoutSeconds must be > 0.")
            .Validate(options => options.MinPoolSize >= 0, "Sql:MinPoolSize must be >= 0.")
            .Validate(options => options.MaxPoolSize >= options.MinPoolSize, "Sql:MaxPoolSize must be >= MinPoolSize.")
            .Validate(options => options.ConnectionTimeoutSeconds > 0, "Sql:ConnectionTimeoutSeconds must be > 0.")
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Redis))
            .Validate(options => options.DefaultTtlMinutes >= 30, "Redis:DefaultTtlMinutes must be >= 30.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Redis:ConnectionString is required when Redis is enabled.")
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Auth))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Issuer), "Auth:Issuer is required when Auth:Enabled=true.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Audience), "Auth:Audience is required when Auth:Enabled=true.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.TenantClaimName), "Auth:TenantClaimName is required when Auth:Enabled=true.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.UserIdClaimName), "Auth:UserIdClaimName is required when Auth:Enabled=true.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.TrustedGatewayClaimName), "Auth:TrustedGatewayClaimName is required when Auth:Enabled=true.")
            .Validate(options => options.JwksRefreshIntervalMinutes > 0, "Auth:JwksRefreshIntervalMinutes must be > 0.")
            .Validate(options => options.JwksRefreshFailureBackoffSeconds > 0, "Auth:JwksRefreshFailureBackoffSeconds must be > 0.")
            .Validate(options => options.JwksRefreshMaxBackoffSeconds >= options.JwksRefreshFailureBackoffSeconds,
                "Auth:JwksRefreshMaxBackoffSeconds must be >= Auth:JwksRefreshFailureBackoffSeconds.")
            .Validate(options => options.JwksRequestTimeoutSeconds > 0, "Auth:JwksRequestTimeoutSeconds must be > 0.")
            .ValidateOnStart();

        services.AddOptions<AiRoutingOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.AiRouting))
            .Validate(options => options.MaxCandidateDomains > 0, "AiRouting:MaxCandidateDomains must be > 0.")
            .Validate(options => options.MaxCandidateToolsPerDomain > 0, "AiRouting:MaxCandidateToolsPerDomain must be > 0.")
            .Validate(options => options.MaxTotalCandidateTools > 0, "AiRouting:MaxTotalCandidateTools must be > 0.")
            .Validate(options => options.MaxCandidateTools > 0, "AiRouting:MaxCandidateTools must be > 0.")
            .Validate(options => options.MaxToolCallsPerTurn > 0, "AiRouting:MaxToolCallsPerTurn must be > 0.")
            .Validate(options => options.MaxTotalCandidateTools >= options.MaxCandidateToolsPerDomain,
                "AiRouting:MaxTotalCandidateTools must be >= AiRouting:MaxCandidateToolsPerDomain.")
            .Validate(options => options.AllowedDomains.Length > 0
                    && options.AllowedDomains.All(domain => string.Equals(domain, "model", StringComparison.OrdinalIgnoreCase)),
                "AiRouting:AllowedDomains must be [\"model\"] for the model-only agent runtime.")
            .Validate(options => !(options.UseOfficialMicrosoftAgentFramework || options.MicrosoftAgentFrameworkRoutingEnabled)
                    || OfficialAgentProviderFactory.IsAllowedProvider(options.Provider),
                "AiRouting:Provider must be AzureOpenAI, OpenAI, OpenAiCompatibleLocal, or OllamaOfficialProviderForDevOnly when official Agent Framework routing is enabled.")
            .ValidateOnStart();

        services.AddOptions<LocalAiOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.LocalAi))
            .Validate(options => options.TimeoutSeconds > 0, "LocalAi:TimeoutSeconds must be > 0.")
            .ValidateOnStart();

        services.AddOptions<TILSOFTAI.Domain.Configuration.ChatOptions>()
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

        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection("Cors"))
            .PostConfigure(options => options.Normalize())
            .Validate(options => !options.Enabled || options.AllowedOrigins.Length > 0,
                "CORS: Enabled=true requires at least one allowed origin. Update Cors:AllowedOrigins in configuration.")
            .Validate(options => !options.Enabled || !options.AllowCredentials || !options.AllowedOrigins.Any(o => o == "*"),
                "CORS: AllowCredentials=true requires explicit origins, not wildcard '*'. Update Cors:AllowedOrigins in configuration.")
            .ValidateOnStart();

        services.AddOptions<GovernanceOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Governance))
            .Validate(options => options.ModelCallableSpPrefix == "ai_", "Governance:ModelCallableSpPrefix must be 'ai_'.")
            .Validate(options => options.InternalSpPrefix == "app_", "Governance:InternalSpPrefix must be 'app_'.")
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

        services.AddOptions<RuntimePolicySystemOptions>()
            .Bind(configuration.GetSection("RuntimePolicy"));

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

        services.AddOptions<AnswerNarrationPolicy>()
            .Bind(configuration.GetSection("Answering:Narration"))
            .Validate(options => options.MaxRowsForNarration > 0, "Answering:Narration:MaxRowsForNarration must be > 0.")
            .Validate(options => options.MaxOutputCharacters > 0, "Answering:Narration:MaxOutputCharacters must be > 0.")
            .ValidateOnStart();

        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection("RateLimit"))
            .Validate(options => options.PermitLimit > 0, "RateLimit:PermitLimit must be > 0.")
            .Validate(options => options.WindowSeconds > 0, "RateLimit:WindowSeconds must be > 0.")
            .Validate(options => options.QueueLimit >= 0, "RateLimit:QueueLimit must be >= 0.")
            .ValidateOnStart();

        services.AddOptions<ValidationOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Validation))
            .Validate(options => options.MaxInputLength > 0, "Validation:MaxInputLength must be > 0.")
            .Validate(options => options.MaxToolArgumentLength > 0, "Validation:MaxToolArgumentLength must be > 0.")
            .ValidateOnStart();

        services.AddOptions<AuditOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Audit))
            .Validate(options => options.RetentionDays > 0, "Audit:RetentionDays must be > 0.")
            .Validate(options => options.BufferSize > 0, "Audit:BufferSize must be > 0.")
            .ValidateOnStart();

        services.AddOptions<LoggingOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.StructuredLogging))
            .Validate(options => options.MaxPropertyValueLength > 0, "StructuredLogging:MaxPropertyValueLength must be > 0.")
            .Validate(options => options.SamplingRate >= 0 && options.SamplingRate <= 1, "StructuredLogging:SamplingRate must be between 0 and 1.")
            .ValidateOnStart();

        services.AddOptions<MetricsOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Metrics))
            .ValidateOnStart();

        services.AddOptions<ResilienceOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.Resilience))
            .ValidateOnStart();

        services.AddOptions<SecretsOptions>()
            .Bind(configuration.GetSection("Secrets"))
            .ValidateOnStart();

        services.AddOptions<ExternalConnectionCatalogOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.ExternalConnections))
            .ValidateOnStart();

        services.AddOptions<PlatformCatalogOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.PlatformCatalog))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.CatalogPath),
                "PlatformCatalog:CatalogPath is required when PlatformCatalog:Enabled=true.")
            .ValidateOnStart();

        services.AddOptions<CatalogControlPlaneOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.CatalogControlPlane))
            .Validate(options => options.SubmitRoles.Length > 0, "CatalogControlPlane:SubmitRoles must have at least one role.")
            .Validate(options => options.ApproveRoles.Length > 0, "CatalogControlPlane:ApproveRoles must have at least one role.")
            .Validate(options => options.ApplyRoles.Length > 0, "CatalogControlPlane:ApplyRoles must have at least one role.")
            .Validate(options => options.HighRiskApproveRoles.Length > 0, "CatalogControlPlane:HighRiskApproveRoles must have at least one role.")
            .Validate(options => options.MinBreakGlassJustificationLength >= 0, "CatalogControlPlane:MinBreakGlassJustificationLength must be >= 0.")
            .ValidateOnStart();

        services.AddOptions<CatalogCertificationOptions>()
            .Bind(configuration.GetSection(ConfigurationSectionNames.CatalogCertification))
            .Validate(options => !string.IsNullOrWhiteSpace(options.PolicyVersion), "CatalogCertification:PolicyVersion is required.")
            .Validate(options => options.RequiredEvidenceKinds.Length > 0, "CatalogCertification:RequiredEvidenceKinds must have at least one evidence kind.")
            .Validate(options => options.PreviewSuccessSloPercent is >= 0 and <= 100, "CatalogCertification:PreviewSuccessSloPercent must be between 0 and 100.")
            .Validate(options => options.SubmitSuccessSloPercent is >= 0 and <= 100, "CatalogCertification:SubmitSuccessSloPercent must be between 0 and 100.")
            .Validate(options => options.ApproveSuccessSloPercent is >= 0 and <= 100, "CatalogCertification:ApproveSuccessSloPercent must be between 0 and 100.")
            .Validate(options => options.ApplySuccessSloPercent is >= 0 and <= 100, "CatalogCertification:ApplySuccessSloPercent must be between 0 and 100.")
            .Validate(options => options.MaxTrustedEvidenceAgeDays >= 0, "CatalogCertification:MaxTrustedEvidenceAgeDays must be >= 0.")
            .Validate(options => options.EvidenceRetentionDays >= 0, "CatalogCertification:EvidenceRetentionDays must be >= 0.")
            .Validate(options => options.ManifestRetentionDays >= 0, "CatalogCertification:ManifestRetentionDays must be >= 0.")
            .Validate(options => options.AttestationRetentionDays >= 0, "CatalogCertification:AttestationRetentionDays must be >= 0.")
            .Validate(options => options.DossierArchiveRetentionDays >= 0, "CatalogCertification:DossierArchiveRetentionDays must be >= 0.")
            .Validate(options => options.TrustedEvidenceStatuses.Length > 0, "CatalogCertification:TrustedEvidenceStatuses must have at least one status.")
            .Validate(options => options.AllowedSignatureAlgorithms.Length > 0, "CatalogCertification:AllowedSignatureAlgorithms must have at least one algorithm.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SignerTrustStorePath), "CatalogCertification:SignerTrustStorePath is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SignerTrustStoreBackupPath), "CatalogCertification:SignerTrustStoreBackupPath is required.")
            .Validate(options => new[] { "filesystem", "managed_sql" }.Contains(options.SignerTrustStoreBackupBackend, StringComparer.OrdinalIgnoreCase),
                "CatalogCertification:SignerTrustStoreBackupBackend currently supports 'filesystem' or 'managed_sql'.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DossierArchiveBackend), "CatalogCertification:DossierArchiveBackend is required.")
            .Validate(options => new[] { "filesystem", "filesystem_mirror", "managed_sql" }.Contains(options.DossierArchiveBackend, StringComparer.OrdinalIgnoreCase),
                "CatalogCertification:DossierArchiveBackend currently supports 'filesystem', 'filesystem_mirror', or 'managed_sql'.")
            .Validate(options => !options.EnableDossierArchiveMirror || !string.IsNullOrWhiteSpace(options.DossierArchiveMirrorRootPath),
                "CatalogCertification:DossierArchiveMirrorRootPath is required when mirror archives are enabled.")
            .Validate(options => CatalogDurabilityClasses.Rank(options.MinimumArchiveDurabilityClassForProductionLike) > 0,
                "CatalogCertification:MinimumArchiveDurabilityClassForProductionLike is invalid.")
            .Validate(options => CatalogDurabilityClasses.Rank(options.MinimumTrustStoreDurabilityClassForProductionLike) > 0,
                "CatalogCertification:MinimumTrustStoreDurabilityClassForProductionLike is invalid.")
            .Validate(options => CatalogRetentionPostures.Rank(options.RequiredArchiveRetentionPostureForProductionLike) > 0,
                "CatalogCertification:RequiredArchiveRetentionPostureForProductionLike is invalid.")
            .Validate(options => options.TrustedEvidenceSigners.All(signer =>
                    !string.IsNullOrWhiteSpace(signer.SignerId)
                    && !string.IsNullOrWhiteSpace(signer.KeyId)
                    && !string.IsNullOrWhiteSpace(signer.PublicKeyPem)),
                "CatalogCertification:TrustedEvidenceSigners entries must include SignerId, KeyId, and PublicKeyPem.")
            .ValidateOnStart();

        return services;
    }
}
