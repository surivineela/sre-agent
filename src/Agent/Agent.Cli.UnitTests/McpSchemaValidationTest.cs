using Agent.Cli.Mcp.Tools;
using ModelContextProtocol.Server;
using Shouldly;
using Xunit;

namespace Agent.Cli.UnitTests;

/// <summary>
/// Tests for the refactored MCP Server tools.
/// Workflow: design_workflow -> get_documentation -> generate_and_validate_yaml -> apply_yaml_to_server
/// </summary>
public class McpSchemaValidationTest
{
    [Fact]
    public void SreAgentTools_ShouldHaveMcpServerToolTypeAttribute()
    {
        var toolType = typeof(SreAgentTools);
        var attribute = toolType.GetCustomAttributes(typeof(McpServerToolTypeAttribute), false);

        attribute.ShouldNotBeEmpty("SreAgentTools should have McpServerToolType attribute");
    }

    #region plan_agent_architecture Tests

    [Fact]
    public void PlanAgentArchitecture_ShouldHaveMcpServerToolAttribute()
    {
        var method = typeof(SreAgentTools).GetMethod("PlanAgentArchitecture");
        method.ShouldNotBeNull();

        var attribute = method.GetCustomAttributes(typeof(McpServerToolAttribute), false);
        attribute.ShouldNotBeEmpty("PlanAgentArchitecture should have McpServerTool attribute");
    }

    [Fact]
    public void PlanAgentArchitecture_ShouldReturnMermaidDiagram()
    {
        var result = SreAgentTools.PlanAgentArchitecture(
            requirements: "incident response agent that queries Kusto logs",
            triggerType: "icm");

        result.Success.ShouldBeTrue(result.Error);
        result.MermaidDiagram.ShouldNotBeNullOrWhiteSpace();
        result.MermaidDiagram!.ShouldContain("```mermaid");
        result.MermaidDiagram.ShouldContain("flowchart");
    }

    [Fact]
    public void PlanAgentArchitecture_ShouldReturnImplementationChecklist()
    {
        var result = SreAgentTools.PlanAgentArchitecture(
            requirements: "agent that analyzes Kusto telemetry",
            triggerType: "manual");

        result.Success.ShouldBeTrue();
        result.Checklist.ShouldNotBeNull();
        result.Checklist.ShouldNotBeEmpty();
    }

    #endregion

    #region get_documentation Tests

    [Fact]
    public void GetDocumentation_ShouldHaveMcpServerToolAttribute()
    {
        var method = typeof(SreAgentTools).GetMethod("GetDocumentation");
        method.ShouldNotBeNull();

        var attribute = method.GetCustomAttributes(typeof(McpServerToolAttribute), false);
        attribute.ShouldNotBeEmpty("GetDocumentation should have McpServerTool attribute");
    }

    [Fact]
    public void LearnTopic_ShouldHaveMcpServerToolAttribute()
    {
        var method = typeof(SreAgentTools).GetMethod("LearnTopic");
        method.ShouldNotBeNull();

        var attribute = method.GetCustomAttributes(typeof(McpServerToolAttribute), false);
        attribute.ShouldNotBeEmpty("LearnTopic should have McpServerTool attribute");
    }

    [Fact]
    public void ListStableToolTypes_ShouldHaveMcpServerToolAttribute()
    {
        var method = typeof(SreAgentTools).GetMethod("ListStableToolTypes");
        method.ShouldNotBeNull();

        var attribute = method.GetCustomAttributes(typeof(McpServerToolAttribute), false);
        attribute.ShouldNotBeEmpty("ListStableToolTypes should have McpServerTool attribute");
    }

    [Fact]
    public void GetDocumentation_ShouldReturnContentForValidTopics()
    {
        var validTopics = new[] { "overview", "agents", "tools", "triggers", "quickstart" };

        foreach (var topic in validTopics)
        {
            var result = SreAgentTools.GetDocumentation(topic);
            result.ShouldNotBeNullOrWhiteSpace($"GetDocumentation('{topic}') should return content");
        }
    }

    [Fact]
    public void GetDocumentation_ShouldReturnErrorForInvalidTopic()
    {
        var result = SreAgentTools.GetDocumentation("invalid-topic-xyz");
        result.ShouldContain("Unknown topic");
    }

    #endregion

    #region generate_and_validate_yaml Tests

    [Fact]
    public void GenerateAndValidateYaml_ShouldHaveMcpServerToolAttribute()
    {
        var method = typeof(SreAgentTools).GetMethod("GenerateAndValidateYaml");
        method.ShouldNotBeNull();

        var attribute = method.GetCustomAttributes(typeof(McpServerToolAttribute), false);
        attribute.ShouldNotBeEmpty("GenerateAndValidateYaml should have McpServerTool attribute");
    }

    [Fact]
    public void GenerateAndValidateYaml_ShouldGenerateAgentYaml()
    {
        var result = SreAgentTools.GenerateAndValidateYaml(
            kind: "agent",
            name: "my-test-agent",
            description: "A test agent",
            tools: new[] { "Kusto", "ICM" });

        result.Success.ShouldBeTrue(result.Error);
        result.Yaml.ShouldNotBeNullOrWhiteSpace();
        result.Yaml!.ShouldContain("api_version: azuresre.ai/v2");
        result.Yaml.ShouldContain("kind: ExtendedAgent");
        result.Yaml.ShouldContain("name: my-test-agent");
        result.Yaml.ShouldContain("handoffDescription:");
        result.Yaml.ShouldContain("handoffs: []");
        result.Yaml.ShouldContain("- Kusto");
    }

    [Fact]
    public void GenerateAndValidateYaml_ShouldGenerateKustoToolYaml()
    {
        var result = SreAgentTools.GenerateAndValidateYaml(
            kind: "tool",
            name: "GetErrorLogs",
            description: "Query error logs",
            modelOrType: "KustoTool",
            connector: "my-connector",
            database: "Logs",
            query: "Exceptions | take 100");

        result.Success.ShouldBeTrue(result.Error);
        result.Yaml.ShouldNotBeNullOrWhiteSpace();
        result.Yaml!.ShouldContain("api_version: azuresre.ai/v2");
        result.Yaml.ShouldContain("kind: ExtendedAgentTool");
        result.Yaml.ShouldContain("type: KustoTool");
        result.Yaml.ShouldContain("connector: my-connector");
    }

    [Fact]
    public void GenerateAndValidateYaml_ShouldBlockKustoToolWithoutConnector()
    {
        var result = SreAgentTools.GenerateAndValidateYaml(
            kind: "tool",
            name: "GetErrorLogs",
            description: "Query error logs",
            modelOrType: "KustoTool",
            database: "Logs");

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("connector");
    }

    [Fact]
    public void GenerateAndValidateYaml_ShouldBlockLinkToolWithoutTemplate()
    {
        var result = SreAgentTools.GenerateAndValidateYaml(
            kind: "tool",
            name: "DashboardLink",
            description: "Link to dashboard",
            modelOrType: "LinkTool");

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("urlTemplate");
    }

    [Fact]
    public void GenerateAndValidateYaml_ShouldRejectUnsupportedToolType()
    {
        var result = SreAgentTools.GenerateAndValidateYaml(
            kind: "tool",
            name: "MyTool",
            description: "Test tool",
            modelOrType: "PythonFunctionTool");

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("Unsupported");
    }

    #endregion

    #region validate_workflow Tests

    [Fact]
    public void ValidateWorkflow_ShouldNotRequireModel()
    {
        var yaml = """
            api_version: azuresre.ai/v2
            kind: ExtendedAgent
            metadata:
              name: no-model-agent
            spec:
              instructions: |
                You are an agent.
            """;

        var result = SreAgentTools.ValidateWorkflow(yaml);

        result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors));
        result.Errors.ShouldBeEmpty();
        result.Warnings.ShouldContain(w => w.Contains("model", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateWorkflow_ShouldWarnAboutConnectors()
    {
        var yaml = string.Join("\n", new[]
        {
            "api_version: azuresre.ai/v2",
            "metadata:",
            "  name: kusto-agent",
            "spec:",
            "  model: gpt-4o",
            "  instructions: |",
            "    You are an agent.",
            "  tools:",
            "    - type: KustoTool",
            "      connector: my-kusto-connector"
        });

        var result = SreAgentTools.ValidateWorkflow(yaml);

        result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors));
        // Should warn about connector access verification
        result.Warnings.ShouldContain(w => w.Contains("my-kusto-connector") && w.Contains("show-connectors"));
    }

    #endregion
}
