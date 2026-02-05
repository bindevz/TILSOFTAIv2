using FluentAssertions;
using TILSOFTAI.Orchestration.Analytics;
using Xunit;

namespace TILSOFTAI.Tests.Analytics;

/// <summary>
/// Unit tests for AnalyticsIntentDetector.
/// PATCH 29.09: Tests for vi/en triggers and false-positive prevention.
/// </summary>
public class AnalyticsIntentDetectorTests
{
    private readonly AnalyticsIntentDetector _detector = new();

    #region Vietnamese Triggers

    [Theory]
    [InlineData("bao nhiêu model mùa 25/26", true, "vi")]
    [InlineData("tổng cộng đơn hàng tháng này", true, "vi")]
    [InlineData("có bao nhiêu mẫu trong bộ sưu tập", true, "vi")]
    [InlineData("thống kê sản phẩm theo khách hàng", true, "vi")]
    [InlineData("top 10 model bán chạy", true, "vi")]
    public void Detect_VietnameseTriggers_WithEntity_ShouldReturnAnalytics(
        string input, bool expectedIsAnalytics, string expectedLanguage)
    {
        // Act
        var result = _detector.Detect(input);

        // Assert
        result.IsAnalytics.Should().Be(expectedIsAnalytics);
        result.DetectedLanguage.Should().Be(expectedLanguage);
        result.Confidence.Should().BeGreaterOrEqualTo(0.5m);
    }

    [Theory]
    [InlineData("cao nhất mùa 25/26")]
    [InlineData("nhiều nhất theo model")]
    [InlineData("thấp nhất đơn hàng")]
    public void Detect_VietnameseComparativeTriggers_ShouldReturnAnalytics(string input)
    {
        var result = _detector.Detect(input);
        result.IsAnalytics.Should().BeTrue();
    }

    #endregion

    #region English Triggers

    [Theory]
    [InlineData("how many models for season 25/26", true, "en")]
    [InlineData("total orders this month", true, "en")]
    [InlineData("count products by customer", true, "en")]
    [InlineData("breakdown of orders by supplier", true, "en")]
    [InlineData("top 10 selling styles", true, "en")]
    public void Detect_EnglishTriggers_WithEntity_ShouldReturnAnalytics(
        string input, bool expectedIsAnalytics, string expectedLanguage)
    {
        var result = _detector.Detect(input);

        result.IsAnalytics.Should().Be(expectedIsAnalytics);
        result.DetectedLanguage.Should().Be(expectedLanguage);
        result.Confidence.Should().BeGreaterOrEqualTo(0.5m);
    }

    [Theory]
    [InlineData("average order value")]
    [InlineData("sum of quantities")]
    [InlineData("highest selling product")]
    public void Detect_EnglishAggregationTriggers_ShouldReturnAnalytics(string input)
    {
        var result = _detector.Detect(input);
        result.IsAnalytics.Should().BeTrue();
    }

    #endregion

    #region False Positive Prevention

    [Theory]
    [InlineData("hello")]
    [InlineData("what is the weather")]
    [InlineData("explain machine learning")]
    [InlineData("write me a poem")]
    [InlineData("how are you doing today")]
    public void Detect_NonAnalyticsQueries_ShouldReturnFalse(string input)
    {
        var result = _detector.Detect(input);

        result.IsAnalytics.Should().BeFalse();
        result.Confidence.Should().BeLessThan(0.5m);
    }

    [Theory]
    [InlineData("how many")] // Trigger without context
    [InlineData("total")] // Trigger without entity
    [InlineData("count")] // Trigger without context
    public void Detect_TriggerWithoutContext_ShouldHaveLowConfidence(string input)
    {
        var result = _detector.Detect(input);

        // Should not meet threshold without entity/date context
        result.Confidence.Should().BeLessThan(0.5m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Detect_EmptyInput_ShouldReturnFalse(string? input)
    {
        var result = _detector.Detect(input!);

        result.IsAnalytics.Should().BeFalse();
        result.Confidence.Should().Be(0);
    }

    #endregion

    #region Season and Date Detection

    [Theory]
    [InlineData("orders for 25/26", "season:25/26")]
    [InlineData("count models mùa 25/26", "season:mùa 25/26")]
    [InlineData("total for season 2025", "season:2025")]
    public void Detect_WithSeasonPattern_ShouldIncludeSeasonHint(string input, string expectedSeasonHint)
    {
        var result = _detector.Detect(input);

        result.Hints.Should().Contain(h => h.StartsWith("season:"));
    }

    [Theory]
    [InlineData("orders from 01/01/2025", "date:")]
    [InlineData("count after 2025-01-01", "date:")]
    public void Detect_WithDatePattern_ShouldIncludeDateHint(string input, string expectedPrefix)
    {
        var result = _detector.Detect(input);

        result.Hints.Should().Contain(h => h.StartsWith(expectedPrefix));
    }

    #endregion

    #region Entity Detection

    [Theory]
    [InlineData("how many models", "entity:model")]
    [InlineData("count đơn hàng", "entity:đơn hàng")]
    [InlineData("total products", "entity:product")]
    [InlineData("breakdown by customer", "entity:customer")]
    public void Detect_WithEntityHint_ShouldIncludeEntityInHints(string input, string expectedEntity)
    {
        var result = _detector.Detect(input);

        result.Hints.Should().Contain(h => h.StartsWith("entity:"));
    }

    #endregion
}
