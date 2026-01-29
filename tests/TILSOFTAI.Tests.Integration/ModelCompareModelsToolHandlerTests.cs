using System.Text.Json;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Modules.Model.Tools;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;
using Xunit;

namespace TILSOFTAI.Tests.Integration;

public sealed class ModelCompareModelsToolHandlerTests
{
    [Fact]
    public async Task ModelCompareModels_PassesModelIdsJsonAndReturnsJson()
    {
        var executor = new CapturingSqlExecutor();
        var handler = new ModelCompareModelsToolHandler(executor);
        var tool = new ToolDefinition
        {
            Name = "model_compare_models",
            SpName = "dbo.ai_model_compare_models"
        };
        var context = new TilsoftExecutionContext
        {
            TenantId = "tenant-1",
            Language = "vi"
        };

        var argumentsJson = "{\"modelIds\":[101,202,303]}";

        var result = await handler.ExecuteAsync(tool, argumentsJson, context, CancellationToken.None);

        Assert.Equal("dbo.ai_model_compare_models", executor.StoredProcedure);
        Assert.True(executor.Parameters.TryGetValue("@ModelIdsJson", out var modelIdsJson));
        Assert.Equal("[101,202,303]", modelIdsJson);
        Assert.Equal("tenant-1", executor.Parameters["@TenantId"]);
        Assert.Equal("vi", executor.Parameters["@Language"]);

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("meta", out _));
    }

    private sealed class CapturingSqlExecutor : ISqlExecutor
    {
        public string? StoredProcedure { get; private set; }
        public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<string> ExecuteAsync(
            string storedProcedure,
            IReadOnlyDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            StoredProcedure = storedProcedure;
            Parameters.Clear();
            foreach (var pair in parameters)
            {
                Parameters[pair.Key] = pair.Value;
            }

            var payload = "{\"meta\":{\"tenantId\":\"tenant-1\",\"generatedAtUtc\":\"2026-01-01T00:00:00Z\"},\"columns\":[],\"rows\":[]}";
            return Task.FromResult(payload);
        }

        public Task<string> ExecuteToolAsync(string storedProcedure, string tenantId, string argumentsJson, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<string> ExecuteAtomicPlanAsync(string storedProcedure, string tenantId, string planJson, string callerUserId, string callerRoles, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<string> ExecuteDiagnosticsAsync(string storedProcedure, string tenantId, string module, string ruleKey, string? inputJson, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteQueryAsync(string storedProcedure, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<string> ExecuteWriteActionAsync(string storedProcedure, string tenantId, string argumentsJson, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
