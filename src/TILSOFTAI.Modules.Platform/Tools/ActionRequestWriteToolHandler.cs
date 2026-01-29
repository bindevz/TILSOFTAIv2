using System.Text.Json;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Actions;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Platform.Tools;

public sealed class ActionRequestWriteToolHandler : IToolHandler
{
    private readonly ActionApprovalService _actionApprovalService;

    public ActionRequestWriteToolHandler(ActionApprovalService actionApprovalService)
    {
        _actionApprovalService = actionApprovalService ?? throw new ArgumentNullException(nameof(actionApprovalService));
    }

    public async Task<string> ExecuteAsync(
        ToolDefinition tool,
        string argumentsJson,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (tool is null)
        {
            throw new ArgumentNullException(nameof(tool));
        }

        using var argsDoc = JsonDocument.Parse(argumentsJson);
        var root = argsDoc.RootElement;

        if (!root.TryGetProperty("proposedToolName", out var toolNameElement) || toolNameElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Missing required 'proposedToolName' argument.");
        }

        if (!root.TryGetProperty("proposedSpName", out var spNameElement) || spNameElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Missing required 'proposedSpName' argument.");
        }

        if (!root.TryGetProperty("argsJson", out var argsJsonElement) || argsJsonElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Missing required 'argsJson' argument.");
        }

        var proposedToolName = toolNameElement.GetString() ?? string.Empty;
        var proposedSpName = spNameElement.GetString() ?? string.Empty;
        var argsJson = argsJsonElement.GetString() ?? string.Empty;

        // Create pending action request (does NOT execute)
        var request = await _actionApprovalService.CreateAsync(
            context,
            proposedToolName,
            proposedSpName,
            argsJson,
            cancellationToken);

        var result = new
        {
            actionId = request.ActionId,
            status = request.Status,
            proposedSpName = request.ProposedSpName,
            proposedToolName = request.ProposedToolName,
            message = "Action request created successfully. Pending human approval."
        };

        return JsonSerializer.Serialize(result);
    }
}
