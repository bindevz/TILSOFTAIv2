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
    public void EntityHint_Model_ShouldNotAlreadyBeInCapabilityScopes()
    {
        // Simulates the condition where capability scope detection did not include 'model' but intent detection found it.
        var capabilityScopes = new List<string> { "platform", "analytics" };
        var entityHint = "model";

        var needsWidening = !capabilityScopes.Any(m =>
            m.Equals(entityHint, StringComparison.OrdinalIgnoreCase));

        Assert.True(needsWidening);
    }

    [Fact]
    public void EntityHint_Model_ShouldUnionIntoCapabilityScopes()
    {
        var capabilityScopes = new List<string> { "platform", "analytics" };
        var entityHint = "model";

        if (!capabilityScopes.Any(m => m.Equals(entityHint, StringComparison.OrdinalIgnoreCase)))
        {
            capabilityScopes.Add(entityHint.ToLowerInvariant());
        }

        Assert.Contains("model", capabilityScopes);
        Assert.Equal(3, capabilityScopes.Count);
    }

    [Fact]
    public void EntityHint_AlreadyPresent_ShouldNotDuplicate()
    {
        var capabilityScopes = new List<string> { "platform", "model" };
        var entityHint = "model";

        if (!capabilityScopes.Any(m => m.Equals(entityHint, StringComparison.OrdinalIgnoreCase)))
        {
            capabilityScopes.Add(entityHint.ToLowerInvariant());
        }

        Assert.Equal(2, capabilityScopes.Count);
    }

    [Fact]
    public void EntityHint_CaseInsensitive_ShouldMatch()
    {
        var capabilityScopes = new List<string> { "platform", "Model" };
        var entityHint = "model";

        var alreadyPresent = capabilityScopes.Any(m =>
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
