using System.Text.Json;
using TILSOFTAI.Orchestration.Policies;
using Xunit;

namespace TILSOFTAI.Tests.ReAct;

/// <summary>
/// PATCH 36.08: Enterprise tests for ReActFollowUpEvaluator envelope support.
/// Verifies that evaluator correctly unwraps {rows:[{...}]} envelopes
/// and evaluates rules against the effective row data.
/// </summary>
public class ReActFollowUpEnvelopeTests
{
    private readonly ReActFollowUpEvaluator _evaluator = new();

    // ────────────────────────────────────────────
    // GetEffectiveRoot
    // ────────────────────────────────────────────

    [Fact]
    public void GetEffectiveRoot_PlainObject_ReturnsSelf()
    {
        var json = """{"ModelId":3,"ModelNm":"TestModel"}""";
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();

        var effective = ReActFollowUpEvaluator.GetEffectiveRoot(root);

        Assert.Equal(JsonValueKind.Object, effective.ValueKind);
        Assert.True(effective.TryGetProperty("ModelId", out _));
    }

    [Fact]
    public void GetEffectiveRoot_RowsEnvelope_ReturnsFirstRow()
    {
        var json = """
        {
            "meta": {"total":1},
            "columns": ["ModelId","ModelNm"],
            "rows": [{"ModelId":3,"ModelNm":"TestModel","PieceCount":12}]
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();

        var effective = ReActFollowUpEvaluator.GetEffectiveRoot(root);

        Assert.True(effective.TryGetProperty("PieceCount", out var val));
        Assert.Equal(12, val.GetInt32());
    }

    [Fact]
    public void GetEffectiveRoot_EmptyRowsArray_ReturnsSelf()
    {
        var json = """{"rows":[]}""";
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();

        var effective = ReActFollowUpEvaluator.GetEffectiveRoot(root);

        Assert.True(effective.TryGetProperty("rows", out _));
    }

    [Fact]
    public void GetEffectiveRoot_ArrayOfObjects_ReturnsFirst()
    {
        var json = """[{"ModelId":3},{"ModelId":4}]""";
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();

        var effective = ReActFollowUpEvaluator.GetEffectiveRoot(root);

        Assert.True(effective.TryGetProperty("ModelId", out var val));
        Assert.Equal(3, val.GetInt32());
    }

    // ────────────────────────────────────────────
    // Evaluate with envelopes
    // ────────────────────────────────────────────

    [Fact]
    public void Evaluate_EnvelopePayload_MatchesRowProperty()
    {
        var rules = new List<ReActFollowUpRule>
        {
            new(1, "model_pieces", "model", "model_overview", 10,
                "$.PieceCount", ">", "0",
                "model_pieces_list", """{"ModelId":"{{$.ModelId}}"}""",
                "Now get the pieces list")
        };

        var envelope = """
        {
            "meta":{"total":1},
            "rows":[{"ModelId":3,"ModelNm":"Widget","PieceCount":12}]
        }
        """;

        var matched = _evaluator.Evaluate(rules, "model_overview", envelope);

        Assert.Single(matched);
        Assert.Equal("model_pieces", matched[0].RuleKey);
    }

    [Fact]
    public void Evaluate_PlainPayload_StillWorks()
    {
        var rules = new List<ReActFollowUpRule>
        {
            new(1, "model_pieces", "model", "model_overview", 10,
                "$.PieceCount", ">", "0",
                "model_pieces_list", null, "Now get the pieces")
        };

        var plain = """{"ModelId":3,"PieceCount":5}""";

        var matched = _evaluator.Evaluate(rules, "model_overview", plain);

        Assert.Single(matched);
    }

    [Fact]
    public void Evaluate_EnvelopeExists_MatchesPackagingMethodId()
    {
        var rules = new List<ReActFollowUpRule>
        {
            new(2, "model_packaging", "model", "model_overview", 20,
                "$.PackagingMethodId", "exists", null,
                "model_packaging_details", """{"PackagingMethodId":"{{$.PackagingMethodId}}"}""",
                "Now get packaging details")
        };

        var envelope = """
        {
            "rows":[{"ModelId":3,"PackagingMethodId":42}]
        }
        """;

        var matched = _evaluator.Evaluate(rules, "model_overview", envelope);

        Assert.Single(matched);
        Assert.Equal("model_packaging", matched[0].RuleKey);
    }

    [Fact]
    public void Evaluate_EnvelopeMissingField_NoMatch()
    {
        var rules = new List<ReActFollowUpRule>
        {
            new(2, "model_packaging", "model", "model_overview", 20,
                "$.PackagingMethodId", "exists", null,
                "model_packaging_details", null, "")
        };

        var envelope = """
        {
            "rows":[{"ModelId":3,"PieceCount":12}]
        }
        """;

        var matched = _evaluator.Evaluate(rules, "model_overview", envelope);

        Assert.Empty(matched);
    }

    // ────────────────────────────────────────────
    // ResolveArgsTemplate with envelopes
    // ────────────────────────────────────────────

    [Fact]
    public void ResolveArgsTemplate_Envelope_ResolvesFromRow()
    {
        var template = """{"ModelId":"{{$.ModelId}}"}""";
        var envelope = """
        {
            "rows":[{"ModelId":3,"ModelNm":"Widget"}]
        }
        """;

        var resolved = _evaluator.ResolveArgsTemplate(template, envelope);

        Assert.Equal("""{"ModelId":"3"}""", resolved);
    }

    [Fact]
    public void ResolveArgsTemplate_Envelope_FallbackToTopLevel()
    {
        var template = """{"total":"{{$.total}}","ModelId":"{{$.ModelId}}"}""";
        var envelope = """
        {
            "total":1,
            "rows":[{"ModelId":3}]
        }
        """;

        var resolved = _evaluator.ResolveArgsTemplate(template, envelope);

        // total is on root, ModelId is on rows[0]
        Assert.Contains("\"total\":\"1\"", resolved);
        Assert.Contains("\"ModelId\":\"3\"", resolved);
    }

    // ────────────────────────────────────────────
    // Numeric InvariantCulture
    // ────────────────────────────────────────────

    [Fact]
    public void Evaluate_DecimalComparison_InvariantCulture()
    {
        var rules = new List<ReActFollowUpRule>
        {
            new(1, "weight_check", "model", "model_overview", 10,
                "$.Weight", ">", "1.5",
                "weight_details", null, "Heavy item")
        };

        var json = """{"Weight":2.7}""";

        var matched = _evaluator.Evaluate(rules, "model_overview", json);

        Assert.Single(matched);
    }
}
