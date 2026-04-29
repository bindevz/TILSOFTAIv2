using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Infrastructure.Actions;
using TILSOFTAI.Infrastructure.Audit;
using TILSOFTAI.Infrastructure.Catalog;
using TILSOFTAI.Infrastructure.Conversations;
using TILSOFTAI.Infrastructure.Errors;
using TILSOFTAI.Infrastructure.Http;
using TILSOFTAI.Infrastructure.SemanticSql;
using TILSOFTAI.Infrastructure.Sql;
using TILSOFTAI.Orchestration.Actions;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Semantic;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Api.Extensions;

public static class AddTilsoftAiSqlExtensions
{
    public static IServiceCollection AddTilsoftAiSql(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISqlErrorLogWriter, SqlErrorLogWriter>();
        services.AddSingleton<ISqlExecutor, SqlExecutor>();
        services.AddSingleton<IToolAdapter, SqlToolAdapter>();
        services.AddHttpClient<RestJsonToolAdapter>();
        services.AddSingleton<IToolAdapter>(sp => sp.GetRequiredService<RestJsonToolAdapter>());
        services.AddSingleton<SqlContractValidator>();
        services.AddHostedService<SqlContractValidatorHostedService>();
        services.AddSingleton<IConversationStore, SqlConversationStore>();
        services.AddSingleton<IActionRequestStore, SqlActionRequestStore>();
        services.AddSingleton<IWriteActionGuard, ApprovalBackedWriteActionGuard>();
        services.AddSingleton<ISemanticKnowledgeRepository, SqlSemanticKnowledgeRepository>();
        services.AddSingleton<ICapabilityMetadataRepository, SqlCapabilityMetadataRepository>();
        services.AddSingleton<IEntityAliasRepository, SqlEntityAliasRepository>();
        services.AddSingleton<IToolRoutingTraceStore, SqlToolRoutingTraceStore>();
        return services;
    }
}
