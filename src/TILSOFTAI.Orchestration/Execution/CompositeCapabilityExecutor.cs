using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.Execution;

public sealed class CompositeCapabilityExecutor : ICompositeCapabilityExecutor
{
    private const string CompositeMode = "composite";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ICapabilityMetadataRepository? _metadataRepository;
    private readonly ICapabilityExecutionFacade _executionFacade;
    private readonly IExecutionContextAccessor? _executionContextAccessor;
    private readonly AiRoutingOptions _options;
    private readonly ILogger<CompositeCapabilityExecutor> _logger;

    public CompositeCapabilityExecutor(
        IEnumerable<ICapabilityMetadataRepository> metadataRepositories,
        ICapabilityExecutionFacade executionFacade,
        IEnumerable<IExecutionContextAccessor> executionContextAccessors,
        IOptions<AiRoutingOptions>? options = null,
        ILogger<CompositeCapabilityExecutor>? logger = null)
    {
        _metadataRepository = metadataRepositories?.FirstOrDefault();
        _executionFacade = executionFacade ?? throw new ArgumentNullException(nameof(executionFacade));
        _executionContextAccessor = executionContextAccessors?.FirstOrDefault();
        _options = options?.Value ?? new AiRoutingOptions();
        _logger = logger ?? NullLogger<CompositeCapabilityExecutor>.Instance;
    }

    public async Task<CapabilityExecutionEnvelope> ExecuteAsync(
        string compositeCapabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        if (_metadataRepository is null)
        {
            return CapabilityExecutionEnvelope.Blocked(
                compositeCapabilityKey,
                "Composite capability execution requires capability metadata.");
        }

        var context = CurrentContext();
        var locale = string.IsNullOrWhiteSpace(context.Language) ? "vi-VN" : context.Language;
        var composite = await _metadataRepository.GetCapabilityMetadataAsync(
                compositeCapabilityKey,
                locale,
                cancellationToken)
            .ConfigureAwait(false);

        if (composite is null)
        {
            return CapabilityExecutionEnvelope.Blocked(compositeCapabilityKey, "Composite capability was not found.");
        }

        if (!string.Equals(composite.ExecutionMode, CompositeMode, StringComparison.OrdinalIgnoreCase))
        {
            return CapabilityExecutionEnvelope.Blocked(
                compositeCapabilityKey,
                $"Capability is not composite: {composite.ExecutionMode}");
        }

        var definition = CompositeDefinition.Parse(composite);
        if (definition.SubCapabilities.Count == 0)
        {
            return CapabilityExecutionEnvelope.Blocked(
                compositeCapabilityKey,
                "Composite capability has no declared sub-capabilities.");
        }

        var maxCalls = Math.Max(1, _options.MaxToolCallsPerTurn);
        if (definition.SubCapabilities.Count > maxCalls)
        {
            return CapabilityExecutionEnvelope.Blocked(
                compositeCapabilityKey,
                $"Composite capability exceeds max sub-capability count ({maxCalls}).");
        }

        var subMetadata = await LoadSubMetadataAsync(definition.SubCapabilities, locale, cancellationToken)
            .ConfigureAwait(false);
        var nonReadOnly = subMetadata
            .Where(pair => pair.Value is null || !IsReadOnly(pair.Value.ExecutionMode))
            .Select(pair => pair.Key)
            .ToArray();

        if (nonReadOnly.Length > 0)
        {
            return CapabilityExecutionEnvelope.Blocked(
                compositeCapabilityKey,
                $"Composite capability can only execute read-only sub-capabilities: {string.Join(", ", nonReadOnly)}");
        }

        var envelopes = definition.Policy.Mode.Equals("sequential", StringComparison.OrdinalIgnoreCase)
            ? await ExecuteSequentialAsync(definition.SubCapabilities, arguments, cancellationToken).ConfigureAwait(false)
            : await ExecuteParallelAsync(definition.SubCapabilities, arguments, cancellationToken).ConfigureAwait(false);

        var sections = envelopes
            .Select(envelope => new CompositeResultSection
            {
                CapabilityKey = envelope.CapabilityKey,
                Success = envelope.Success,
                RowCount = envelope.RowCount,
                Rows = envelope.Rows,
                ErrorCode = envelope.ErrorCode,
                ErrorMessage = envelope.ErrorMessage,
                ExecutionMetadata = envelope.ExecutionMetadata
            })
            .ToArray();

        var totalRows = sections.Sum(section => section.RowCount);
        var bundle = new CompositeResultBundle
        {
            CapabilityKey = compositeCapabilityKey,
            Mode = definition.Policy.Mode,
            Output = definition.Policy.Output,
            JoinKey = definition.Policy.JoinKey,
            Sections = sections,
            RowCounts = sections.ToDictionary(section => section.CapabilityKey, section => section.RowCount, StringComparer.OrdinalIgnoreCase)
        };

        _logger.LogInformation(
            "CompositeCapabilityExecuted | CapabilityKey: {CapabilityKey} | SubCapabilities: {SubCapabilityCount} | RowCount: {RowCount}",
            compositeCapabilityKey,
            sections.Length,
            totalRows);

        return new CapabilityExecutionEnvelope
        {
            CapabilityKey = compositeCapabilityKey,
            ExecutionMode = CompositeMode,
            Arguments = arguments,
            ResultSchema = ResultSchema.FromJson(composite.ResultSchema),
            Rows = BuildBundleRows(sections),
            RowCount = totalRows,
            Result = bundle,
            Status = sections.All(section => section.Success) ? "succeeded" : "partial_failure",
            Success = sections.All(section => section.Success),
            ExecutionMetadata = new ExecutionMetadata
            {
                Operation = CompositeMode,
                AdapterType = "composite",
                TenantId = context.TenantId,
                UserId = context.UserId,
                CorrelationId = context.CorrelationId,
                Detail = bundle
            },
            SensitivityPolicy = SensitivityPolicy.FromJson(composite.SensitivityPolicy),
            AnswerPolicy = AnswerPolicy.FromJson(composite.AnswerPolicy)
        };
    }

    private async Task<Dictionary<string, CapabilitySemanticMetadata?>> LoadSubMetadataAsync(
        IReadOnlyList<string> subCapabilities,
        string locale,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, CapabilitySemanticMetadata?>(StringComparer.OrdinalIgnoreCase);
        foreach (var subCapability in subCapabilities)
        {
            result[subCapability] = await _metadataRepository!.GetCapabilityMetadataAsync(subCapability, locale, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    private async Task<IReadOnlyList<CapabilityExecutionEnvelope>> ExecuteParallelAsync(
        IReadOnlyList<string> subCapabilities,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var tasks = subCapabilities
            .Select(subCapability => _executionFacade.ExecuteReadAsync(subCapability, arguments, cancellationToken))
            .ToArray();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<CapabilityExecutionEnvelope>> ExecuteSequentialAsync(
        IReadOnlyList<string> subCapabilities,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var result = new List<CapabilityExecutionEnvelope>(subCapabilities.Count);
        foreach (var subCapability in subCapabilities)
        {
            result.Add(await _executionFacade.ExecuteReadAsync(subCapability, arguments, cancellationToken)
                .ConfigureAwait(false));
        }

        return result;
    }

    private TilsoftExecutionContext CurrentContext() =>
        _executionContextAccessor?.Current ?? new TilsoftExecutionContext();

    private static bool IsReadOnly(string executionMode) =>
        executionMode.Equals("read", StringComparison.OrdinalIgnoreCase)
        || executionMode.Equals("read_only", StringComparison.OrdinalIgnoreCase)
        || executionMode.Equals("readonly", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> BuildBundleRows(
        IReadOnlyList<CompositeResultSection> sections) =>
        sections.Select(section => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["capabilityKey"] = section.CapabilityKey,
            ["success"] = section.Success,
            ["rowCount"] = section.RowCount,
            ["rows"] = section.Rows,
            ["errorCode"] = section.ErrorCode,
            ["errorMessage"] = section.ErrorMessage
        }).ToArray();

    private sealed record CompositeDefinition(
        IReadOnlyList<string> SubCapabilities,
        CompositeCompositionPolicy Policy)
    {
        public static CompositeDefinition Parse(CapabilitySemanticMetadata metadata)
        {
            var subCapabilities = ReadSubCapabilities(metadata);
            var policy = ReadPolicy(metadata) ?? new CompositeCompositionPolicy();
            return new CompositeDefinition(subCapabilities, policy);
        }

        private static IReadOnlyList<string> ReadSubCapabilities(CapabilitySemanticMetadata metadata)
        {
            var direct = ReadStringList(metadata.SubCapabilities);
            if (direct.Count > 0)
            {
                return direct;
            }

            using var document = TryParseJson(metadata.ArgumentContract);
            if (document is not null
                && document.RootElement.TryGetProperty("subCapabilities", out var subCapabilities))
            {
                return ReadStringList(subCapabilities);
            }

            return Array.Empty<string>();
        }

        private static CompositeCompositionPolicy? ReadPolicy(CapabilitySemanticMetadata metadata)
        {
            var direct = ReadPolicyJson(metadata.CompositionPolicy);
            if (direct is not null)
            {
                return direct;
            }

            using var document = TryParseJson(metadata.ArgumentContract);
            if (document is not null
                && document.RootElement.TryGetProperty("compositionPolicy", out var policy))
            {
                return ReadPolicyJson(policy.GetRawText());
            }

            return null;
        }
    }

    private static IReadOnlyList<string> ReadStringList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        using var document = TryParseJson(value);
        if (document is not null && document.RootElement.ValueKind == JsonValueKind.Array)
        {
            return ReadStringList(document.RootElement);
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static CompositeCompositionPolicy? ReadPolicyJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CompositeCompositionPolicy>(value, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonDocument? TryParseJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record CompositeCompositionPolicy
{
    public string Mode { get; init; } = "parallel";
    public string? JoinKey { get; init; }
    public string Output { get; init; } = "json_bundle";
}

public sealed record CompositeResultBundle
{
    public required string CapabilityKey { get; init; }
    public string Mode { get; init; } = "parallel";
    public string Output { get; init; } = "json_bundle";
    public string? JoinKey { get; init; }
    public required IReadOnlyList<CompositeResultSection> Sections { get; init; }
    public required IReadOnlyDictionary<string, int> RowCounts { get; init; }
}

public sealed record CompositeResultSection
{
    public required string CapabilityKey { get; init; }
    public bool Success { get; init; }
    public int RowCount { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = Array.Empty<IReadOnlyDictionary<string, object?>>();
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required ExecutionMetadata ExecutionMetadata { get; init; }
}
