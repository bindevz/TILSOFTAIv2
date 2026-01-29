using Microsoft.Extensions.DependencyInjection;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Infrastructure.Tools;
using TILSOFTAI.Modules.Core.Tools;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Api.Tools;

public sealed class ToolHandlerRouter : IToolHandler
{
    private const string DiagnosticsSpName = "ai_diagnostics_run";
    private readonly SqlToolHandler _sqlToolHandler;
    private readonly DiagnosticsToolHandler _diagnosticsToolHandler;
    private readonly INamedToolHandlerRegistry _handlerRegistry;
    private readonly IServiceProvider _serviceProvider;

    public ToolHandlerRouter(
        SqlToolHandler sqlToolHandler,
        DiagnosticsToolHandler diagnosticsToolHandler,
        INamedToolHandlerRegistry handlerRegistry,
        IServiceProvider serviceProvider)
    {
        _sqlToolHandler = sqlToolHandler ?? throw new ArgumentNullException(nameof(sqlToolHandler));
        _diagnosticsToolHandler = diagnosticsToolHandler ?? throw new ArgumentNullException(nameof(diagnosticsToolHandler));
        _handlerRegistry = handlerRegistry ?? throw new ArgumentNullException(nameof(handlerRegistry));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Task<string> ExecuteAsync(ToolDefinition tool, string argumentsJson, TilsoftExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (string.Equals(tool.SpName, DiagnosticsSpName, StringComparison.OrdinalIgnoreCase))
        {
            return _diagnosticsToolHandler.ExecuteAsync(tool, argumentsJson, context, cancellationToken);
        }

        if (_handlerRegistry.TryGet(tool.Name, out var handlerType) && handlerType is not null)
        {
            var handler = (IToolHandler)ActivatorUtilities.CreateInstance(_serviceProvider, handlerType);
            return handler.ExecuteAsync(tool, argumentsJson, context, cancellationToken);
        }

        return _sqlToolHandler.ExecuteAsync(tool, argumentsJson, context, cancellationToken);
    }
}
