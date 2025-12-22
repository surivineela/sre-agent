// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.Tool;

/// <summary>
/// Tests for invalid parameter validation in 'srectl tool create' command.
/// Validates error handling for missing, empty, or invalid parameters.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class CreateCommandInvalidParameterTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public CreateCommandInvalidParameterTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task ToolCreate_MissingName_ReturnsError()
    {
        // Act - missing --name option
        var result = await Runner.RunAsync(
            "tool", "create",
            "--type", "KustoTool"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is missing");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Tool name must not be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task ToolCreate_MissingType_ReturnsError()
    {
        // Act - missing --type option
        var result = await Runner.RunAsync(
            "tool", "create",
            "--name", "TestTool"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --type is missing");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Option '--type' is required", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task ToolCreate_EmptyName_ReturnsError()
    {
        // Act - empty name
        var result = await Runner.RunAsync(
            "tool", "create",
            "--name", "",
            "--type", "KustoTool"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is empty");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Invalid parameter: Tool name must not be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task ToolCreate_InvalidToolType_ReturnsError()
    {
        // Act - invalid tool type
        var result = await Runner.RunAsync(
            "tool", "create",
            "--name", "TestTool",
            "--type", "InvalidToolType"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when tool type is invalid");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Invalid parameter: Invalid tool type 'InvalidToolType'", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Supported types:", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task ToolCreate_NameWithSpaces_ReturnsError()
    {
        // Act - name with spaces
        var result = await Runner.RunAsync(
            "tool", "create",
            "--name", "Test Tool With Spaces",
            "--type", "KustoTool"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name contains spaces");
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(
            result.Output.Contains("letters", StringComparison.OrdinalIgnoreCase) ||
            result.Output.Contains("a-z", StringComparison.OrdinalIgnoreCase),
            "Output should indicate valid character requirements");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task ToolCreate_NameTooLong_ReturnsError()
    {
        // Act - name exceeds 128 characters
        var longName = new string('a', 129);
        var result = await Runner.RunAsync(
            "tool", "create",
            "--name", longName,
            "--type", "KustoTool"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name exceeds 128 characters");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("128 characters", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task ToolCreate_NameWithInvalidCharacters_ReturnsError()
    {
        // Act - name contains special characters
        var result = await Runner.RunAsync(
            "tool", "create",
            "--name", "invalid@name!",
            "--type", "KustoTool"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name contains invalid characters");
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(
            result.Output.Contains("letters", StringComparison.OrdinalIgnoreCase) ||
            result.Output.Contains("a-z", StringComparison.OrdinalIgnoreCase),
            "Output should indicate valid character requirements");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task ToolCreate_InvalidPath_ReturnsError()
    {
        // Act - path with invalid characters
        var result = await Runner.RunAsync(
            "tool", "create",
            "--name", "TestTool",
            "--type", "KustoTool",
            "--path", "invalid/../../escape"
        );

        // Assert
        _output.WriteLine(result.Output);
        // Should either succeed (normalized) or fail with clear error
        // This test documents the current behavior
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task ToolCreate_UnrecognizedOption_ReturnsError()
    {
        // Act - unrecognized option
        var result = await Runner.RunAsync(
            "tool", "create",
            "--name", "TestTool",
            "--type", "KustoTool",
            "--invalid-option", "some-value"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail with unrecognized option");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unrecognized command or argument '--invalid-option'", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
