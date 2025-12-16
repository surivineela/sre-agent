// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.Tool;

/// <summary>
/// In-process tests for 'srectl tool show-types' command.
/// These tests are fast, debuggable, and don't require spawning processes.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class ShowTypesCommandTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public ShowTypesCommandTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "ShowTypes")]
    public async Task ToolShowTypes_WithoutParameters_ListsAllToolTypes()
    {
        // Act
        var result = await Runner.RunAsync("tool", "show-types");

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}, Error: {result.StandardError}");

        // Verify section header
        Assert.Contains("Available Tool Types", result.Output);

        // Verify both tool types are listed
        Assert.Contains("KustoTool", result.Output);
        Assert.Contains("LinkTool", result.Output);

        // Verify descriptions
        Assert.Contains("Execute Kusto queries against Azure Data Explorer clusters", result.Output);
        Assert.Contains("Generate URLs based on templates with parameter substitution", result.Output);

        // Verify total count
        Assert.Contains("2 tool type(s)", result.Output);

        // Verify help text
        Assert.Contains("Use 'srectl tool show-types --type <ToolTypeName>' for detailed information", result.Output);

        // Verify emoji is present
        Assert.Contains("🔧", result.Output);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "ShowTypes")]
    public async Task ToolShowTypes_WithKustoToolType_ShowsDetailedInformation()
    {
        // Act
        var result = await Runner.RunAsync("tool", "show-types", "--type", "KustoTool");

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}, Error: {result.StandardError}");

        // Verify section header with tool name
        Assert.Contains("Tool Type Details: KustoTool", result.Output);

        // Verify description
        Assert.Contains("Execute Kusto queries against Azure Data Explorer clusters", result.Output);

        // Verify Sample YAML section
        Assert.Contains("Sample YAML", result.Output);

        // Verify YAML content - metadata
        Assert.Contains("api_version: azuresre.ai/v2", result.Output);
        Assert.Contains("kind: ExtendedAgentTool", result.Output);
        Assert.Contains("metadata:", result.Output);
        Assert.Contains("name: MyKustoTool", result.Output);

        // Verify YAML content - spec
        Assert.Contains("spec:", result.Output);
        Assert.Contains("type: KustoTool", result.Output);
        Assert.Contains("connector: analytics-cluster", result.Output);
        Assert.Contains("database: kustodb", result.Output);
        Assert.Contains("query:", result.Output);

        // Verify parameters section
        Assert.Contains("parameters:", result.Output);
        Assert.Contains("- name: SubscriptionId", result.Output);
        Assert.Contains("- name: Tenant", result.Output);

        // Verify success message
        Assert.Contains("Tool type details displayed for 'KustoTool'", result.Output);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "ShowTypes")]
    public async Task ToolShowTypes_WithLinkToolType_ShowsDetailedInformation()
    {
        // Act
        var result = await Runner.RunAsync("tool", "show-types", "--type", "LinkTool");

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}, Error: {result.StandardError}");

        // Verify section header with tool name
        Assert.Contains("Tool Type Details: LinkTool", result.Output);

        // Verify description
        Assert.Contains("Generate URLs based on templates with parameter substitution", result.Output);

        // Verify Sample YAML section
        Assert.Contains("Sample YAML", result.Output);

        // Verify YAML content - metadata
        Assert.Contains("api_version: azuresre.ai/v2", result.Output);
        Assert.Contains("kind: ExtendedAgentTool", result.Output);
        Assert.Contains("metadata:", result.Output);
        Assert.Contains("name: MyLinkTool", result.Output);

        // Verify YAML content - spec
        Assert.Contains("spec:", result.Output);
        Assert.Contains("type: LinkTool", result.Output);
        Assert.Contains("template: https://example.com/{resourceId}", result.Output);

        // Verify parameters section
        Assert.Contains("parameters:", result.Output);
        Assert.Contains("- name: resourceId", result.Output);
        Assert.Contains("The resource identifier to include in the URL", result.Output);

        // Verify success message
        Assert.Contains("Tool type details displayed for 'LinkTool'", result.Output);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "ShowTypes")]
    public async Task ToolShowTypes_WithCaseInsensitiveToolType_ShowsDetails()
    {
        // Act - test with lowercase
        var result = await Runner.RunAsync("tool", "show-types", "--type", "kustotool");

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command should succeed with case-insensitive match. Exit code: {result.ExitCode}");
        Assert.Contains("Tool Type Details: KustoTool", result.Output);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "ShowTypes")]
    public async Task ToolShowTypes_WithInvalidToolType_ShowsError()
    {
        // Act
        var result = await Runner.RunAsync("tool", "show-types", "--type", "InvalidTool");

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.False(result.Success, "Command should fail with invalid tool type");

        // Verify error message
        Assert.Contains("Tool type 'InvalidTool' not found", result.Output);
        Assert.Contains("Use 'srectl tool show-types' to see available tool types", result.Output);
    }
}
