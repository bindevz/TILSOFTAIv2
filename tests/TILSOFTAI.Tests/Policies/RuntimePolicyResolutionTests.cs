using System.Text.Json;
using TILSOFTAI.Orchestration.Policies;
using Xunit;

namespace TILSOFTAI.Tests.Policies;

public class RuntimePolicyResolutionTests
{
    [Fact]
    public void Empty_Snapshot_Returns_Fallback_Values()
    {
        var snapshot = RuntimePolicySnapshot.Empty;

        Assert.False(snapshot.IsEnabled("react_nudge"));
        Assert.Equal(42, snapshot.GetValueOrDefault("missing", "key", 42));
        Assert.False(snapshot.TryGetPolicy("missing", out _));
    }

    [Fact]
    public void Snapshot_With_Policy_Returns_Correct_Values()
    {
        var json = JsonDocument.Parse("""{"enabled":true,"maxNudgesPerTurn":3}""");
        var policies = new Dictionary<string, JsonElement>
        {
            ["react_nudge"] = json.RootElement.Clone()
        };
        json.Dispose();

        var snapshot = new RuntimePolicySnapshot(policies);

        Assert.True(snapshot.IsEnabled("react_nudge"));
        Assert.Equal(3, snapshot.GetValueOrDefault("react_nudge", "maxNudgesPerTurn", 2));
        Assert.True(snapshot.TryGetPolicy("react_nudge", out var element));
        Assert.Equal(JsonValueKind.Object, element.ValueKind);
    }

    [Fact]
    public void Snapshot_Is_Case_Insensitive()
    {
        var json = JsonDocument.Parse("""{"enabled":true}""");
        var policies = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["REACT_NUDGE"] = json.RootElement.Clone()
        };
        json.Dispose();

        var snapshot = new RuntimePolicySnapshot(policies);

        Assert.True(snapshot.TryGetPolicy("react_nudge", out _));
    }

    [Fact]
    public void GetValueOrDefault_Returns_Fallback_For_Invalid_Json()
    {
        var json = JsonDocument.Parse("""{"enabled":"not_a_bool"}""");
        var policies = new Dictionary<string, JsonElement>
        {
            ["test"] = json.RootElement.Clone()
        };
        json.Dispose();

        var snapshot = new RuntimePolicySnapshot(policies);

        // "not_a_bool" cannot be deserialized as bool, should return fallback
        Assert.False(snapshot.GetValueOrDefault("test", "enabled", false));
    }

    [Fact]
    public void GetValueOrDefault_Returns_Fallback_For_Missing_Property()
    {
        var json = JsonDocument.Parse("""{"foo":"bar"}""");
        var policies = new Dictionary<string, JsonElement>
        {
            ["test"] = json.RootElement.Clone()
        };
        json.Dispose();

        var snapshot = new RuntimePolicySnapshot(policies);

        Assert.Equal(99, snapshot.GetValueOrDefault("test", "nonexistent", 99));
    }
}
