using Agent.Cli.Mcp.Tools;
using Shouldly;
using Xunit;

namespace Agent.Cli.UnitTests;

/// <summary>
/// Tests for the refactored MCP agent/tool generation.
/// Uses the new generate_and_validate_yaml tool which replaces draft_subagent_yaml.
/// </summary>
public class McpSubagentBuilderToolsTests
{
    [Fact]
    public void GenerateAndValidateYaml_ShouldProduceAgentYaml()
    {
        // Use real platform tool names from PublishedTools.json
        var result = SreAgentTools.GenerateAndValidateYaml(
            kind: "agent",
            name: "Kusto Analyst",
            description: "Specialist for KQL queries and telemetry analysis",
            modelOrType: "gpt-4o",
            tools: ["RunAzCliReadCommands", "GetActivityLogsSummary"]);

        result.Success.ShouldBeTrue(result.Error ?? "expected success");
        result.Yaml.ShouldNotBeNullOrWhiteSpace();
        result.Yaml!.ShouldContain("api_version: azuresre.ai/v2");
        result.Yaml!.ShouldContain("kind: ExtendedAgent");
        result.Yaml!.ShouldContain("name: kusto-analyst");
        result.Yaml!.ShouldContain("instructions:");
        result.Yaml!.ShouldContain("tools:");
        result.Yaml!.ShouldContain("- RunAzCliReadCommands");

        // Should NOT contain invalid fields
        result.Yaml!.ShouldNotContain("connectors:");
        result.Yaml!.ShouldNotContain("toolsFile:");
    }

    [Fact]
    public void GenerateAndValidateYaml_ShouldIncludeToolsForKustoAgent()
    {
        // When Kusto tools are included, agent should list them properly
        var result = SreAgentTools.GenerateAndValidateYaml(
            kind: "agent",
            name: "kusto-agent",
            description: "Agent with read tools",
            tools: ["RunAzCliReadCommands", "GetArmResourceAsJson"]);

        result.Success.ShouldBeTrue();
        result.Yaml!.ShouldContain("tools:");
        result.Yaml!.ShouldContain("- RunAzCliReadCommands");
        result.Yaml!.ShouldContain("- GetArmResourceAsJson");
    }

    [Fact]
    public void GenerateAndValidateYaml_ShouldWarnAboutCustomTools()
    {
        var result = SreAgentTools.GenerateAndValidateYaml(
            kind: "agent",
            name: "custom-tools-agent",
            description: "Agent with custom tools",
            tools: ["GetIncidentInfo", "MyCustomTool", "AnotherCustom"]);

        result.Success.ShouldBeTrue();
        // Warnings should mention custom tools need ExtendedTool YAML
        result.Warnings.ShouldContain(w => w.Contains("MyCustomTool"));
    }

    [Fact]
    public void GenerateAndValidateYaml_ShouldGenerateHandoffs()
    {
        var result = SreAgentTools.GenerateAndValidateYaml(
            kind: "agent",
            name: "orchestrator",
            description: "Orchestrator agent",
            handoffs: ["kusto-analyst", "incident-manager"]);

        result.Success.ShouldBeTrue();
        result.Yaml!.ShouldContain("handoffs:");
        // Handoffs are now just agent names
        result.Yaml!.ShouldContain("kusto-analyst");
        result.Yaml!.ShouldContain("incident-manager");
    }

    [Fact]
    public void GenerateAndValidateYaml_ToolYaml_ShouldGenerateKustoTool()
    {
        var result = SreAgentTools.GenerateAndValidateYaml(
            kind: "tool",
            name: "GetMetrics",
            description: "Query metrics from Kusto",
            modelOrType: "KustoTool",
            connector: "metrics-connector",
            database: "Metrics",
            query: "Perf | summarize avg(Value) by Computer");

        result.Success.ShouldBeTrue();
        // Just check it contains basic KustoTool elements
        result.Yaml!.ShouldContain("KustoTool");
        result.Yaml!.ShouldContain("metrics-connector");
        result.Yaml!.ShouldContain("Metrics");
    }

    [Fact]
    public void GenerateAndValidateYaml_ToolYaml_ShouldFailWithoutRequiredFields()
    {
        var result = SreAgentTools.GenerateAndValidateYaml(
            kind: "tool",
            name: "IncompleteKusto",
            description: "Missing required fields",
            modelOrType: "KustoTool");

        // KustoTool requires connector and database - should fail without them
        result.Success.ShouldBeFalse();
        result.Error!.ShouldContain("connector");
    }
}
