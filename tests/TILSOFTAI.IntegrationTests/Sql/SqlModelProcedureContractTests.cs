using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace TILSOFTAI.IntegrationTests.Sql;

public sealed class SqlModelProcedureContractTests
{
    private const string RunSqlIntegrationTestsFlag = "TILSOFTAI_RUN_SQL_INTEGRATION_TESTS";
    private const string ConnectionStringVariable = "TILSOFTAI_SQL_CONNECTION_STRING";
    private const string DefaultConnectionString =
        "Server=localhost;Database=TILSOFTAI;User Id=sa;Password=123;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True";

    [Fact]
    public async Task Sql_ModelCount_ReturnsEnvelope()
    {
        if (!ShouldRunSqlIntegrationTests())
            return;

        using var envelope = await ExecuteEnvelopeAsync("dbo.ai_model_count", "{}");

        AssertEnvelope(envelope, "model.count", "ok");
    }

    [Fact]
    public async Task Sql_ModelOverview_ByCode_ReturnsEnvelope()
    {
        if (!ShouldRunSqlIntegrationTests())
            return;

        using var envelope = await ExecuteEnvelopeAsync("dbo.ai_model_get_overview", """{"modelCode":"ABC"}""");

        AssertEnvelope(envelope, "model.overview.by-code", "ok");
    }

    [Fact]
    public async Task Sql_ModelPieces_ByCode_ReturnsEnvelope()
    {
        if (!ShouldRunSqlIntegrationTests())
            return;

        using var envelope = await ExecuteEnvelopeAsync("dbo.ai_model_get_pieces", """{"modelCode":"SET-DINING-001"}""");

        AssertEnvelope(envelope, "model.pieces.by-code", "ok");
    }

    [Fact]
    public async Task Sql_ModelMaterials_ByCode_ReturnsEnvelope()
    {
        if (!ShouldRunSqlIntegrationTests())
            return;

        using var envelope = await ExecuteEnvelopeAsync("dbo.ai_model_get_materials", """{"modelCode":"ABC"}""");

        AssertEnvelope(envelope, "model.materials.by-code", "ok");
    }

    [Fact]
    public async Task Sql_ModelCompare_ByCodes_ReturnsEnvelope()
    {
        if (!ShouldRunSqlIntegrationTests())
            return;

        using var envelope = await ExecuteEnvelopeAsync("dbo.ai_model_compare", """{"modelCodes":["ABC","XYZ"]}""");

        AssertEnvelope(envelope, "model.compare", "ok");
    }

    [Fact]
    public async Task Sql_ModelPackaging_ByCode_ReturnsEnvelope()
    {
        if (!ShouldRunSqlIntegrationTests())
            return;

        using var envelope = await ExecuteEnvelopeAsync("dbo.ai_model_get_packaging", """{"modelCode":"ABC"}""");

        AssertEnvelope(envelope, "model.packaging.by-code", "ok");
    }

    [Fact]
    public async Task Sql_MissingModelCode_ReturnsControlledValidation()
    {
        if (!ShouldRunSqlIntegrationTests())
            return;

        using var envelope = await ExecuteEnvelopeAsync("dbo.ai_model_get_overview", "{}");

        AssertEnvelope(envelope, "model.overview.by-code", "validation_error");
        envelope.RootElement.GetProperty("meta").GetProperty("message").GetString()
            .Should().Be("modelCode is required.");
    }

    private static bool ShouldRunSqlIntegrationTests() =>
        string.Equals(Environment.GetEnvironmentVariable(RunSqlIntegrationTestsFlag), "true", StringComparison.OrdinalIgnoreCase);

    private static async Task<JsonDocument> ExecuteEnvelopeAsync(string procedureName, string argsJson)
    {
        await using var connection = new SqlConnection(
            Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? DefaultConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.Add(new SqlParameter("@TenantId", System.Data.SqlDbType.NVarChar, 50) { Value = "default" });
        command.Parameters.Add(new SqlParameter("@ArgsJson", System.Data.SqlDbType.NVarChar) { Value = argsJson });

        var result = await command.ExecuteScalarAsync();
        var resultJson = result.Should().BeOfType<string>().Subject;
        return JsonDocument.Parse(resultJson);
    }

    private static void AssertEnvelope(JsonDocument envelope, string expectedCapabilityKey, string expectedStatus)
    {
        var root = envelope.RootElement;
        root.TryGetProperty("meta", out var meta).Should().BeTrue();
        root.TryGetProperty("columns", out var columns).Should().BeTrue();
        root.TryGetProperty("rows", out var rows).Should().BeTrue();

        meta.GetProperty("capabilityKey").GetString().Should().Be(expectedCapabilityKey);
        meta.GetProperty("status").GetString().Should().Be(expectedStatus);
        meta.GetProperty("rowCount").ValueKind.Should().Be(JsonValueKind.Number);
        columns.ValueKind.Should().Be(JsonValueKind.Array);
        rows.ValueKind.Should().Be(JsonValueKind.Array);
    }
}
