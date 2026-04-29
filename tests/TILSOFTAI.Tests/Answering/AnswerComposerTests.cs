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
        block.Data.GetType().GetProperty("mode")!.GetValue(block.Data).Should().Be("raw_json");
        var argumentsProperty = block.Data.GetType().GetProperty("arguments")!.GetValue(block.Data);
        argumentsProperty.Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>();
        var rowsProperty = block.Data.GetType().GetProperty("rows")!.GetValue(block.Data);
        rowsProperty.Should().BeAssignableTo<IReadOnlyList<IReadOnlyDictionary<string, object?>>>();
        var rows = (IReadOnlyList<IReadOnlyDictionary<string, object?>>)rowsProperty!;
        rows[0]["Email"].Should().Be("***");
        rows[0].Should().NotContainKey("SecretNote");
        answer.Provenance.CapabilityKey.Should().Be("warehouse.inventory.by-item");
        answer.Provenance.CorrelationId.Should().Be("corr-1");
    }

    [Fact]
    public async Task RawJsonMode_WhenNoRows_ShouldReturnEnvelopeWithMetadata()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Mode = AnswerMode.RawJson,
            Rows = [],
            RowCount = 0
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        var block = answer.Blocks.OfType<RawJsonBlock>().Should().ContainSingle().Subject;
        block.Data.GetType().GetProperty("mode")!.GetValue(block.Data).Should().Be("raw_json");
        block.Data.GetType().GetProperty("capabilityKey")!.GetValue(block.Data).Should().Be("warehouse.inventory.by-item");
        block.Data.GetType().GetProperty("procedureName")!.GetValue(block.Data).Should().Be("dbo.ai_warehouse_inventory_by_item");
        block.Data.GetType().GetProperty("rowCount")!.GetValue(block.Data).Should().Be(0);
        var rows = block.Data.GetType().GetProperty("rows")!.GetValue(block.Data);
        rows.Should().BeAssignableTo<IReadOnlyList<IReadOnlyDictionary<string, object?>>>();
        ((IReadOnlyList<IReadOnlyDictionary<string, object?>>)rows!).Should().BeEmpty();
    }

    [Fact]
    public async Task StructuredMode_WhenRowsWithinLimit_ShouldReturnSummaryTableLabelsAndProvenance()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            CapabilityKey = "model.overview.by-code",
            ProcedureName = "dbo.ai_model_get_overview",
            Arguments = new Dictionary<string, object?> { ["modelCode"] = "ABC" },
            Rows =
            [
                new Dictionary<string, object?> { ["ModelCode"] = "ABC", ["ModelName"] = "Chair" }
            ],
            RowCount = 1,
            ResultSchema = new ResultSchema
            {
                Columns =
                [
                    new ResultColumn { Name = "ModelCode", Label = "Model Code", Type = "string" },
                    new ResultColumn { Name = "ModelName", Label = "Model Name", Type = "string" }
                ]
            }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("structured");
        answer.Text.Should().Contain("Found 1 rows for model.overview.by-code");
        answer.Blocks.OfType<SummaryBlock>().Should().ContainSingle();
        var table = answer.Blocks.OfType<TableBlock>().Should().ContainSingle().Subject;
        table.Columns.Should().Equal("Model Code", "Model Name");
        table.Rows.Should().ContainSingle();
        table.Truncated.Should().BeFalse();
        answer.Provenance.CapabilityKey.Should().Be("model.overview.by-code");
        answer.Provenance.ProcedureName.Should().Be("dbo.ai_model_get_overview");
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
        answer.Blocks.OfType<SummaryBlock>().Should().ContainSingle();
        var table = answer.Blocks.OfType<TableBlock>().Should().ContainSingle().Subject;
        table.Columns.Should().Equal("Warehouse", "Available Quantity");
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
        answer.Text.Should().Contain("ItemNo");
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<FollowUpBlock>();
    }

    [Fact]
    public async Task StructuredMode_WhenModelCodeIsMissing_ShouldAskForModelCode()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            CapabilityKey = "model.overview.by-code",
            ErrorCode = CapabilityExecutionFacade.ArgumentValidationFailedCode,
            MissingArguments = ["model_code"]
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("follow_up");
        answer.Text.Should().Contain("modelCode");
        answer.Text.Should().NotContain("model_code");
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
        answer.Text.Should().Contain("@ItemNo=MISSING");
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<TextBlock>();
    }

    [Fact]
    public async Task StructuredMode_WhenCapabilityExecutionFails_ShouldReturnErrorAnswer()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            ErrorCode = "SQL_TIMEOUT",
            ErrorMessage = "The model procedure timed out."
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("error");
        answer.Text.Should().Be("The model procedure timed out.");
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<TextBlock>();
        answer.Provenance.CorrelationId.Should().Be("corr-1");
    }

    [Fact]
    public async Task StructuredMode_WhenVietnamese_ShouldUseAccentedText()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Locale = "vi-VN",
            Rows =
            [
                new Dictionary<string, object?> { ["ModelCode"] = "ABC" }
            ],
            RowCount = 1
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.Text.Should().Contain("Tìm thấy");
        answer.Text.Should().Contain("hiển thị");
        answer.Text.Should().NotContain("Tim thay");
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
        answer.Blocks.OfType<SummaryBlock>().Should().ContainSingle();
        answer.Blocks.OfType<TextBlock>().Should().HaveCount(2);
        var tables = answer.Blocks.OfType<TableBlock>().ToArray();
        tables.Should().HaveCount(2);
        tables[0].Rows.Should().ContainSingle();
        tables[0].Rows[0].Should().Contain("***");
        tables[0].Truncated.Should().BeTrue();
        answer.Detail.Should().NotBeSameAs(bundle);
        answer.Detail!.GetType().GetProperty("mode")!.GetValue(answer.Detail).Should().Be("structured");
        var safeBundle = answer.Detail.GetType().GetProperty("result")!.GetValue(answer.Detail)
            .Should().BeOfType<CompositeResultBundle>().Subject;
        safeBundle.Sections[0].Rows[0]["Email"].Should().Be("***");
    }

    [Fact]
    public async Task StructuredMode_WhenWritePreview_ShouldReturnConfirmationBlock()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            CapabilityKey = "sales.order.create-preview",
            Locale = "vi-VN",
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
        answer.Text.Should().Be("Kiểm tra trước thao tác sales.order.create-preview.");
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<ConfirmationBlock>();
        var block = (ConfirmationBlock)answer.Blocks[0];
        block.Title.Should().Be("Xác nhận thao tác");
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
