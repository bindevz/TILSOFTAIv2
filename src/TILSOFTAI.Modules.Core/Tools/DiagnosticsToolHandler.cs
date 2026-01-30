using System.Text.Json;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Compaction;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Core.Tools;

public sealed class DiagnosticsToolHandler : IToolHandler
{
    private const string DiagnosticsSpName = "ai_diagnostics_run";
    private readonly IJsonSchemaValidator _schemaValidator;
    private readonly ISqlExecutor _sqlExecutor;
    private readonly ToolResultCompactor _compactor;
    private readonly ChatOptions _chatOptions;

    public DiagnosticsToolHandler(
        IJsonSchemaValidator schemaValidator,
        ISqlExecutor sqlExecutor,
        ToolResultCompactor compactor,
        IOptions<ChatOptions> chatOptions)
    {
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
        _compactor = compactor ?? throw new ArgumentNullException(nameof(compactor));
        _chatOptions = chatOptions?.Value ?? throw new ArgumentNullException(nameof(chatOptions));
    }

    public async Task<string> ExecuteAsync(ToolDefinition tool, string argumentsJson, TilsoftExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(tool.SpName, DiagnosticsSpName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Diagnostics tool handler can only execute ai_diagnostics_run.");
        }

        var schemaValidation = _schemaValidator.Validate(tool.JsonSchema, argumentsJson);
        if (!schemaValidation.IsValid)
        {
            throw new InvalidOperationException(schemaValidation.Summary ?? "Diagnostics arguments failed schema validation.");
        }

        using var doc = JsonDocument.Parse(argumentsJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("module", out var moduleProp)
            || !root.TryGetProperty("ruleKey", out var ruleKeyProp))
        {
            throw new InvalidOperationException("Diagnostics requires module and ruleKey.");
        }

        var module = moduleProp.GetString() ?? string.Empty;
        var ruleKey = ruleKeyProp.GetString() ?? string.Empty;

        string? inputJson = null;
        if (root.TryGetProperty("inputJson", out var inputNode))
        {
            inputJson = inputNode.ValueKind == JsonValueKind.String
                ? inputNode.GetString()
                : inputNode.GetRawText();
        }

        if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(ruleKey))
        {
            throw new InvalidOperationException("Diagnostics requires module and ruleKey.");
        }

        var rawResult = await _sqlExecutor.ExecuteDiagnosticsAsync(
            DiagnosticsSpName,
            context.TenantId,
            module,
            ruleKey,
            inputJson,
            cancellationToken);

        var maxBytes = _chatOptions.CompactionLimits.TryGetValue("ToolResultMaxBytes", out var limit) && limit > 0
            ? limit
            : 16000;

        return _compactor.CompactJson(rawResult ?? "{}", maxBytes, _chatOptions.CompactionRules);
    }
}
