using TILSOFTAI.Orchestration.Policies;
using Xunit;

namespace TILSOFTAI.Tests.Policies;

public class ReActFollowUpEvaluatorTests
{
    private readonly ReActFollowUpEvaluator _evaluator = new();

    private static ReActFollowUpRule MakeRule(
        string ruleKey = "test",
        string? toolName = null,
        string jsonPath = "$.Value",
        string op = ">",
        string? compareValue = "0",
        string followUpTool = "test_followup",
        string promptHint = "Test nudge")
    {
        return new ReActFollowUpRule(
            RuleId: 1,
            RuleKey: ruleKey,
            ModuleKey: "test",
            ToolName: toolName,
            Priority: 10,
            JsonPath: jsonPath,
            Operator: op,
            CompareValue: compareValue,
            FollowUpToolName: followUpTool,
            ArgsTemplateJson: null,
            PromptHint: promptHint);
    }

    // ---- Operator tests ----

    [Fact]
    public void Evaluate_GreaterThan_Matches_When_True()
    {
        var rules = new[] { MakeRule(jsonPath: "$.Count", op: ">", compareValue: "5") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"Count": 10}""");
        Assert.Single(result);
    }

    [Fact]
    public void Evaluate_GreaterThan_NoMatch_When_False()
    {
        var rules = new[] { MakeRule(jsonPath: "$.Count", op: ">", compareValue: "10") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"Count": 5}""");
        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_Equals_Matches()
    {
        var rules = new[] { MakeRule(jsonPath: "$.Status", op: "==", compareValue: "active") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"Status": "active"}""");
        Assert.Single(result);
    }

    [Fact]
    public void Evaluate_NotEquals_Matches()
    {
        var rules = new[] { MakeRule(jsonPath: "$.Status", op: "!=", compareValue: "inactive") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"Status": "active"}""");
        Assert.Single(result);
    }

    [Fact]
    public void Evaluate_Exists_Matches_When_Present()
    {
        var rules = new[] { MakeRule(jsonPath: "$.PackagingId", op: "exists") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"PackagingId": 42}""");
        Assert.Single(result);
    }

    [Fact]
    public void Evaluate_Exists_NoMatch_When_Missing()
    {
        var rules = new[] { MakeRule(jsonPath: "$.MissingField", op: "exists") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"OtherField": 42}""");
        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_Contains_Matches()
    {
        var rules = new[] { MakeRule(jsonPath: "$.Name", op: "contains", compareValue: "cotton") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"Name": "100% Cotton Shirt"}""");
        Assert.Single(result);
    }

    [Fact]
    public void Evaluate_LessThanOrEqual_Matches()
    {
        var rules = new[] { MakeRule(jsonPath: "$.Price", op: "<=", compareValue: "100") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"Price": 99.5}""");
        Assert.Single(result);
    }

    [Fact]
    public void Evaluate_Boolean_True_Matches()
    {
        var rules = new[] { MakeRule(jsonPath: "$.HasMaterials", op: "==", compareValue: "true") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"HasMaterials": true}""");
        Assert.Single(result);
    }

    // ---- Tool filtering ----

    [Fact]
    public void Evaluate_Skips_Rules_With_Different_ToolName()
    {
        var rules = new[] { MakeRule(toolName: "tool_b", jsonPath: "$.Count", op: ">", compareValue: "0") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"Count": 10}""");
        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_Matches_Rules_Without_ToolName_Filter()
    {
        var rules = new[] { MakeRule(toolName: null, jsonPath: "$.Count", op: ">", compareValue: "0") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"Count": 10}""");
        Assert.Single(result);
    }

    // ---- Edge cases ----

    [Fact]
    public void Evaluate_Returns_Empty_For_Invalid_Json()
    {
        var rules = new[] { MakeRule() };
        var result = _evaluator.Evaluate(rules, "tool_a", "not json");
        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_Returns_Empty_For_Empty_Rules()
    {
        var result = _evaluator.Evaluate(Array.Empty<ReActFollowUpRule>(), "tool_a", """{"Count": 10}""");
        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_Handles_Array_Wrapper()
    {
        var rules = new[] { MakeRule(jsonPath: "$.Count", op: ">", compareValue: "0") };
        var result = _evaluator.Evaluate(rules, "tool_a", """[{"Count": 10}]""");
        Assert.Single(result);
    }

    [Fact]
    public void Evaluate_Case_Insensitive_Property()
    {
        var rules = new[] { MakeRule(jsonPath: "$.count", op: ">", compareValue: "0") };
        var result = _evaluator.Evaluate(rules, "tool_a", """{"Count": 10}""");
        Assert.Single(result);
    }

    // ---- Template resolution ----

    [Fact]
    public void ResolveArgsTemplate_Replaces_Placeholders()
    {
        var template = """{"modelId":"{{$.ModelId}}","size":"{{$.Size}}"}""";
        var json = """{"ModelId": "M-123", "Size": "large"}""";

        var resolved = _evaluator.ResolveArgsTemplate(template, json);

        Assert.Equal("""{"modelId":"M-123","size":"large"}""", resolved);
    }

    [Fact]
    public void ResolveArgsTemplate_Keeps_Missing_Placeholders()
    {
        var template = """{"modelId":"{{$.MissingField}}"}""";
        var json = """{"Other": "value"}""";

        var resolved = _evaluator.ResolveArgsTemplate(template, json);

        Assert.Equal("""{"modelId":"{{$.MissingField}}"}""", resolved);
    }

    [Fact]
    public void ResolveArgsTemplate_Returns_Null_For_Empty_Template()
    {
        Assert.Null(_evaluator.ResolveArgsTemplate(null, """{}"""));
        Assert.Null(_evaluator.ResolveArgsTemplate("", """{}"""));
    }
}
