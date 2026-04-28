using FluentAssertions;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Execution;
using Xunit;

namespace TILSOFTAI.Tests.Answering;

public sealed class AnswerComposerTests
{
    [Fact]
    public async Task RawJsonMode_ShouldMaskSensitiveColumnsAndReturnDeterministicBlock()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Mode = AnswerMode.RawJson,
            Rows =
            [
                new Dictionary<string, object?>
                {
                    ["CustomerName"] = "Acme",
                    ["Email"] = "ops@example.test",
                    ["SecretNote"] = "internal"
                }
            ],
            RowCount = 1,
            SensitivityPolicy = new SensitivityPolicy
            {
                MaskColumns = ["Email"],
                HiddenColumns = ["SecretNote"]
            }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("raw_json");
        answer.Text.Should().BeEmpty();
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<RawJsonBlock>();
        var block = (RawJsonBlock)answer.Blocks[0];
        block.Data!.GetType().GetProperty("mode")!.GetValue(block.Data).Should().Be("raw_json");
        var argumentsProperty = block.Data.GetType().GetProperty("arguments")!.GetValue(block.Data);
        argumentsProperty.Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>();
        var rowsProperty = block.Data!.GetType().GetProperty("rows")!.GetValue(block.Data);
        rowsProperty.Should().BeAssignableTo<IReadOnlyList<IReadOnlyDictionary<string, object?>>>();
        var rows = (IReadOnlyList<IReadOnlyDictionary<string, object?>>)rowsProperty!;
        rows[0]["Email"].Should().Be("***");
        rows[0].Should().NotContainKey("SecretNote");
        answer.Provenance.CapabilityKey.Should().Be("warehouse.inventory.by-item");
        answer.Provenance.CorrelationId.Should().Be("corr-1");
    }

    [Fact]
    public async Task StructuredMode_WhenLargeResult_ShouldReturnSummaryTableChartAndTruncationFollowUp()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Rows =
            [
                new Dictionary<string, object?> { ["WarehouseName"] = "BD", ["AvailableQty"] = 20m },
                new Dictionary<string, object?> { ["WarehouseName"] = "LA", ["AvailableQty"] = 10m },
                new Dictionary<string, object?> { ["WarehouseName"] = "NY", ["AvailableQty"] = 5m }
            ],
            RowCount = 3,
            AnswerPolicy = new AnswerPolicy { MaxRowsForChat = 2 },
            ResultSchema = new ResultSchema
            {
                Columns =
                [
                    new ResultColumn { Name = "WarehouseName", Label = "Warehouse", Type = "string", Role = "dimension" },
                    new ResultColumn { Name = "AvailableQty", Label = "Available Quantity", Type = "decimal", Role = "measure" }
                ],
                ChartHints =
                [
                    new ResultChartHint { Type = "bar", Category = "WarehouseName", Value = "AvailableQty" }
                ]
            }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("structured");
        answer.Blocks.OfType<TextBlock>().Should().ContainSingle();
        var table = answer.Blocks.OfType<TableBlock>().Should().ContainSingle().Subject;
        table.Rows.Should().HaveCount(2);
        table.TotalRows.Should().Be(3);
        table.Truncated.Should().BeTrue();
        var chart = answer.Blocks.OfType<ChartBlock>().Should().ContainSingle().Subject;
        chart.ChartType.Should().Be("bar");
        chart.CategoryField.Should().Be("WarehouseName");
        chart.ValueField.Should().Be("AvailableQty");
        answer.FollowUpQuestions.Should().ContainSingle();
    }

    [Fact]
    public async Task StructuredMode_WhenValidationFailed_ShouldReturnFollowUpBlock()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            ErrorCode = CapabilityExecutionFacade.ArgumentValidationFailedCode,
            MissingArguments = ["@ItemNo"],
            ErrorMessage = "missing item"
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("follow_up");
        answer.Text.Should().Contain("@ItemNo");
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<FollowUpBlock>();
    }

    [Fact]
    public async Task StructuredMode_WhenNoRows_ShouldExplainNoData()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Rows = [],
            RowCount = 0,
            Arguments = new Dictionary<string, object?> { ["@ItemNo"] = "MISSING" }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("no_data");
        answer.Text.Should().Contain("No data");
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<TextBlock>();
    }

    [Fact]
    public async Task StructuredMode_WhenCompositeBundle_ShouldSummarizeSectionsAndTables()
    {
        var composer = CreateComposer();
        var bundle = new CompositeResultBundle
        {
            CapabilityKey = "sales.customer.360",
            Mode = "parallel",
            Output = "json_bundle",
            Sections =
            [
                new CompositeResultSection
                {
                    CapabilityKey = "sales.orders.by-customer",
                    Success = true,
                    RowCount = 2,
                    Rows =
                    [
                        new Dictionary<string, object?> { ["OrderNo"] = "SO-001", ["Email"] = "ops@example.test" },
                        new Dictionary<string, object?> { ["OrderNo"] = "SO-002", ["Email"] = "ops2@example.test" }
                    ],
                    ExecutionMetadata = ExecutionMetadata.Empty
                },
                new CompositeResultSection
                {
                    CapabilityKey = "accounting.receivables.by-customer",
                    Success = true,
                    RowCount = 1,
                    Rows =
                    [
                        new Dictionary<string, object?> { ["InvoiceNo"] = "AR-001", ["Email"] = "ar@example.test" }
                    ],
                    ExecutionMetadata = ExecutionMetadata.Empty
                }
            ],
            RowCounts = new Dictionary<string, int>
            {
                ["sales.orders.by-customer"] = 2,
                ["accounting.receivables.by-customer"] = 1
            }
        };
        var request = Request() with
        {
            CapabilityKey = "sales.customer.360",
            Result = bundle,
            RowCount = 3,
            AnswerPolicy = new AnswerPolicy { MaxRowsForChat = 1 },
            SensitivityPolicy = new SensitivityPolicy { MaskColumns = ["Email"] }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("composite");
        answer.Text.Should().Contain("Compiled 2 sections");
        answer.Blocks.OfType<TextBlock>().Should().HaveCount(3);
        var tables = answer.Blocks.OfType<TableBlock>().ToArray();
        tables.Should().HaveCount(2);
        tables[0].Rows.Should().ContainSingle();
        tables[0].Rows[0].Should().Contain("***");
        tables[0].Truncated.Should().BeTrue();
        answer.Detail.Should().NotBeSameAs(bundle);
        var safeBundle = answer.Detail.Should().BeOfType<CompositeResultBundle>().Subject;
        safeBundle.Sections[0].Rows[0]["Email"].Should().Be("***");
    }

    [Fact]
    public async Task StructuredMode_WhenWritePreview_ShouldReturnConfirmationBlock()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            ExecutionMetadata = new ExecutionMetadata
            {
                CorrelationId = "corr-1",
                Operation = "write_preview"
            },
            DraftAction = new Dictionary<string, object?>
            {
                ["actionId"] = "action-32-6",
                ["capabilityKey"] = "sales.order.create-preview",
                ["Email"] = "buyer@example.test",
                ["SecretNote"] = "internal"
            },
            SensitivityPolicy = new SensitivityPolicy
            {
                MaskColumns = ["Email"],
                HiddenColumns = ["SecretNote"]
            }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("confirmation");
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<ConfirmationBlock>();
        var block = (ConfirmationBlock)answer.Blocks[0];
        block.DraftAction["actionId"].Should().Be("action-32-6");
        block.DraftAction["Email"].Should().Be("***");
        block.DraftAction.Should().NotContainKey("SecretNote");
    }

    private static StructuredAnswerComposer CreateComposer() =>
        new(new RawJsonAnswerComposer(), new AiSummaryService());

    private static AnswerComposerRequest Request() => new()
    {
        Mode = AnswerMode.Structured,
        CapabilityKey = "warehouse.inventory.by-item",
        ProcedureName = "dbo.ai_warehouse_inventory_by_item",
        Arguments = new Dictionary<string, object?> { ["@ItemNo"] = "MADEIRA-BLK" },
        ResultSchema = null,
        Result = null,
        Rows = [],
        RowCount = 0,
        ExecutionMetadata = new ExecutionMetadata
        {
            CorrelationId = "corr-1",
            Operation = "execute_query"
        },
        SensitivityPolicy = SensitivityPolicy.Default,
        Locale = "en-US",
        AnswerPolicy = AnswerPolicy.Default
    };
}
