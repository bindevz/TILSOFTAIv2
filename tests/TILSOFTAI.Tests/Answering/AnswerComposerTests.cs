using FluentAssertions;
using System.Text.Json;
using TILSOFTAI.Orchestration.Answering;
using TILSOFTAI.Orchestration.Answering.Narration;
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
        block.Data.GetType().GetProperty("sensitivityPolicyApplied")!.GetValue(block.Data)
            .Should().BeOfType<SensitivityPolicy>();
        block.Data.GetType().GetProperty("functionName")!.GetValue(block.Data)
            .Should().Be("warehouse.inventory.by-item");
        var rows = (IReadOnlyList<IReadOnlyDictionary<string, object?>>)rowsProperty!;
        rows[0]["Email"].Should().Be("***");
        rows[0].Should().NotContainKey("SecretNote");
        answer.Provenance.CapabilityKey.Should().Be("warehouse.inventory.by-item");
        answer.Provenance.CorrelationId.Should().Be("corr-1");
        answer.CorrelationId.Should().Be("corr-1");
        answer.Locale.Should().Be("en-US");
    }

    [Fact]
    public async Task RawJsonMode_WhenNoRows_ShouldReturnEnvelopeWithMetadata()
    {
        var composer = CreateComposer("Tìm thấy 1 dòng; hiển thị kết quả.");
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
        answer.Text.Should().Be("AI-generated summary from fake narrator.");
        answer.Blocks.OfType<SummaryBlock>().Should().ContainSingle();
        var table = answer.Blocks.OfType<TableBlock>().Should().ContainSingle().Subject;
        table.Columns.Should().Equal("Model Code", "Model Name");
        table.Rows.Should().ContainSingle();
        table.RowCount.Should().Be(1);
        table.DisplayedRows.Should().Be(1);
        table.Truncated.Should().BeFalse();
        answer.Provenance.CapabilityKey.Should().Be("model.overview.by-code");
        answer.Provenance.ProcedureName.Should().Be("dbo.ai_model_get_overview");
    }

    [Fact]
    public async Task Structured_ModelOverview_ReturnsBlocksAndProvenance()
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
        answer.Text.Should().NotBeNullOrWhiteSpace();
        answer.Blocks.OfType<SummaryBlock>().Should().ContainSingle();
        answer.Blocks.OfType<TableBlock>().Should().ContainSingle();
        answer.Provenance.CapabilityKey.Should().Be("model.overview.by-code");
        answer.Provenance.ProcedureName.Should().Be("dbo.ai_model_get_overview");
        answer.Provenance.CorrelationId.Should().Be("corr-1");
        answer.Detail.Should().NotBeNull();
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
        table.RowCount.Should().Be(3);
        table.DisplayedRows.Should().Be(2);
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
        answer.Text.Should().Be("Please provide: model_code.");
    }

    [Fact]
    public async Task Structured_MissingModelCode_ReturnsFollowUp()
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
        answer.FollowUpQuestions.Should().ContainSingle();
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<FollowUpBlock>();
        answer.Text.Should().Be("Please provide: model_code.");
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
        answer.Text.Should().Contain("Filters used:");
        answer.Text.Should().Contain("- ItemNo: MISSING");
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<TextBlock>();
    }

    [Fact]
    public async Task Structured_EmptyRows_ReturnsNoData()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Rows = [],
            RowCount = 0,
            Arguments = new Dictionary<string, object?> { ["modelCode"] = "MISSING" }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("no_data");
        answer.Text.Should().Contain("No data");
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<TextBlock>();
        answer.Provenance.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Structured_DoesNotExposeMaskedColumns()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Rows =
            [
                new Dictionary<string, object?>
                {
                    ["ModelCode"] = "ABC",
                    ["Cost"] = 123.45m,
                    ["InternalMargin"] = 42m
                }
            ],
            RowCount = 1,
            ResultSchema = new ResultSchema
            {
                Columns =
                [
                    new ResultColumn { Name = "ModelCode", Label = "Model Code", Type = "string" },
                    new ResultColumn { Name = "Cost", Label = "Cost", Type = "decimal" },
                    new ResultColumn { Name = "InternalMargin", Label = "Internal Margin", Type = "decimal" }
                ]
            },
            SensitivityPolicy = new SensitivityPolicy
            {
                MaskColumns = ["Cost"],
                HiddenColumns = ["InternalMargin"]
            }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        var table = answer.Blocks.OfType<TableBlock>().Should().ContainSingle().Subject;
        table.Columns.Should().Equal("Model Code", "Cost");
        table.Columns.Should().NotContain("Internal Margin");
        table.Rows[0].Should().Contain("***");
        table.Rows[0].Should().NotContain(42m);
        answer.Detail!.ToString().Should().NotContain("InternalMargin");
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
        var composer = CreateComposer("Tìm thấy 1 dòng; hiển thị kết quả.");
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

        answer.AnswerType.Should().Be("structured");
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

        answer.AnswerType.Should().Be("write_preview");
        answer.Text.Should().Be("Kiểm tra trước thao tác sales.order.create-preview.");
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<ConfirmationBlock>();
        var block = (ConfirmationBlock)answer.Blocks[0];
        block.Title.Should().Be("Xác nhận thao tác");
        block.DraftAction["actionId"].Should().Be("action-32-6");
        block.DraftAction["Email"].Should().Be("***");
        block.DraftAction.Should().NotContainKey("SecretNote");
    }

    [Fact]
    public async Task Structured_Table_UsesVietnameseLabels()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Locale = "vi-VN",
            CapabilityKey = "model.overview.by-code",
            Arguments = new Dictionary<string, object?> { ["modelCode"] = "ABC" },
            Rows =
            [
                new Dictionary<string, object?> { ["ModelCode"] = "ABC", ["ModelName"] = "Ghế" }
            ],
            RowCount = 1,
            ResultSchema = new ResultSchema
            {
                Columns =
                [
                    new ResultColumn { Name = "ModelCode", Label = "Model Code", LabelVi = "Mã model", Type = "string", Role = "dimension" },
                    new ResultColumn { Name = "ModelName", Label = "Model Name", LabelVi = "Tên model", Type = "string", Role = "dimension" }
                ]
            }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        var table = answer.Blocks.OfType<TableBlock>().Should().ContainSingle().Subject;
        table.Columns.Should().Equal("Mã model", "Tên model");
        answer.Locale.Should().Be("vi-VN");
    }

    [Fact]
    public async Task Structured_Table_UsesEnglishLabels()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            CapabilityKey = "model.overview.by-code",
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
                    new ResultColumn { Name = "ModelCode", Label = "Model Code", LabelVi = "Mã model", Type = "string", Role = "dimension" },
                    new ResultColumn { Name = "ModelName", Label = "Model Name", LabelVi = "Tên model", Type = "string", Role = "dimension" }
                ]
            }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.Blocks.OfType<TableBlock>().Single().Columns.Should().Equal("Model Code", "Model Name");
    }

    [Fact]
    public async Task AnswerComposer_UsesSqlResultSchemaLabels()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            CapabilityKey = "model.materials.by-code",
            Rows =
            [
                new Dictionary<string, object?>
                {
                    ["ModelCode"] = "ABC",
                    ["MaterialCode"] = "MAT-1",
                    ["PrivateCost"] = 12.34m
                }
            ],
            RowCount = 1,
            ResultSchema = new ResultSchema
            {
                Columns =
                [
                    new ResultColumn { Name = "ModelCode", Label = "Model", Type = "string" },
                    new ResultColumn { Name = "MaterialCode", Label = "Material", Type = "string" },
                    new ResultColumn { Name = "PrivateCost", Label = "Private Cost", Type = "decimal", Visible = false }
                ]
            }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        var table = answer.Blocks.OfType<TableBlock>().Single();
        table.Columns.Should().Equal("Model", "Material");
        table.Rows[0].Should().Equal("ABC", "MAT-1");
    }

    [Fact]
    public async Task Structured_Table_HidesInvisibleColumns()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Rows =
            [
                new Dictionary<string, object?> { ["ModelCode"] = "ABC", ["InternalNote"] = "hide me", ["UnknownColumn"] = "not rendered" }
            ],
            RowCount = 1,
            ResultSchema = new ResultSchema
            {
                Columns =
                [
                    new ResultColumn { Name = "ModelCode", Label = "Model Code", Type = "string", Visible = true },
                    new ResultColumn { Name = "InternalNote", Label = "Internal Note", Type = "string", Visible = false }
                ]
            }
        };

        var table = (await composer.ComposeAsync(request, CancellationToken.None))
            .Blocks.OfType<TableBlock>().Single();

        table.Columns.Should().Equal("Model Code");
        table.Rows[0].Should().Equal("ABC");
    }

    [Theory]
    [InlineData("model.count")]
    [InlineData("model.overview.by-code")]
    [InlineData("model.pieces.by-code")]
    [InlineData("model.materials.by-code")]
    [InlineData("model.packaging.by-code")]
    [InlineData("model.compare")]
    public async Task AnswerComposer_Structured_AllCapabilitiesUseNarrationService(string capabilityKey)
    {
        var composer = CreateComposer(out var narrator);
        var request = ModelCapabilityRequest(capabilityKey);

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("structured");
        answer.Text.Should().Be("AI-generated summary from fake narrator.");
        answer.Blocks.OfType<TableBlock>().Should().ContainSingle();
        narrator.Requests.Should().ContainSingle().Which.CapabilityKey.Should().Be(capabilityKey);
    }

    [Theory]
    [InlineData("model.count")]
    [InlineData("model.overview.by-code")]
    [InlineData("model.pieces.by-code")]
    [InlineData("model.materials.by-code")]
    [InlineData("model.compare")]
    [InlineData("model.packaging.by-code")]
    public async Task AnswerComposer_RawJson_AllModelCapabilities(string capabilityKey)
    {
        var composer = CreateComposer();
        var request = ModelCapabilityRequest(capabilityKey) with { Mode = AnswerMode.RawJson };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("raw_json");
        var detail = answer.Detail!;
        detail.GetType().GetProperty("mode")!.GetValue(detail).Should().Be("raw_json");
        detail.GetType().GetProperty("capabilityKey")!.GetValue(detail).Should().Be(capabilityKey);
        detail.GetType().GetProperty("functionName")!.GetValue(detail).Should().Be(capabilityKey);
        detail.GetType().GetProperty("resultSchema")!.GetValue(detail).Should().BeOfType<ResultSchema>();
        detail.GetType().GetProperty("executionMetadata")!.GetValue(detail).Should().BeOfType<ExecutionMetadata>();
        detail.GetType().GetProperty("sensitivityPolicyApplied")!.GetValue(detail).Should().BeOfType<SensitivityPolicy>();
    }

    [Fact]
    public async Task AnswerComposer_Localization_ViAndEn()
    {
        var composer = CreateComposer(out var narrator);
        var vi = await composer.ComposeAsync(
            ModelCapabilityRequest("model.materials.by-code") with { Locale = "vi-VN" },
            CancellationToken.None);
        var en = await composer.ComposeAsync(
            ModelCapabilityRequest("model.materials.by-code") with { Locale = "en-US" },
            CancellationToken.None);

        vi.Text.Should().Be("AI-generated summary from fake narrator.");
        en.Text.Should().Be("AI-generated summary from fake narrator.");
        narrator.Requests.Select(request => request.Locale).Should().Equal("vi-VN", "en-US");
    }

    [Fact]
    public async Task Structured_MissingModelCode_AsksSpecificQuestion()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Locale = "vi-VN",
            CapabilityKey = "model.materials.by-code",
            ErrorCode = CapabilityExecutionFacade.ArgumentValidationFailedCode,
            MissingArguments = ["model_code"]
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("follow_up");
        answer.Text.Should().Be("Vui lòng cung cấp: model_code.");
    }

    [Fact]
    public async Task Structured_NoData_IncludesUsedFilter()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Locale = "vi-VN",
            CapabilityKey = "model.overview.by-code",
            Arguments = new Dictionary<string, object?> { ["modelCode"] = "ABC" },
            Rows = [],
            RowCount = 0
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("no_data");
        answer.Text.Should().Contain("Không tìm thấy dữ liệu cho model.overview.by-code.");
        answer.Text.Should().Contain("Điều kiện đã dùng:");
        answer.Text.Should().Contain("- modelCode: ABC");
    }

    [Fact]
    public async Task Structured_NoData_DoesNotInventAlternatives()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            CapabilityKey = "model.overview.by-code",
            Arguments = new Dictionary<string, object?> { ["modelCode"] = "ABC" },
            Rows = [],
            RowCount = 0
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.Text.Should().NotContain("similar", "no-data should not invent alternatives");
        answer.Text.Should().NotContain("try", "no-data should not invent alternatives");
        answer.FollowUpQuestions.Should().BeEmpty();
    }

    [Fact]
    public async Task AnswerComposer_NoData()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            CapabilityKey = "model.overview.by-code",
            Arguments = new Dictionary<string, object?> { ["modelCode"] = "ABC" },
            Rows = [],
            RowCount = 0
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        answer.AnswerType.Should().Be("no_data");
        answer.Text.Should().Contain("No data was found for model.overview.by-code.");
        answer.Text.Should().Contain("Filters used:");
        answer.Text.Should().Contain("- modelCode: ABC");
        answer.FollowUpQuestions.Should().BeEmpty();
    }

    [Fact]
    public async Task AnswerComposer_FollowUp()
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
        answer.Text.Should().Be("Please provide: model_code.");
        answer.Blocks.Should().ContainSingle().Which.Should().BeOfType<FollowUpBlock>();
    }

    [Fact]
    public async Task AnswerComposer_Truncation()
    {
        var composer = CreateComposer();
        var rows = Enumerable.Range(1, 21)
            .Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["ModelCode"] = $"M{i:00}" })
            .ToArray();
        var request = Request() with
        {
            Rows = rows,
            RowCount = 21,
            ResultSchema = new ResultSchema
            {
                Columns = [new ResultColumn { Name = "ModelCode", Label = "Model Code", Type = "string" }]
            }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        var table = answer.Blocks.OfType<TableBlock>().Single();
        table.RowCount.Should().Be(21);
        table.DisplayedRows.Should().Be(20);
        table.Rows.Should().HaveCount(20);
        table.Truncated.Should().BeTrue();
        answer.FollowUpQuestions.Should().ContainSingle().Which.Should().Contain("Narrow the filters");
    }

    [Fact]
    public async Task AnswerComposer_SensitiveFieldMasking()
    {
        var composer = CreateComposer();
        var request = Request() with
        {
            Rows =
            [
                new Dictionary<string, object?>
                {
                    ["ModelCode"] = "ABC",
                    ["Cost"] = 123.45m,
                    ["InternalMargin"] = 42m
                }
            ],
            RowCount = 1,
            ResultSchema = new ResultSchema
            {
                Columns =
                [
                    new ResultColumn { Name = "ModelCode", Label = "Model Code", Type = "string" },
                    new ResultColumn { Name = "Cost", Label = "Cost", Type = "decimal" },
                    new ResultColumn { Name = "InternalMargin", Label = "Internal Margin", Type = "decimal" }
                ]
            },
            SensitivityPolicy = new SensitivityPolicy
            {
                MaskColumns = ["Cost"],
                HiddenColumns = ["InternalMargin"]
            }
        };

        var answer = await composer.ComposeAsync(request, CancellationToken.None);

        var table = answer.Blocks.OfType<TableBlock>().Single();
        table.Columns.Should().Equal("Model Code", "Cost");
        table.Rows[0].Should().Contain("***");
        table.Columns.Should().NotContain("Internal Margin");
        answer.Detail!.ToString().Should().NotContain("InternalMargin");
    }

    [Fact]
    public async Task AnswerComposer_CanRunWithFallbackNarrationService()
    {
        var composer = new StructuredAnswerComposer(
            new RawJsonAnswerComposer(),
            new FallbackAnswerNarrationService());

        var answer = await composer.ComposeAsync(ModelCapabilityRequest("model.overview.by-code"), CancellationToken.None);

        answer.AnswerType.Should().Be("structured");
        answer.Text.Should().Contain("Found 1 rows");
    }

    [Fact]
    public async Task StructuredAnswerComposer_UsesNarrationServiceText()
    {
        var composer = CreateComposer(out var narrator);

        var answer = await composer.ComposeAsync(ModelCapabilityRequest("model.overview.by-code"), CancellationToken.None);

        answer.Text.Should().Be("AI-generated summary from fake narrator.");
        answer.Blocks.OfType<SummaryBlock>().Single().Content.Should().Be("AI-generated summary from fake narrator.");
        narrator.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task StructuredAnswerComposer_SendsSanitizedRowsToNarrator()
    {
        var composer = CreateComposer(out var narrator);
        var request = Request() with
        {
            Rows =
            [
                new Dictionary<string, object?>
                {
                    ["ItemNo"] = "A-1",
                    ["Cost"] = 10m,
                    ["InternalNote"] = "hide"
                }
            ],
            RowCount = 1,
            Arguments = new Dictionary<string, object?>
            {
                ["ItemNo"] = "A-1",
                ["Cost"] = 10m,
                ["InternalNote"] = "hide"
            },
            SensitivityPolicy = new SensitivityPolicy
            {
                MaskColumns = ["Cost"],
                HiddenColumns = ["InternalNote"]
            }
        };

        await composer.ComposeAsync(request, CancellationToken.None);

        var narrationRequest = narrator.Requests.Should().ContainSingle().Subject;
        narrationRequest.Rows[0].Should().ContainKey("Cost").WhoseValue.Should().Be("***");
        narrationRequest.Rows[0].Should().NotContainKey("InternalNote");
        narrationRequest.Arguments["Cost"].Should().Be("***");
        narrationRequest.Arguments.Should().NotContainKey("InternalNote");
    }

    [Fact]
    public async Task StructuredAnswerComposer_DoesNotCallNarratorForRawJson()
    {
        var composer = CreateComposer(out var narrator);

        await composer.ComposeAsync(ModelCapabilityRequest("model.overview.by-code") with { Mode = AnswerMode.RawJson }, CancellationToken.None);

        narrator.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task StructuredAnswerComposer_DoesNotCallNarratorForNoData()
    {
        var composer = CreateComposer(out var narrator);

        await composer.ComposeAsync(Request() with { Rows = [], RowCount = 0 }, CancellationToken.None);

        narrator.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task StructuredAnswerComposer_DoesNotCallNarratorForFollowUp()
    {
        var composer = CreateComposer(out var narrator);

        await composer.ComposeAsync(Request() with { ClarificationQuestion = "Which item?", MissingArguments = ["itemNo"] }, CancellationToken.None);

        narrator.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AnswerPolicy_SummaryModeAi_CallsNarrator()
    {
        var composer = CreateComposer(out var narrator);

        var answer = await composer.ComposeAsync(
            ModelCapabilityRequest("model.overview.by-code") with
            {
                AnswerPolicy = AnswerPolicy.Default with
                {
                    Summary = new SummaryPolicy { Mode = SummaryPolicy.ModeAi }
                }
            },
            CancellationToken.None);

        answer.Text.Should().Be("AI-generated summary from fake narrator.");
        narrator.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task AnswerPolicy_SummaryModeFallback_UsesFallback()
    {
        var composer = CreateComposer(out var narrator);

        var answer = await composer.ComposeAsync(
            ModelCapabilityRequest("model.overview.by-code") with
            {
                AnswerPolicy = AnswerPolicy.Default with
                {
                    Summary = new SummaryPolicy { Mode = SummaryPolicy.ModeFallback }
                }
            },
            CancellationToken.None);

        answer.Text.Should().Contain("Found 1 rows");
        narrator.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AnswerPolicy_SummaryModeDisabled_DoesNotCallNarrator()
    {
        var composer = CreateComposer(out var narrator);

        var answer = await composer.ComposeAsync(
            ModelCapabilityRequest("model.overview.by-code") with
            {
                AnswerPolicy = AnswerPolicy.Default with
                {
                    Summary = new SummaryPolicy { Mode = SummaryPolicy.ModeDisabled }
                }
            },
            CancellationToken.None);

        answer.Text.Should().Be("Found 1 rows.");
        narrator.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AnswerPolicy_MaxRowsForNarration_LimitsNarratorRows()
    {
        var composer = CreateComposer(out var narrator);
        var rows = Enumerable.Range(1, 5)
            .Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["ModelCode"] = $"M{i}" })
            .ToArray();

        await composer.ComposeAsync(
            Request() with
            {
                Rows = rows,
                RowCount = rows.Length,
                AnswerPolicy = AnswerPolicy.Default with { MaxRowsForNarration = 2 }
            },
            CancellationToken.None);

        narrator.Requests.Should().ContainSingle().Which.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task AnswerPolicy_TableDisabled_NoTableBlock()
    {
        var composer = CreateComposer();

        var answer = await composer.ComposeAsync(
            ModelCapabilityRequest("model.overview.by-code") with
            {
                AnswerPolicy = AnswerPolicy.Default with
                {
                    Table = new TablePolicy { Enabled = false }
                }
            },
            CancellationToken.None);

        answer.Blocks.OfType<SummaryBlock>().Should().ContainSingle();
        answer.Blocks.OfType<TableBlock>().Should().BeEmpty();
    }

    [Fact]
    public async Task AnswerPolicy_TableMaxRows_Truncates()
    {
        var composer = CreateComposer();
        var rows = Enumerable.Range(1, 4)
            .Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["ModelCode"] = $"M{i}" })
            .ToArray();

        var answer = await composer.ComposeAsync(
            Request() with
            {
                Rows = rows,
                RowCount = rows.Length,
                AnswerPolicy = AnswerPolicy.Default with
                {
                    MaxRowsForChat = 20,
                    Table = new TablePolicy { MaxDisplayedRows = 2 }
                }
            },
            CancellationToken.None);

        var table = answer.Blocks.OfType<TableBlock>().Single();
        table.Rows.Should().HaveCount(2);
        table.DisplayedRows.Should().Be(2);
        table.Truncated.Should().BeTrue();
        answer.FollowUpQuestions.Should().ContainSingle();
    }

    [Fact]
    public async Task AnswerPolicy_NoData_IncludeFilters()
    {
        var composer = CreateComposer();

        var answer = await composer.ComposeAsync(
            Request() with
            {
                Rows = [],
                RowCount = 0,
                Arguments = new Dictionary<string, object?> { ["modelCode"] = "ABC" },
                AnswerPolicy = AnswerPolicy.Default with
                {
                    NoData = new NoDataPolicy { IncludeUsedFilters = false }
                }
            },
            CancellationToken.None);

        answer.Text.Should().Contain("No data was found");
        answer.Text.Should().NotContain("Filters used:");
        answer.Text.Should().NotContain("modelCode");
    }

    [Fact]
    public async Task AnswerPolicy_FollowUp_IncludeMissingFields()
    {
        var composer = CreateComposer();

        var answer = await composer.ComposeAsync(
            Request() with
            {
                ErrorCode = CapabilityExecutionFacade.ArgumentValidationFailedCode,
                MissingArguments = ["modelCode"],
                AnswerPolicy = AnswerPolicy.Default with
                {
                    FollowUp = new FollowUpPolicy { IncludeMissingFields = false }
                }
            },
            CancellationToken.None);

        var block = answer.Blocks.OfType<FollowUpBlock>().Single();
        block.Options.Should().BeEmpty();
        answer.Text.Should().Contain("modelCode");
    }

    [Fact]
    public void AgentAnswerNarrationService_InvalidJson_UsesFallback()
    {
        var parser = new AnswerNarrationResponseParser();
        var request = CreateNarrationRequest(ModelCapabilityRequest("model.materials.by-code"));

        var parsed = parser.TryParse("not json", request, 1200, out _);
        var fallback = new GenericSchemaSummaryFallback().Generate(request);

        parsed.Should().BeFalse();
        fallback.UsedFallback.Should().BeTrue();
        fallback.Text.Should().Contain("Found 8 rows");
    }

    [Fact]
    public void GenericSchemaSummaryFallback_IsCapabilityAgnostic()
    {
        var fallback = new GenericSchemaSummaryFallback();
        var request = CreateNarrationRequest(Request() with
        {
            CapabilityKey = "finance.example",
            RowCount = 2,
            Rows =
            [
                new Dictionary<string, object?> { ["Code"] = "A" },
                new Dictionary<string, object?> { ["Code"] = "B" }
            ],
            ResultSchema = new ResultSchema
            {
                Columns = [new ResultColumn { Name = "Code", Label = "Code", Type = "string" }]
            }
        }) with
        {
            CapabilityName = "Example result"
        };

        var result = fallback.Generate(request);

        result.Text.Should().Contain("Example result");
        result.Text.Should().Contain("Found 2 rows");
        result.Text.Should().NotContain("finance.example");
    }

    [Fact]
    public void AnswerNarrationPromptBuilder_UsesVietnameseInstructionsFromPolicy()
    {
        var request = CreateNarrationRequest(ModelCapabilityRequest("model.overview.by-code") with
        {
            Locale = "vi-VN",
            AnswerPolicy = AnswerPolicy.Default with
            {
                Summary = new SummaryPolicy
                {
                    InstructionsByLocale = new Dictionary<string, string>
                    {
                        ["vi-VN"] = "Chi dung chi dan tieng Viet tu catalog.",
                        ["en-US"] = "Use the English catalog instruction."
                    }
                }
            }
        });

        using var document = JsonDocument.Parse(new AnswerNarrationPromptBuilder().BuildUserMessage(request));

        document.RootElement
            .GetProperty("summaryPolicy")
            .GetProperty("catalogInstruction")
            .GetString()
            .Should().Be("Chi dung chi dan tieng Viet tu catalog.");
    }

    [Fact]
    public void AnswerNarrationPromptBuilder_UsesEnglishInstructionsFromPolicy()
    {
        var request = CreateNarrationRequest(ModelCapabilityRequest("model.overview.by-code") with
        {
            Locale = "en-US",
            AnswerPolicy = AnswerPolicy.Default with
            {
                Summary = new SummaryPolicy
                {
                    InstructionsByLocale = new Dictionary<string, string>
                    {
                        ["vi-VN"] = "Chi dung chi dan tieng Viet tu catalog.",
                        ["en-US"] = "Use the English catalog instruction."
                    }
                }
            }
        });

        using var document = JsonDocument.Parse(new AnswerNarrationPromptBuilder().BuildUserMessage(request));

        document.RootElement
            .GetProperty("summaryPolicy")
            .GetProperty("catalogInstruction")
            .GetString()
            .Should().Be("Use the English catalog instruction.");
    }

    private static StructuredAnswerComposer CreateComposer() =>
        CreateComposer(out _);

    private static StructuredAnswerComposer CreateComposer(out FakeAnswerNarrationService narrator)
    {
        narrator = new FakeAnswerNarrationService();
        return new StructuredAnswerComposer(new RawJsonAnswerComposer(), narrator);
    }

    private static StructuredAnswerComposer CreateComposer(string narratorText)
    {
        var narrator = new FakeAnswerNarrationService(narratorText);
        return new StructuredAnswerComposer(new RawJsonAnswerComposer(), narrator);
    }

    private static AnswerComposerRequest ModelCapabilityRequest(string capabilityKey)
    {
        var rowCount = capabilityKey switch
        {
            "model.count" => 1,
            "model.pieces.by-code" => 4,
            "model.materials.by-code" => 8,
            "model.packaging.by-code" => 2,
            "model.compare" => 2,
            _ => 1
        };

        var rows = capabilityKey switch
        {
            "model.count" =>
            [
                new Dictionary<string, object?> { ["ModelCount"] = 6 }
            ],
            "model.compare" =>
            [
                new Dictionary<string, object?> { ["ModelCode"] = "ABC", ["ComparedValue"] = 1 },
                new Dictionary<string, object?> { ["ModelCode"] = "XYZ", ["ComparedValue"] = 2 }
            ],
            _ => Enumerable.Range(1, rowCount)
                .Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
                {
                    ["ModelCode"] = "ABC",
                    ["LineNo"] = i
                })
                .ToArray()
        };

        return Request() with
        {
            CapabilityKey = capabilityKey,
            ProcedureName = $"dbo.ai_{capabilityKey.Replace('.', '_').Replace('-', '_')}",
            Arguments = capabilityKey == "model.compare"
                ? new Dictionary<string, object?> { ["modelCodeA"] = "ABC", ["modelCodeB"] = "XYZ" }
                : new Dictionary<string, object?> { ["modelCode"] = "ABC" },
            Rows = rows,
            RowCount = rowCount,
            ResultSchema = new ResultSchema
            {
                Columns =
                [
                    new ResultColumn { Name = "ModelCode", Label = "Model Code", LabelVi = "Mã model", Type = "string", Role = "dimension" },
                    new ResultColumn { Name = "LineNo", Label = "Line", LabelVi = "Dòng", Type = "integer", Role = "measure" },
                    new ResultColumn { Name = "ModelCount", Label = "Model Count", LabelVi = "Số model", Type = "integer", Role = "measure" },
                    new ResultColumn { Name = "ComparedValue", Label = "Compared Value", LabelVi = "Giá trị so sánh", Type = "integer", Role = "measure" }
                ]
            }
        };
    }

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

    private static AnswerNarrationRequest CreateNarrationRequest(AnswerComposerRequest request) => new()
    {
        Locale = request.Locale,
        CapabilityKey = request.CapabilityKey,
        Arguments = request.Arguments,
        ResultSchema = request.ResultSchema,
        Rows = request.Rows,
        RowCount = request.RowCount,
        AnswerPolicy = request.AnswerPolicy,
        SensitivityPolicy = request.SensitivityPolicy,
        ExecutionMetadata = request.ExecutionMetadata
    };

    private sealed class FakeAnswerNarrationService : IAnswerNarrationService
    {
        private readonly string _text;

        public FakeAnswerNarrationService(string text = "AI-generated summary from fake narrator.")
        {
            _text = text;
        }

        public List<AnswerNarrationRequest> Requests { get; } = [];

        public Task<AnswerNarrationResult> GenerateAsync(
            AnswerNarrationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new AnswerNarrationResult
            {
                Text = _text,
                Confidence = 0.9
            });
        }
    }

    private sealed class FallbackAnswerNarrationService : IAnswerNarrationService
    {
        private readonly GenericSchemaSummaryFallback _fallback = new();

        public Task<AnswerNarrationResult> GenerateAsync(
            AnswerNarrationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_fallback.Generate(request));
    }
}
