using FluentAssertions;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Semantic;
using Xunit;

namespace TILSOFTAI.Tests.AiRouting;

public sealed class HardSignalExtractorTests
{
    [Fact]
    public void Extract_ShouldFindCodesDatesNumbersAndVietnameseLanguage()
    {
        var extractor = new HardSignalExtractor();

        var result = extractor.Extract(
            "Cho tôi xem nhập xuất MADEIRA-BLK tháng trước 500 pcs",
            "en-US",
            new TilsoftExecutionContext());

        result.Codes.Should().Contain(signal => signal.Text.Equals("MADEIRA-BLK", StringComparison.OrdinalIgnoreCase));
        result.Dates.Should().Contain(signal => signal.Kind == "relative_month");
        result.Numbers.Should().Contain(signal => signal.Value == 500 && signal.Unit == "pcs");
        result.BusinessKeywords.Should().Contain(keyword => keyword == "nhập xuất");
        result.DetectedLanguage.Should().Be("vi-VN");
    }
}
