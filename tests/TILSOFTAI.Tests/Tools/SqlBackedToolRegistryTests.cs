using TILSOFTAI.Orchestration.Tools;
using Xunit;

namespace TILSOFTAI.Tests.Tools;

/// <summary>
/// PATCH 37.05: SQL-backed tool registry contract tests.
/// </summary>
public sealed class SqlBackedToolRegistryTests
{
    [Fact]
    public void Register_SqlBacked_SkipsInstructionValidation()
    {
        // Arrange
        var registry = new ToolRegistry();
        var def = new ToolDefinition
        {
            Name = "test_sql_tool",
            SpName = "ai_test_sql_tool",
            Module = "TestModule",
            IsEnabled = true,
            IsSqlBacked = true
            // No Instruction or JsonSchema — should NOT throw
        };

        // Act — should not throw
        registry.Register(def);

        // Assert
        var retrieved = registry.Get("test_sql_tool");
        Assert.True(retrieved.IsSqlBacked);
        Assert.Equal(string.Empty, retrieved.Instruction);
        Assert.Equal(string.Empty, retrieved.JsonSchema);
    }

    [Fact]
    public void Register_NonSqlBacked_RequiresInstruction()
    {
        var registry = new ToolRegistry();
        var def = new ToolDefinition
        {
            Name = "test_regular_tool",
            SpName = "ai_test_regular_tool",
            Module = "TestModule",
            IsEnabled = true,
            IsSqlBacked = false,
            JsonSchema = "{}",
            // Missing Instruction
        };

        Assert.Throws<ArgumentException>(() => registry.Register(def));
    }

    [Fact]
    public void Register_NonSqlBacked_RequiresJsonSchema()
    {
        var registry = new ToolRegistry();
        var def = new ToolDefinition
        {
            Name = "test_regular_tool2",
            SpName = "ai_test_regular_tool2",
            Module = "TestModule",
            IsEnabled = true,
            IsSqlBacked = false,
            Instruction = "Do something",
            // Missing JsonSchema
        };

        Assert.Throws<ArgumentException>(() => registry.Register(def));
    }

    [Fact]
    public void Register_SqlBacked_StillRequiresName()
    {
        var registry = new ToolRegistry();
        var def = new ToolDefinition
        {
            Name = "", // Missing name
            SpName = "ai_test",
            Module = "TestModule",
            IsSqlBacked = true
        };

        Assert.Throws<ArgumentException>(() => registry.Register(def));
    }

    [Fact]
    public void Register_SqlBacked_StillRequiresAiPrefix()
    {
        var registry = new ToolRegistry();
        var def = new ToolDefinition
        {
            Name = "test_bad_sp",
            SpName = "bad_prefix_sp",
            Module = "TestModule",
            IsSqlBacked = true
        };

        Assert.Throws<ArgumentException>(() => registry.Register(def));
    }

    [Fact]
    public void ListEnabled_IncludesSqlBackedTools()
    {
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition
        {
            Name = "sql_tool_1",
            SpName = "ai_sql_tool_1",
            Module = "Analytics",
            IsEnabled = true,
            IsSqlBacked = true
        });
        registry.Register(new ToolDefinition
        {
            Name = "regular_tool_1",
            SpName = "ai_regular_tool_1",
            Module = "Model",
            IsEnabled = true,
            Instruction = "Do X",
            JsonSchema = "{}"
        });

        var enabled = registry.ListEnabled();
        Assert.Equal(2, enabled.Count);
        Assert.Contains(enabled, t => t.Name == "sql_tool_1" && t.IsSqlBacked);
        Assert.Contains(enabled, t => t.Name == "regular_tool_1" && !t.IsSqlBacked);
    }

    [Fact]
    public void ToolDefinition_IsSqlBacked_DefaultsFalse()
    {
        var def = new ToolDefinition();
        Assert.False(def.IsSqlBacked);
    }
}
