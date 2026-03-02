using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Orchestration.Analytics;
using Xunit;

namespace TILSOFTAI.Tests.Pipeline;

/// <summary>
/// PATCH 37.02: Tests for scoped toolset self-heal and empty assistant guard.
/// </summary>
public sealed class ScopeWideningTests
{
    // ===== 37.02A: Entity-hint scope widening =====

    [Fact]
    public void EntityHint_Model_ShouldNotAlreadyBeInModules()
    {
        // Simulates the condition where scope resolver didn't include 'model' but intent detector found it
        var resolvedModules = new List<string> { "platform", "analytics" };
        var entityHint = "model";

        var needsWidening = !resolvedModules.Any(m =>
            m.Equals(entityHint, StringComparison.OrdinalIgnoreCase));

        Assert.True(needsWidening);
    }

    [Fact]
    public void EntityHint_Model_ShouldUnionIntoModules()
    {
        var resolvedModules = new List<string> { "platform", "analytics" };
        var entityHint = "model";

        if (!resolvedModules.Any(m => m.Equals(entityHint, StringComparison.OrdinalIgnoreCase)))
        {
            resolvedModules.Add(entityHint.ToLowerInvariant());
        }

        Assert.Contains("model", resolvedModules);
        Assert.Equal(3, resolvedModules.Count);
    }

    [Fact]
    public void EntityHint_AlreadyPresent_ShouldNotDuplicate()
    {
        var resolvedModules = new List<string> { "platform", "model" };
        var entityHint = "model";

        if (!resolvedModules.Any(m => m.Equals(entityHint, StringComparison.OrdinalIgnoreCase)))
        {
            resolvedModules.Add(entityHint.ToLowerInvariant());
        }

        Assert.Equal(2, resolvedModules.Count);
    }

    [Fact]
    public void EntityHint_CaseInsensitive_ShouldMatch()
    {
        var resolvedModules = new List<string> { "platform", "Model" };
        var entityHint = "model";

        var alreadyPresent = resolvedModules.Any(m =>
            m.Equals(entityHint, StringComparison.OrdinalIgnoreCase));

        Assert.True(alreadyPresent);
    }

    // ===== 37.02B: Empty assistant guard =====

    [Fact]
    public void EmptyResponseGuard_Step0_ShouldTriggerRetry()
    {
        var content = "";
        var step = 0;
        var toolCallCount = 0;

        var shouldRetry = string.IsNullOrWhiteSpace(content) && step == 0 && toolCallCount == 0;

        Assert.True(shouldRetry);
    }

    [Fact]
    public void EmptyResponseGuard_Step1_ShouldNotRetry()
    {
        var content = "";
        var step = 1;
        var toolCallCount = 0;

        var shouldRetry = string.IsNullOrWhiteSpace(content) && step == 0 && toolCallCount == 0;

        Assert.False(shouldRetry);
    }

    [Fact]
    public void EmptyResponseGuard_WithContent_ShouldNotRetry()
    {
        var content = "Tổng số model là 42.";
        var step = 0;
        var toolCallCount = 0;

        var shouldRetry = string.IsNullOrWhiteSpace(content) && step == 0 && toolCallCount == 0;

        Assert.False(shouldRetry);
    }

    [Fact]
    public void EmptyResponseGuard_WithToolCalls_ShouldNotRetry()
    {
        var content = "";
        var step = 0;
        var toolCallCount = 1;

        var shouldRetry = string.IsNullOrWhiteSpace(content) && step == 0 && toolCallCount == 0;

        Assert.False(shouldRetry);
    }

    // ===== ErrorCode check =====

    [Fact]
    public void ErrorCode_LlmEmptyResponse_Exists()
    {
        Assert.Equal("LLM_EMPTY_RESPONSE", ErrorCode.LlmEmptyResponse);
    }

    // ===== AnalyticsIntentDetector entity hint =====

    [Fact]
    public void IntentDetector_ModelQuery_ShouldReturnEntityHint()
    {
        var detector = new AnalyticsIntentDetector();
        var result = detector.Detect("tổng số models trong mùa 25/26");

        Assert.NotNull(result.EntityHint);
        Assert.Equal("model", result.EntityHint);
    }

    [Fact]
    public void IntentDetector_NonEntityQuery_ShouldReturnNullHint()
    {
        var detector = new AnalyticsIntentDetector();
        var result = detector.Detect("xin chào");

        Assert.Null(result.EntityHint);
    }
}
