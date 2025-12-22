// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.Tool;

/// <summary>
/// E2E tests for 'srectl tool validate' command.
/// Tests validation of tool YAML files in V2 format.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class ValidateCommandTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public ValidateCommandTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_ValidKustoTool_SucceedsWithName()
    {
        // Arrange - create a valid V2 KustoTool
        var toolName = "ValidKustoTool";
        var parameters = new List<(string name, string type, string description)>
        {
            ("param1", "string", "Test parameter")
        };
        var yamlContent = TestYamlHelper.GetKustoToolV2(
            toolName,
            description: "A valid Kusto tool for testing",
            connector: "test-connector",
            database: "test-database",
            query: "TestQuery | take 10",
            parameters: parameters);
        Runner.CreateFile($"tools/{toolName}/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("succeeded", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(toolName, result.Output);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_ValidLinkTool_SucceedsWithName()
    {
        // Arrange - create a valid V2 LinkTool
        var toolName = "ValidLinkTool";
        var parameters = new List<(string name, string type, string description)>
        {
            ("id", "string", "The identifier")
        };
        var yamlContent = TestYamlHelper.GetLinkToolV2(
            toolName,
            description: "A valid link tool for testing",
            template: "https://example.com/{id}",
            parameters: parameters);
        Runner.CreateFile($"tools/{toolName}/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("succeeded", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_FlatStructure_SucceedsWithName()
    {
        // Arrange - create a tool in flat structure (tools/ToolName.yaml)
        var toolName = "FlatStructureTool";
        var yamlContent = TestYamlHelper.GetKustoToolV2(
            toolName,
            description: "Tool in flat structure",
            connector: "test-connector",
            database: "test-db",
            query: "TestQuery | take 10");
        Runner.CreateFile($"tools/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should succeed for flat structure. Exit code: {result.ExitCode}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("succeeded", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_AllValidTools_SucceedsWithAll()
    {
        // Arrange - create multiple valid tools
        var tool1 = "Tool1";
        var tool2 = "Tool2";
        var yamlContent1 = TestYamlHelper.GetKustoToolV2(
            tool1,
            description: "First test tool",
            connector: "connector1",
            database: "db1",
            query: "TestQuery | take 10");
        var parameters2 = new List<(string name, string type, string description)>
        {
            ("id", "string", "ID parameter")
        };
        var yamlContent2 = TestYamlHelper.GetLinkToolV2(
            tool2,
            description: "Second test tool",
            template: "https://example.com/{id}",
            parameters: parameters2);
        Runner.CreateFile($"tools/{tool1}/{tool1}.yaml", yamlContent1);
        Runner.CreateFile($"tools/{tool2}/{tool2}.yaml", yamlContent2);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--all"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should succeed when all tools are valid. Exit code: {result.ExitCode}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("All tool YAML files are valid", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_InvalidKustoTool_FailsWithName()
    {
        // Arrange - create an invalid V2 KustoTool (missing required connector)
        var toolName = "InvalidKustoTool";
        var yamlContent = @"api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: InvalidKustoTool
spec:
  type: KustoTool
  description: Invalid Kusto tool missing connector
  database: test-database
";
        Runner.CreateFile($"tools/{toolName}/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail for invalid tool");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("failed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_InvalidLinkTool_FailsWithName()
    {
        // Arrange - create an invalid V2 LinkTool (missing required template)
        var toolName = "InvalidLinkTool";
        var yamlContent = @"api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: InvalidLinkTool
spec:
  type: LinkTool
  description: Invalid link tool missing template
  parameters:
    - name: id
      type: string
      description: ID parameter
";
        Runner.CreateFile($"tools/{toolName}/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail for invalid LinkTool");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("failed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_MixedValidAndInvalid_FailsWithAll()
    {
        // Arrange - create mix of valid and invalid tools
        var validTool = "ValidTool";
        var invalidTool = "InvalidTool";

        var validYaml = TestYamlHelper.GetKustoToolV2(
            validTool,
            description: "Valid tool",
            connector: "connector1",
            database: "db1");
        var invalidYaml = @"api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: InvalidTool
spec:
  type: KustoTool
  description: Invalid tool missing connector
  database: db1
";
        Runner.CreateFile($"tools/{validTool}/{validTool}.yaml", validYaml);
        Runner.CreateFile($"tools/{invalidTool}/{invalidTool}.yaml", invalidYaml);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--all"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when any tool is invalid");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Some tool YAML files failed validation", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_NonExistentTool_FailsWithName()
    {
        // Act - try to validate a tool that doesn't exist
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", "NonExistentTool"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail for non-existent tool");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_NoToolsDirectory_FailsWithAll()
    {
        // Act - validate all when no tools directory exists
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--all"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when no tools directory exists");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("No tools directory found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_EmptyToolsDirectory_FailsWithAll()
    {
        // Arrange - create empty tools directory
        Directory.CreateDirectory(Path.Combine(Runner.WorkingDirectory, "tools"));

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--all"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when tools directory is empty");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("No tool YAML files found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_MalformedYaml_FailsWithName()
    {
        // Arrange - create a tool with malformed YAML
        var toolName = "MalformedTool";
        var yamlContent = @"api_version: v2
kind: ExtendedAgentTool
metadata:
  name: MalformedTool
spec:
  type: KustoTool
  description: Malformed YAML
    connector: test-connector
  invalid_indent
  database: test-db
";
        Runner.CreateFile($"tools/{toolName}/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail for malformed YAML");
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_InvalidYamlStructure_FailsWithName()
    {
        // Arrange - create a YAML file that's not a valid tool
        var toolName = "NotATool";
        var yamlContent = @"# This is not a tool configuration
random_key: random_value
another_key: another_value
";
        Runner.CreateFile($"tools/{toolName}/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail for non-tool YAML");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Not a valid extended tool YAML", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_ToolWithComplexParameters_Succeeds()
    {
        // Arrange - create a tool with multiple parameters
        var toolName = "ComplexParametersTool";
        var parameters = new List<(string name, string type, string description)>
        {
            ("param1", "string", "String parameter"),
            ("param2", "int", "Integer parameter"),
            ("param3", "bool", "Boolean parameter")
        };
        var yamlContent = TestYamlHelper.GetKustoToolV2(
            toolName,
            description: "Tool with complex parameters",
            connector: "test-connector",
            database: "test-db",
            query: "TestQuery | where Name == \"{param1}\" and ID == {param2}",
            parameters: parameters);
        Runner.CreateFile($"tools/{toolName}/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should succeed for tool with multiple parameters. Exit code: {result.ExitCode}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("succeeded", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_ToolInCustomPath_SucceedsWithName()
    {
        // Arrange - create a tool in a custom subdirectory
        var toolName = "CustomPathTool";
        var customPath = "monitoring";
        var yamlContent = TestYamlHelper.GetKustoToolV2(
            toolName,
            description: "Tool in custom path",
            connector: "test-connector",
            database: "test-db",
            query: "TestQuery | take 10");
        Runner.CreateFile($"tools/{customPath}/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should find and validate tool in custom path. Exit code: {result.ExitCode}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("succeeded", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_ValidPythonTool_SucceedsWithName()
    {
        // Arrange - create a valid V2 PythonTool
        var toolName = "ValidPythonTool";
        var parameters = new List<(string name, string type, string description)>
        {
            ("input", "string", "The input parameter")
        };
        var yamlContent = TestYamlHelper.GetPythonToolV2(
            toolName,
            description: "A valid Python tool for testing",
            timeoutSeconds: 60,
            dependencies: new List<string> { "requests", "pandas" },
            parameters: parameters);
        Runner.CreateFile($"tools/{toolName}/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("succeeded", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(toolName, result.Output);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_PythonToolMissingFunctionCode_FailsWithName()
    {
        // Arrange - create a PythonTool without functionCode
        var toolName = "InvalidPythonTool";
        var yamlContent = @"api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: InvalidPythonTool
spec:
  type: PythonTool
  toolMode: Auto
  description: ""Missing function code""
  timeoutSeconds: 30
";
        Runner.CreateFile($"tools/{toolName}/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail for PythonTool without functionCode");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("functionCode", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Validate")]
    public async Task ToolValidate_PythonToolWithInvalidTimeout_FailsWithName()
    {
        // Arrange - create a PythonTool with invalid timeout
        var toolName = "InvalidTimeoutPythonTool";
        var yamlContent = @"api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: InvalidTimeoutPythonTool
spec:
  type: PythonTool
  toolMode: Auto
  description: ""Invalid timeout""
  functionCode: |-
    def execute(**kwargs):
        return {}
  timeoutSeconds: 0
";
        Runner.CreateFile($"tools/{toolName}/{toolName}.yaml", yamlContent);

        // Act
        var result = await Runner.RunAsync(
            "tool", "validate",
            "--name", toolName
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail for PythonTool with invalid timeout");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("timeoutSeconds", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("greater than 0", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
