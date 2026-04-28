using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Orchestration.Execution;

public sealed class CapabilityExecutionPolicy
{
    public bool IsReadMode(string? executionMode) =>
        EqualsAny(executionMode, "read", "read_only", "readonly");

    public bool IsWritePreviewMode(string? executionMode) =>
        EqualsAny(executionMode, "write_preview", "preview_write");

    public bool IsApprovedWriteMode(string? executionMode) =>
        EqualsAny(executionMode, "write", "approved_write", "execute_write", "write_execute");

    public bool CanExecuteRead(CapabilityDescriptor capability) =>
        IsReadMode(capability.ExecutionMode);

    public bool CanPreviewWrite(CapabilityDescriptor capability) =>
        IsWritePreviewMode(capability.ExecutionMode) || IsApprovedWriteMode(capability.ExecutionMode);

    public bool CanExecuteApprovedWrite(CapabilityDescriptor capability) =>
        IsApprovedWriteMode(capability.ExecutionMode);

    public string ToAdapterOperation(CapabilityDescriptor capability)
    {
        if (!string.IsNullOrWhiteSpace(capability.Operation)
            && !EqualsAny(capability.Operation, "read", "read_only", "readonly", "write", "write_preview", "write_execute"))
        {
            return capability.Operation;
        }

        if (IsReadMode(capability.ExecutionMode))
        {
            return ToolAdapterOperationNames.ExecuteQuery;
        }

        if (IsApprovedWriteMode(capability.ExecutionMode))
        {
            return ToolAdapterOperationNames.ExecuteWriteAction;
        }

        return capability.Operation;
    }

    private static bool EqualsAny(string? value, params string[] candidates) =>
        candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
}
