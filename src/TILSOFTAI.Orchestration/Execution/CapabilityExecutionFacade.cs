using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Observability;
using TILSOFTAI.Orchestration.Semantic;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Orchestration.Execution;

public sealed class CapabilityExecutionFacade : ICapabilityExecutionFacade
{
    public const string ArgumentValidationFailedCode = "ARGUMENT_VALIDATION_FAILED";
    private const string CapabilityNotFoundCode = "CAPABILITY_NOT_FOUND";
    private const string UnsupportedExecutionModeCode = "UNSUPPORTED_EXECUTION_MODE";
    private const string WriteApprovalRequiredCode = "WRITE_APPROVAL_REQUIRED";
    private const string AdapterExecutionFailedCode = "ADAPTER_EXECUTION_FAILED";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICapabilityRegistry _capabilityRegistry;
    private readonly ICapabilityMetadataRepository? _metadataRepository;
    private readonly IExecutionContextAccessor? _executionContextAccessor;
    private readonly IToolAdapterRegistry _toolAdapterRegistry;
    private readonly IApprovalEngine? _approvalEngine;
    private readonly CapabilityArgumentMapper _argumentMapper;
    private readonly CapabilityExecutionPolicy _executionPolicy;
    private readonly ILogger<CapabilityExecutionFacade> _logger;
    private readonly IMetricsService? _metrics;

    public CapabilityExecutionFacade(
        ICapabilityRegistry capabilityRegistry,
        IEnumerable<ICapabilityMetadataRepository> metadataRepositories,
        IEnumerable<IExecutionContextAccessor> executionContextAccessors,
        IEnumerable<IApprovalEngine> approvalEngines,
        IToolAdapterRegistry toolAdapterRegistry,
        CapabilityArgumentMapper argumentMapper,
        CapabilityExecutionPolicy executionPolicy,
        ILogger<CapabilityExecutionFacade>? logger = null,
        IEnumerable<IMetricsService>? metricsServices = null)
    {
        _capabilityRegistry = capabilityRegistry ?? throw new ArgumentNullException(nameof(capabilityRegistry));
        _metadataRepository = metadataRepositories?.FirstOrDefault();
        _executionContextAccessor = executionContextAccessors?.FirstOrDefault();
        _approvalEngine = approvalEngines?.FirstOrDefault();
        _toolAdapterRegistry = toolAdapterRegistry ?? throw new ArgumentNullException(nameof(toolAdapterRegistry));
        _argumentMapper = argumentMapper ?? throw new ArgumentNullException(nameof(argumentMapper));
        _executionPolicy = executionPolicy ?? throw new ArgumentNullException(nameof(executionPolicy));
        _logger = logger ?? NullLogger<CapabilityExecutionFacade>.Instance;
        _metrics = metricsServices?.FirstOrDefault();
    }

    public async Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var loaded = await LoadCapabilityAsync(capabilityKey, cancellationToken).ConfigureAwait(false);
        if (loaded.Capability is null)
        {
            return NotFound(capabilityKey, arguments);
        }

        var context = CurrentContext();
        LogFacadeStarted(loaded.Capability, context, arguments);
        var access = CapabilityAccessPolicy.Evaluate(loaded.Capability, context);
        if (!access.Allowed)
        {
            return Complete(AccessDenied(loaded.Capability, arguments, context, access), context, startedAt);
        }

        if (!_executionPolicy.CanExecuteRead(loaded.Capability))
        {
            return Complete(Blocked(
                loaded.Capability,
                arguments,
                context,
                UnsupportedExecutionModeCode,
                "Capability is not read-only. Use preview/approval flow."), context, startedAt);
        }

        var procArgs = _argumentMapper.MapModelArgsToProcArgs(loaded.Capability, arguments, loaded.Metadata);
        var validation = ValidateArguments(loaded.Capability, procArgs);
        if (!validation.IsValid)
        {
            var failed = ValidationFailed(loaded.Capability, procArgs, context, validation);
            LogValidationFailed(loaded.Capability, context, failed);
            return Complete(failed, context, startedAt);
        }

        var envelope = await ExecuteAdapterAsync(loaded.Capability, loaded.Metadata, procArgs, context, null, cancellationToken)
            .ConfigureAwait(false);
        return Complete(envelope, context, startedAt);
    }

    public async Task<CapabilityExecutionEnvelope> PreviewWriteAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var loaded = await LoadCapabilityAsync(capabilityKey, cancellationToken).ConfigureAwait(false);
        if (loaded.Capability is null)
        {
            return NotFound(capabilityKey, arguments);
        }

        var context = CurrentContext();
        LogFacadeStarted(loaded.Capability, context, arguments);
        var access = CapabilityAccessPolicy.Evaluate(loaded.Capability, context);
        if (!access.Allowed)
        {
            return Complete(AccessDenied(loaded.Capability, arguments, context, access), context, startedAt);
        }

        if (!_executionPolicy.CanPreviewWrite(loaded.Capability))
        {
            return Complete(Blocked(
                loaded.Capability,
                arguments,
                context,
                UnsupportedExecutionModeCode,
                "Capability does not support write preview."), context, startedAt);
        }

        var procArgs = _argumentMapper.MapModelArgsToProcArgs(loaded.Capability, arguments, loaded.Metadata);
        var validation = ValidateArguments(loaded.Capability, procArgs);
        if (!validation.IsValid)
        {
            var failed = ValidationFailed(loaded.Capability, procArgs, context, validation);
            LogValidationFailed(loaded.Capability, context, failed);
            return Complete(failed, context, startedAt);
        }

        var envelope = await CreateWritePreviewAsync(
                loaded.Capability,
                loaded.Metadata,
                procArgs,
                context,
                cancellationToken)
            .ConfigureAwait(false);
        return Complete(envelope, context, startedAt);
    }

    public async Task<CapabilityExecutionEnvelope> ExecuteApprovedWriteAsync(
        string capabilityKey,
        string approvedActionId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var loaded = await LoadCapabilityAsync(capabilityKey, cancellationToken).ConfigureAwait(false);
        if (loaded.Capability is null)
        {
            return NotFound(capabilityKey, arguments);
        }

        var context = CurrentContext();
        LogFacadeStarted(loaded.Capability, context, arguments);
        var access = CapabilityAccessPolicy.Evaluate(loaded.Capability, context);
        if (!access.Allowed)
        {
            return Complete(AccessDenied(loaded.Capability, arguments, context, access), context, startedAt);
        }

        if (!_executionPolicy.CanExecuteApprovedWrite(loaded.Capability))
        {
            return Complete(Blocked(
                loaded.Capability,
                arguments,
                context,
                UnsupportedExecutionModeCode,
                "Capability is not an approved write operation."), context, startedAt);
        }

        if (string.IsNullOrWhiteSpace(approvedActionId))
        {
            return Complete(Blocked(
                loaded.Capability,
                arguments,
                context,
                WriteApprovalRequiredCode,
                "Write operations require an approved action ID."), context, startedAt);
        }

        var procArgs = _argumentMapper.MapModelArgsToProcArgs(loaded.Capability, arguments, loaded.Metadata);
        var validation = ValidateArguments(loaded.Capability, procArgs);
        if (!validation.IsValid)
        {
            var failed = ValidationFailed(loaded.Capability, procArgs, context, validation);
            LogValidationFailed(loaded.Capability, context, failed);
            return Complete(failed, context, startedAt);
        }

        if (_approvalEngine is not null)
        {
            var approvedEnvelope = await ExecuteApprovedActionAsync(
                    loaded.Capability,
                    loaded.Metadata,
                    procArgs,
                    context,
                    approvedActionId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Complete(approvedEnvelope, context, startedAt);
        }

        var envelope = await ExecuteAdapterAsync(loaded.Capability, loaded.Metadata, procArgs, context, approvedActionId, cancellationToken)
            .ConfigureAwait(false);
        return Complete(envelope, context, startedAt);
    }

    private CapabilityExecutionEnvelope Complete(
        CapabilityExecutionEnvelope envelope,
        TilsoftExecutionContext context,
        long startedAt)
    {
        var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        _metrics?.RecordHistogram(
            MetricNames.CapabilityFacadeDurationMs,
            durationMs,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["capability"] = envelope.CapabilityKey,
                ["status"] = envelope.Status ?? "unknown"
            });
        _logger.LogInformation(
            "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | capabilityKey: {CapabilityKey} | procedureName: {ProcedureName} | rowCount: {RowCount} | durationMs: {DurationMs} | errorCode: {ErrorCode}",
            Sprint35TraceEvents.CapabilityExecutionCompleted,
            context.CorrelationId,
            context.TenantId,
            context.UserId,
            envelope.CapabilityKey,
            envelope.ProcedureName ?? "none",
            envelope.RowCount,
            durationMs,
            envelope.ErrorCode ?? "none");
        return envelope;
    }

    private void LogFacadeStarted(
        CapabilityDescriptor capability,
        TilsoftExecutionContext context,
        IReadOnlyDictionary<string, object?> arguments)
    {
        _logger.LogInformation(
            "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | capabilityKey: {CapabilityKey} | procedureName: {ProcedureName} | argumentsMasked: {ArgumentsMasked}",
            Sprint35TraceEvents.CapabilityFacadeStarted,
            context.CorrelationId,
            context.TenantId,
            context.UserId,
            capability.CapabilityKey,
            capability.IntegrationBinding.TryGetValue("storedProcedure", out var sp) ? sp : "none",
            JsonSerializer.Serialize(arguments, JsonOptions));
    }

    private void LogValidationFailed(
        CapabilityDescriptor capability,
        TilsoftExecutionContext context,
        CapabilityExecutionEnvelope envelope)
    {
        _metrics?.IncrementCounter(
            MetricNames.AgentRoutingValidationFailureTotal,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["capability"] = capability.CapabilityKey
            });
        _logger.LogWarning(
            "{EventName} | correlationId: {CorrelationId} | tenantId: {TenantId} | userId: {UserId} | capabilityKey: {CapabilityKey} | errorCode: {ErrorCode}",
            Sprint35TraceEvents.CapabilityValidationFailed,
            context.CorrelationId,
            context.TenantId,
            context.UserId,
            capability.CapabilityKey,
            envelope.ErrorCode ?? ArgumentValidationFailedCode);
    }

    private async Task<CapabilityExecutionEnvelope> CreateWritePreviewAsync(
        CapabilityDescriptor capability,
        CapabilitySemanticMetadata? metadata,
        IReadOnlyDictionary<string, object?> procArgs,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (_approvalEngine is null)
        {
            return Blocked(
                capability,
                procArgs,
                context,
                WriteApprovalRequiredCode,
                "Write preview requires approval infrastructure.");
        }

        try
        {
            var storedProcedure = GetStoredProcedure(capability);
            var payloadJson = JsonSerializer.Serialize(procArgs, JsonOptions);
            var proposed = new ProposedAction
            {
                ActionType = "write",
                AgentId = "microsoft-agent-router",
                TargetSystem = capability.AdapterType,
                CapabilityKey = capability.CapabilityKey,
                ToolName = capability.CapabilityKey,
                StoredProcedure = storedProcedure,
                PayloadJson = payloadJson,
                DiffPreviewJson = payloadJson,
                RiskLevel = "requires_confirmation",
                ApprovalRequirement = "explicit_user_confirmation"
            };

            var record = await _approvalEngine.CreateAsync(
                    proposed,
                    ApprovalContext.FromExecutionContext(context, "microsoft-agent-router"),
                    cancellationToken)
                .ConfigureAwait(false);

            var draftAction = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["actionId"] = record.ActionId,
                ["status"] = record.Status,
                ["capabilityKey"] = capability.CapabilityKey,
                ["storedProcedure"] = storedProcedure,
                ["arguments"] = procArgs,
                ["requestedByUserId"] = record.RequestedByUserId,
                ["requestedAtUtc"] = record.RequestedAtUtc,
                ["approvalRequirement"] = record.ApprovalRequirement ?? proposed.ApprovalRequirement,
                ["riskLevel"] = record.RiskLevel ?? proposed.RiskLevel
            };

            return BaseEnvelope(capability, procArgs, context, metadata) with
            {
                Success = true,
                Status = "preview",
                DraftAction = draftAction,
                Result = draftAction,
                ExecutionMetadata = BaseMetadata(capability, context) with
                {
                    Operation = "write_preview",
                    Detail = record
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Write preview failed for {CapabilityKey}.", capability.CapabilityKey);
            return Blocked(capability, procArgs, context, "WRITE_PREVIEW_FAILED", ex.Message);
        }
    }

    private async Task<CapabilityExecutionEnvelope> ExecuteApprovedActionAsync(
        CapabilityDescriptor capability,
        CapabilitySemanticMetadata? metadata,
        IReadOnlyDictionary<string, object?> procArgs,
        TilsoftExecutionContext context,
        string approvedActionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _approvalEngine!.ExecuteAsync(
                    approvedActionId,
                    ApprovalContext.FromExecutionContext(context, "microsoft-agent-router"),
                    JsonSerializer.Serialize(procArgs, JsonOptions),
                    cancellationToken)
                .ConfigureAwait(false);

            return BaseEnvelope(capability, procArgs, context, metadata) with
            {
                Success = true,
                Status = "succeeded",
                Result = result.CompactedResult ?? result.RawResult,
                ExecutionMetadata = BaseMetadata(capability, context) with
                {
                    Operation = ToolAdapterOperationNames.ExecuteWriteAction,
                    PayloadJson = result.RawResult,
                    Detail = result.Action
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Approved write execution failed for {CapabilityKey}.", capability.CapabilityKey);
            return Blocked(capability, procArgs, context, "APPROVED_WRITE_EXECUTION_FAILED", ex.Message);
        }
    }

    private async Task<LoadedCapability> LoadCapabilityAsync(string capabilityKey, CancellationToken cancellationToken)
    {
        var locale = CurrentContext().Language;
        if (string.IsNullOrWhiteSpace(locale))
        {
            locale = "vi-VN";
        }

        CapabilitySemanticMetadata? metadata = null;
        if (_metadataRepository is not null)
        {
            metadata = await _metadataRepository.GetCapabilityMetadataAsync(capabilityKey, locale, cancellationToken)
                .ConfigureAwait(false);
        }

        var descriptor = _capabilityRegistry.Resolve(capabilityKey)
            ?? (metadata is null ? null : BuildDescriptor(metadata));

        return new LoadedCapability(descriptor, metadata);
    }

    private async Task<CapabilityExecutionEnvelope> ExecuteAdapterAsync(
        CapabilityDescriptor capability,
        CapabilitySemanticMetadata? metadata,
        IReadOnlyDictionary<string, object?> procArgs,
        TilsoftExecutionContext context,
        string? approvedActionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var adapter = _toolAdapterRegistry.Resolve(capability.AdapterType);
            var request = CreateRequest(capability, procArgs, context, approvedActionId);
            var result = await adapter.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

            return result.Success
                ? Success(capability, metadata, procArgs, context, result)
                : AdapterFailed(capability, metadata, procArgs, context, result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Capability adapter execution failed for {CapabilityKey}.", capability.CapabilityKey);
            return Blocked(capability, procArgs, context, AdapterExecutionFailedCode, ex.Message);
        }
    }

    private ToolExecutionRequest CreateRequest(
        CapabilityDescriptor capability,
        IReadOnlyDictionary<string, object?> procArgs,
        TilsoftExecutionContext context,
        string? approvedActionId)
    {
        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in capability.IntegrationBinding)
        {
            metadata[key] = value;
        }

        metadata["roles"] = string.Join(",", context.Roles ?? Array.Empty<string>());
        if (!string.IsNullOrWhiteSpace(approvedActionId))
        {
            metadata["approvedActionId"] = approvedActionId;
        }

        return new ToolExecutionRequest
        {
            TenantId = context.TenantId,
            AgentId = "microsoft-agent-router",
            SystemId = capability.TargetSystemId,
            CapabilityKey = capability.CapabilityKey,
            Operation = _executionPolicy.ToAdapterOperation(capability),
            ArgumentsJson = JsonSerializer.Serialize(procArgs, JsonOptions),
            ExecutionMode = capability.ExecutionMode,
            CorrelationId = context.CorrelationId,
            Metadata = metadata
        };
    }

    private static CapabilityArgumentValidationResult ValidateArguments(
        CapabilityDescriptor capability,
        IReadOnlyDictionary<string, object?> procArgs)
    {
        var json = JsonSerializer.Serialize(procArgs, JsonOptions);
        return CapabilityArgumentValidator.Validate(capability, json);
    }

    private TilsoftExecutionContext CurrentContext() =>
        _executionContextAccessor?.Current ?? new TilsoftExecutionContext();

    private static CapabilityDescriptor BuildDescriptor(CapabilitySemanticMetadata metadata)
    {
        var storedProcedure = NormalizeStoredProcedure(metadata.StoredProcedure);
        var binding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(storedProcedure))
        {
            binding["storedProcedure"] = storedProcedure;
        }

        return new CapabilityDescriptor
        {
            CapabilityKey = metadata.CapabilityKey,
            Domain = metadata.Domain,
            AdapterType = metadata.AdapterType,
            Operation = metadata.Operation,
            TargetSystemId = metadata.AdapterType,
            IntegrationBinding = binding,
            RequiredRoles = ReadStringList(metadata.RequiredRoles),
            AllowedTenants = ReadStringList(metadata.AllowedTenants),
            ArgumentContract = BuildArgumentContract(metadata),
            ExecutionMode = NormalizeExecutionMode(metadata.ExecutionMode)
        };
    }

    private static CapabilityArgumentContract BuildArgumentContract(CapabilitySemanticMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.ArgumentContract))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<CapabilityArgumentContract>(metadata.ArgumentContract, JsonOptions);
                if (parsed is not null)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
            }
        }

        return new CapabilityArgumentContract
        {
            RequiredArguments = metadata.Arguments
                .Where(argument => argument.IsRequired)
                .Select(argument => argument.ProcParameterName)
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .ToArray(),
            AllowedArguments = metadata.Arguments
                .Select(argument => argument.ProcParameterName)
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .ToArray(),
            AllowAdditionalArguments = false,
            Arguments = metadata.Arguments
                .Select(ToArgumentRule)
                .ToArray()
        };
    }

    private static CapabilityArgumentRule ToArgumentRule(CapabilityArgumentMetadata argument)
    {
        var rule = ReadValidationRule(argument.ValidationRule);
        return new CapabilityArgumentRule
        {
            Name = argument.ProcParameterName,
            Type = NormalizeArgumentType(argument.DataType),
            Format = rule.Format,
            Enum = rule.Enum,
            Min = rule.Min,
            Max = rule.Max,
            MinLength = rule.MinLength,
            MaxLength = rule.MaxLength,
            Pattern = rule.Pattern
        };
    }

    private static CapabilityArgumentRule ReadValidationRule(string? validationRule)
    {
        if (string.IsNullOrWhiteSpace(validationRule))
        {
            return new CapabilityArgumentRule();
        }

        try
        {
            return JsonSerializer.Deserialize<CapabilityArgumentRule>(validationRule, JsonOptions)
                ?? new CapabilityArgumentRule();
        }
        catch (JsonException)
        {
            return new CapabilityArgumentRule();
        }
    }

    private static string NormalizeArgumentType(string dataType) =>
        dataType.Trim().ToLowerInvariant() switch
        {
            "int" or "bigint" or "smallint" => "integer",
            "decimal" or "numeric" or "money" or "float" => "number",
            "bit" or "bool" => "boolean",
            _ => "string"
        };

    private static string NormalizeExecutionMode(string executionMode) =>
        executionMode.Trim().ToLowerInvariant() switch
        {
            "read" or "read_only" => "readonly",
            _ => executionMode
        };

    private static string? NormalizeStoredProcedure(string? storedProcedure)
    {
        if (string.IsNullOrWhiteSpace(storedProcedure))
        {
            return null;
        }

        var trimmed = storedProcedure.Trim();
        return trimmed.Contains('.', StringComparison.Ordinal) ? trimmed : $"dbo.{trimmed}";
    }

    private static string GetStoredProcedure(CapabilityDescriptor capability) =>
        capability.IntegrationBinding.TryGetValue("storedProcedure", out var storedProcedure)
            && !string.IsNullOrWhiteSpace(storedProcedure)
            ? storedProcedure
            : throw new InvalidOperationException(
                $"Capability '{capability.CapabilityKey}' requires a storedProcedure binding for write preview.");

    private static IReadOnlyList<string> ReadStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return json.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    private static CapabilityExecutionEnvelope Success(
        CapabilityDescriptor capability,
        CapabilitySemanticMetadata? metadata,
        IReadOnlyDictionary<string, object?> arguments,
        TilsoftExecutionContext context,
        ToolExecutionResult result)
    {
        var rows = ReadRows(result);
        return BaseEnvelope(capability, arguments, context, metadata) with
        {
            Success = true,
            Status = "succeeded",
            Rows = rows,
            RowCount = rows.Count,
            Result = rows.Count > 0 ? rows : result.Payload,
            ExecutionMetadata = BaseMetadata(capability, context) with
            {
                PayloadJson = result.PayloadJson,
                Detail = result.Detail
            }
        };
    }

    private static CapabilityExecutionEnvelope AdapterFailed(
        CapabilityDescriptor capability,
        CapabilitySemanticMetadata? metadata,
        IReadOnlyDictionary<string, object?> arguments,
        TilsoftExecutionContext context,
        ToolExecutionResult result) =>
        BaseEnvelope(capability, arguments, context, metadata) with
        {
            Success = false,
            Status = "failed",
            ErrorCode = result.ErrorCode ?? AdapterExecutionFailedCode,
            ErrorMessage = SerializeDetail(result.Detail)
        };

    private static CapabilityExecutionEnvelope AccessDenied(
        CapabilityDescriptor capability,
        IReadOnlyDictionary<string, object?> arguments,
        TilsoftExecutionContext context,
        CapabilityAccessDecision access) =>
        BaseEnvelope(capability, arguments, context) with
        {
            Success = false,
            Status = "blocked",
            ErrorCode = access.Code ?? CapabilityAccessPolicy.AccessDeniedCode,
            ErrorMessage = "You are not allowed to execute this capability.",
            ExecutionMetadata = BaseMetadata(capability, context) with
            {
                Detail = access.Detail
            }
        };

    private static CapabilityExecutionEnvelope ValidationFailed(
        CapabilityDescriptor capability,
        IReadOnlyDictionary<string, object?> arguments,
        TilsoftExecutionContext context,
        CapabilityArgumentValidationResult validation)
    {
        var parsed = ParseValidationDetail(validation.Detail);
        return BaseEnvelope(capability, arguments, context) with
        {
            Success = false,
            Status = "validation_failed",
            MissingArguments = parsed.Missing,
            InvalidArguments = parsed.Invalid,
            ClarificationQuestion = parsed.Missing.Count > 0
                ? $"Please provide: {string.Join(", ", parsed.Missing)}."
                : "Please correct the capability arguments.",
            ErrorCode = ArgumentValidationFailedCode,
            ErrorMessage = SerializeDetail(validation.Detail),
            ExecutionMetadata = BaseMetadata(capability, context) with
            {
                Detail = validation.Detail
            }
        };
    }

    private static CapabilityExecutionEnvelope Blocked(
        CapabilityDescriptor capability,
        IReadOnlyDictionary<string, object?> arguments,
        TilsoftExecutionContext context,
        string errorCode,
        string message) =>
        BaseEnvelope(capability, arguments, context) with
        {
            Success = false,
            Status = "blocked",
            ErrorCode = errorCode,
            ErrorMessage = message
        };

    private static CapabilityExecutionEnvelope NotFound(
        string capabilityKey,
        IReadOnlyDictionary<string, object?> arguments) => new()
        {
            CapabilityKey = capabilityKey,
            ExecutionMode = "unknown",
            Arguments = arguments,
            ExecutionMetadata = ExecutionMetadata.Empty,
            SensitivityPolicy = SensitivityPolicy.Default,
            AnswerPolicy = AnswerPolicy.Default,
            Success = false,
            Status = "blocked",
            ErrorCode = CapabilityNotFoundCode,
            ErrorMessage = "Capability was not found."
        };

    private static CapabilityExecutionEnvelope BaseEnvelope(
        CapabilityDescriptor capability,
        IReadOnlyDictionary<string, object?> arguments,
        TilsoftExecutionContext context,
        CapabilitySemanticMetadata? metadata = null) => new()
        {
            CapabilityKey = capability.CapabilityKey,
            ExecutionMode = capability.ExecutionMode,
            ProcedureName = capability.IntegrationBinding.TryGetValue("storedProcedure", out var storedProcedure)
                ? storedProcedure
                : null,
            Arguments = arguments,
            ResultSchema = ResultSchema.FromJson(metadata?.ResultSchema),
            ExecutionMetadata = BaseMetadata(capability, context),
            SensitivityPolicy = SensitivityPolicy.FromJson(metadata?.SensitivityPolicy),
            AnswerPolicy = AnswerPolicy.FromJson(metadata?.AnswerPolicy)
        };

    private static ExecutionMetadata BaseMetadata(CapabilityDescriptor capability, TilsoftExecutionContext context) => new()
    {
        AdapterType = capability.AdapterType,
        Operation = capability.Operation,
        TenantId = context.TenantId,
        UserId = context.UserId,
        CorrelationId = context.CorrelationId
    };

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> ReadRows(ToolExecutionResult result)
    {
        if (result.Payload is IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
        {
            return rows;
        }

        if (result.Payload is IEnumerable<IReadOnlyDictionary<string, object?>> enumerableRows)
        {
            return enumerableRows.ToArray();
        }

        if (string.IsNullOrWhiteSpace(result.PayloadJson))
        {
            return Array.Empty<IReadOnlyDictionary<string, object?>>();
        }

        try
        {
            var parsedRows = JsonSerializer.Deserialize<IReadOnlyList<Dictionary<string, object?>>>(result.PayloadJson, JsonOptions);
            return parsedRows?.Select(row => (IReadOnlyDictionary<string, object?>)row).ToArray()
                ?? Array.Empty<IReadOnlyDictionary<string, object?>>();
        }
        catch (JsonException)
        {
            return Array.Empty<IReadOnlyDictionary<string, object?>>();
        }
    }

    private static ValidationDetail ParseValidationDetail(object? detail)
    {
        if (detail is null)
        {
            return new ValidationDetail(Array.Empty<string>(), Array.Empty<string>());
        }

        try
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(detail, JsonOptions));
            var root = document.RootElement;
            var missing = ReadStringArray(root, "missing");
            var invalid = ReadStringArray(root, "extra");

            if (root.TryGetProperty("argument", out var argument) && argument.ValueKind == JsonValueKind.String)
            {
                invalid = invalid.Concat(new[] { argument.GetString()! }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            return new ValidationDetail(missing, invalid);
        }
        catch (JsonException)
        {
            return new ValidationDetail(Array.Empty<string>(), Array.Empty<string>());
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray();
    }

    private static string? SerializeDetail(object? detail) =>
        detail is null ? null : JsonSerializer.Serialize(detail, JsonOptions);

    private sealed record LoadedCapability(CapabilityDescriptor? Capability, CapabilitySemanticMetadata? Metadata);

    private sealed record ValidationDetail(IReadOnlyList<string> Missing, IReadOnlyList<string> Invalid);
}
