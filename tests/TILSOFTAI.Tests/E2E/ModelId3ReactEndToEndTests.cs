using System.Text.Json;
using TILSOFTAI.Orchestration.Policies;
using Xunit;

namespace TILSOFTAI.Tests.E2E;

/// <summary>
/// PATCH 36.08: ModelId=3 ReAct end-to-end simulation (unit level).
/// Validates the full cycle: overview → follow-up rules trigger → nudge generation.
/// This is an integration-style unit test — no external dependencies.
/// </summary>
public class ModelId3ReactEndToEndTests
{
    private readonly ReActFollowUpEvaluator _evaluator = new();

    /// <summary>
    /// Simulate: model_overview returns envelope → rules evaluate → pieces + packaging follow-ups fire.
    /// </summary>
    [Fact]
    public void ModelOverview_TriggersFollowUps_ForPiecesAndPackaging()
    {
        // 1. Define rules as if loaded from SQL
        var rules = new List<ReActFollowUpRule>
        {
            new(1, "model_overview_pieces", "model", "model_overview", 10,
                "$.PieceCount", ">", "0",
                "model_pieces_list", """{"ModelId":"{{$.ModelId}}"}""",
                "Now retrieve the pieces list for this model"),
            new(2, "model_overview_packaging", "model", "model_overview", 20,
                "$.PackagingMethodId", "exists", null,
                "model_packaging_method", """{"PackagingMethodId":"{{$.PackagingMethodId}}"}""",
                "Get packaging details"),
            new(3, "model_overview_materials", "model", "model_overview", 30,
                "$.MaterialCount", ">", "0",
                "model_materials_list", """{"ModelId":"{{$.ModelId}}"}""",
                "Get materials breakdown")
        };

        // 2. Simulate model_overview tool result (envelope format)
        var toolResult = """
        {
            "meta": {"total": 1, "status": "ok"},
            "columns": ["ModelId","ModelNm","PieceCount","PackagingMethodId","MaterialCount"],
            "rows": [
                {
                    "ModelId": 3,
                    "ModelNm": "Widget A",
                    "PieceCount": 12,
                    "PackagingMethodId": 42,
                    "MaterialCount": 5
                }
            ]
        }
        """;

        // 3. Evaluate: all 3 rules should fire
        var matched = _evaluator.Evaluate(rules, "model_overview", toolResult);

        Assert.Equal(3, matched.Count);
        Assert.Contains(matched, r => r.RuleKey == "model_overview_pieces");
        Assert.Contains(matched, r => r.RuleKey == "model_overview_packaging");
        Assert.Contains(matched, r => r.RuleKey == "model_overview_materials");
    }

    [Fact]
    public void ModelOverview_OnlyPiecesRule_WhenNoPackaging()
    {
        var rules = new List<ReActFollowUpRule>
        {
            new(1, "model_overview_pieces", "model", "model_overview", 10,
                "$.PieceCount", ">", "0",
                "model_pieces_list", """{"ModelId":"{{$.ModelId}}"}""",
                ""),
            new(2, "model_overview_packaging", "model", "model_overview", 20,
                "$.PackagingMethodId", "exists", null,
                "model_packaging_method", null, "")
        };

        // Envelope WITHOUT PackagingMethodId
        var toolResult = """
        {
            "rows": [{"ModelId": 3, "PieceCount": 8}]
        }
        """;

        var matched = _evaluator.Evaluate(rules, "model_overview", toolResult);

        Assert.Single(matched);
        Assert.Equal("model_overview_pieces", matched[0].RuleKey);
    }

    [Fact]
    public void ModelOverview_NoFollowUps_WhenZeroPieces()
    {
        var rules = new List<ReActFollowUpRule>
        {
            new(1, "model_overview_pieces", "model", "model_overview", 10,
                "$.PieceCount", ">", "0",
                "model_pieces_list", null, "")
        };

        var toolResult = """{"rows":[{"ModelId":3,"PieceCount":0}]}""";

        var matched = _evaluator.Evaluate(rules, "model_overview", toolResult);

        Assert.Empty(matched);
    }

    [Fact]
    public void ResolveArgsTemplate_ModelId_FromEnvelope()
    {
        var template = """{"ModelId":"{{$.ModelId}}","Pieces":"{{$.PieceCount}}"}""";
        var envelope = """{"rows":[{"ModelId":3,"PieceCount":12}]}""";

        var resolved = _evaluator.ResolveArgsTemplate(template, envelope);

        Assert.Equal("""{"ModelId":"3","Pieces":"12"}""", resolved);
    }

    [Fact]
    public void WrongTool_NoRulesFire()
    {
        var rules = new List<ReActFollowUpRule>
        {
            new(1, "model_overview_pieces", "model", "model_overview", 10,
                "$.PieceCount", ">", "0",
                "model_pieces_list", null, "")
        };

        var toolResult = """{"rows":[{"ModelId":3,"PieceCount":12}]}""";

        // Call with wrong tool name
        var matched = _evaluator.Evaluate(rules, "shipment_list", toolResult);

        Assert.Empty(matched);
    }

    [Fact]
    public void NudgeCap_SimulateDeduplication()
    {
        // Simulate the ChatPipeline's dedup logic
        var firedRuleIds = new HashSet<long>();
        var maxNudgesPerTurn = 2;
        var nudgesThisTurn = 0;

        var rules = new List<ReActFollowUpRule>
        {
            new(1, "r1", "model", "model_overview", 10, "$.X", "exists", null, "t1", null, ""),
            new(2, "r2", "model", "model_overview", 20, "$.Y", "exists", null, "t2", null, ""),
            new(3, "r3", "model", "model_overview", 30, "$.Z", "exists", null, "t3", null, "")
        };

        var toolResult = """{"rows":[{"X":1,"Y":2,"Z":3}]}""";
        var matched = _evaluator.Evaluate(rules, "model_overview", toolResult);

        Assert.Equal(3, matched.Count);

        // Simulate cap at 2
        foreach (var rule in matched)
        {
            if (firedRuleIds.Contains(rule.RuleId) || nudgesThisTurn >= maxNudgesPerTurn)
                continue;

            firedRuleIds.Add(rule.RuleId);
            nudgesThisTurn++;
        }

        Assert.Equal(2, nudgesThisTurn);
        Assert.Equal(2, firedRuleIds.Count);
    }
}
