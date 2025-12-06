// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Cli.Tests.E2E.Tool;

/// <summary>
/// In-process tests for 'srectl tool create' command.
/// These tests are fast, debuggable, and don't require spawning processes.
/// </summary>
[Collection("ToolTests")]
public class CreateCommandTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly CliTestRunner _cli;

    public CreateCommandTests(ITestOutputHelper output)
    {
        _output = output;
        _cli = new CliTestRunner();
        _output.WriteLine($"Test working directory: {_cli.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    public async Task ToolCreate_KustoTool_CreatesYamlFile()
    {
        // Arrange
        var toolName = "TestKustoTool";
        var toolType = "KustoTool";
        var description = "Test Kusto tool for E2E testing";

        // Act
        var result = await _cli.RunAsync(
            "tool", "create",
            "--name", toolName,
            "--type", toolType,
            "--description", description,
            "--connector", "test-connector",
            "--database", "test-db"
        );

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}, Error: {result.StandardError}");

        // Verify the YAML file was created
        var expectedPath = $"tools/{toolName}/{toolName}.yaml";
        Assert.True(_cli.FileExists(expectedPath), $"YAML file should exist at {expectedPath}");

        // Verify the YAML content
        var yamlContent = _cli.ReadFile(expectedPath);
        _output.WriteLine("=== YAML Content ===");
        _output.WriteLine(yamlContent);
        _output.WriteLine("====================");

        // Parse and validate YAML structure
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        // Validate required fields
        Assert.True(yamlDict.ContainsKey("api_version"), "YAML should contain api_version");
        Assert.True(yamlDict.ContainsKey("kind"), "YAML should contain kind");
        Assert.Equal("ExtendedTool", yamlDict["kind"].ToString());

        // Validate metadata
        Assert.True(yamlDict.ContainsKey("metadata"), "YAML should contain metadata");
        var metadata = yamlDict["metadata"] as Dictionary<object, object>;
        Assert.NotNull(metadata);
        Assert.Equal(toolName, metadata["name"].ToString());

        // Validate spec
        Assert.True(yamlDict.ContainsKey("spec"), "YAML should contain spec");
        var spec = yamlDict["spec"] as Dictionary<object, object>;
        Assert.NotNull(spec);
        Assert.Equal(toolType, spec["type"].ToString());
        Assert.Equal(description, spec["description"].ToString());

        // Validate KustoTool-specific fields
        Assert.Equal("test-connector", spec["connector"].ToString());
        Assert.Equal("test-db", spec["database"].ToString());

        // Verify success message in output
        Assert.Contains("created", result.Output, StringComparison.OrdinalIgnoreCase);
        // Path separator normalization for cross-platform compatibility
        var normalizedExpectedPath = expectedPath.Replace('/', Path.DirectorySeparatorChar);
        Assert.Contains(normalizedExpectedPath, result.Output, StringComparison.OrdinalIgnoreCase);

        // Validate the created tool
        var validateResult = await _cli.RunAsync(
            "tool", "validate",
            "--name", toolName
        );
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "Created tool should be valid");
        Assert.Equal(0, validateResult.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    public async Task ToolCreate_WithFlatStructure_CreatesInToolsRoot()
    {
        // Arrange
        var toolName = "FlatTool";

        // Act - using empty string for --path means flat structure
        var result = await _cli.RunAsync(
            "tool", "create",
            "--name", toolName,
            "--type", "KustoTool",
            "--path", "" // Empty path means flat structure
        );

        // Assert
        Assert.True(result.Success, $"Command should succeed. Error: {result.StandardError}");

        // Verify file is in flat structure: tools/FlatTool.yaml (not tools/FlatTool/FlatTool.yaml)
        var expectedPath = $"tools/{toolName}.yaml";
        Assert.True(_cli.FileExists(expectedPath), $"YAML file should exist at {expectedPath}");

        // Verify legacy structure does NOT exist
        var legacyPath = $"tools/{toolName}/{toolName}.yaml";
        Assert.False(_cli.FileExists(legacyPath), $"File should not exist at legacy path {legacyPath}");

        // Validate the created tool
        var validateResult = await _cli.RunAsync(
            "tool", "validate",
            "--name", toolName
        );
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "Created tool should be valid");
        Assert.Equal(0, validateResult.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    public async Task ToolCreate_WithCustomPath_CreatesInSubfolder()
    {
        // Arrange
        var toolName = "CustomPathTool";
        var customPath = "monitoring";

        // Act
        var result = await _cli.RunAsync(
            "tool", "create",
            "--name", toolName,
            "--type", "KustoTool",
            "--path", customPath
        );

        // Assert
        Assert.True(result.Success, $"Command should succeed. Error: {result.StandardError}");

        // Verify file is in custom subfolder: tools/monitoring/CustomPathTool.yaml
        var expectedPath = $"tools/{customPath}/{toolName}.yaml";
        Assert.True(_cli.FileExists(expectedPath), $"YAML file should exist at {expectedPath}");

        // Validate the created tool
        var validateResult = await _cli.RunAsync(
            "tool", "validate",
            "--name", toolName
        );
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "Created tool should be valid");
        Assert.Equal(0, validateResult.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    public async Task ToolCreate_LinkTool_CreatesValidYaml()
    {
        // Arrange
        var toolName = "TestLinkTool";
        var description = "Test link tool";
        var template = "https://example.com/{id}";

        // Act
        var result = await _cli.RunAsync(
            "tool", "create",
            "--name", toolName,
            "--type", "LinkTool",
            "--description", description,
            "--template", template,
            "--parameter", "id:string:The identifier"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should succeed. Error: {result.StandardError}");

        var expectedPath = $"tools/{toolName}/{toolName}.yaml";
        Assert.True(_cli.FileExists(expectedPath), $"YAML file should exist at {expectedPath}");

        var yamlContent = _cli.ReadFile(expectedPath);
        Assert.Contains("LinkTool", yamlContent);
        Assert.Contains(template, yamlContent);

        // Validate the created tool
        var validateResult = await _cli.RunAsync(
            "tool", "validate",
            "--name", toolName
        );
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "Created tool should be valid");
        Assert.Equal(0, validateResult.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    public async Task ToolCreate_MissingRequiredOption_ReturnsError()
    {
        // Act - missing --type option
        var result = await _cli.RunAsync(
            "tool", "create",
            "--name", "InvalidTool"
        );

        // Assert
        Assert.False(result.Success, "Command should fail when required option is missing");
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    public async Task ToolCreate_KustoToolMinimal_CreatesValidTool()
    {
        // Act - KustoTool with minimal parameters (defaults provided by template)
        var result = await _cli.RunAsync(
            "tool", "create",
            "--name", "TestKustoMinimal",
            "--type", "KustoTool"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should succeed. Error: {result.StandardError}");

        var expectedPath = "tools/TestKustoMinimal/TestKustoMinimal.yaml";
        Assert.True(_cli.FileExists(expectedPath), $"YAML file should exist at {expectedPath}");

        var yamlContent = _cli.ReadFile(expectedPath);
        Assert.Contains("connector:", yamlContent);
        Assert.Contains("database:", yamlContent);

        // Validate the created tool
        var validateResult = await _cli.RunAsync(
            "tool", "validate",
            "--name", "TestKustoMinimal"
        );
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "Created tool should be valid");
        Assert.Equal(0, validateResult.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    public async Task ToolCreate_LinkToolMinimal_CreatesValidTool()
    {
        // Act - LinkTool with minimal parameters (defaults provided by template)
        var result = await _cli.RunAsync(
            "tool", "create",
            "--name", "TestLinkMinimal",
            "--type", "LinkTool"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should succeed. Error: {result.StandardError}");

        var expectedPath = "tools/TestLinkMinimal/TestLinkMinimal.yaml";
        Assert.True(_cli.FileExists(expectedPath), $"YAML file should exist at {expectedPath}");

        var yamlContent = _cli.ReadFile(expectedPath);
        Assert.Contains("template:", yamlContent);

        // Validate the created tool
        var validateResult = await _cli.RunAsync(
            "tool", "validate",
            "--name", "TestLinkMinimal"
        );
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "Created tool should be valid");
        Assert.Equal(0, validateResult.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    public async Task ToolCreate_NameWithSpecialCharacters_HandlesGracefully()
    {
        // Act - name with special characters that might cause issues
        var result = await _cli.RunAsync(
            "tool", "create",
            "--name", "Test-Tool_123",
            "--type", "KustoTool"
        );

        // Assert
        _output.WriteLine(result.Output);
        // Hyphens and underscores should be acceptable
        Assert.True(result.Success, $"Command should accept valid special characters. Error: {result.StandardError}");

        // Validate the created tool
        var validateResult = await _cli.RunAsync(
            "tool", "validate",
            "--name", "Test-Tool_123"
        );
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "Created tool should be valid");
        Assert.Equal(0, validateResult.ExitCode);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    public async Task ToolCreate_MultipleParameters_ParsesCorrectly()
    {
        // Act - multiple parameter definitions
        var result = await _cli.RunAsync(
            "tool", "create",
            "--name", "MultiParamTool",
            "--type", "LinkTool",
            "--template", "https://example.com/{id}/{type}/{region}",
            "--parameter", "id:string:The ID",
            "--parameter", "type:string:The type",
            "--parameter", "region:string:The region"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should handle multiple parameters. Error: {result.StandardError}");

        var expectedPath = "tools/MultiParamTool/MultiParamTool.yaml";
        Assert.True(_cli.FileExists(expectedPath), $"YAML file should exist at {expectedPath}");

        var yamlContent = _cli.ReadFile(expectedPath);
        Assert.Contains("id", yamlContent);
        Assert.Contains("type", yamlContent);
        Assert.Contains("region", yamlContent);

        // Validate the created tool
        var validateResult = await _cli.RunAsync(
            "tool", "validate",
            "--name", "MultiParamTool"
        );
        _output.WriteLine($"Validation output: {validateResult.Output}");
        Assert.True(validateResult.Success, "Created tool should be valid");
        Assert.Equal(0, validateResult.ExitCode);
    }

    public void Dispose()
    {
        _cli.Dispose();
    }
}
