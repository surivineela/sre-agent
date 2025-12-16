// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.Tool;

/// <summary>
/// Tests for invalid parameter validation in 'srectl tool apply' command.
/// Only tests invalid parameter combinations and error messages.
/// Business logic errors (tool not found, invalid YAML, etc.) are tested in ApplyCommandTests.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class ApplyCommandInvalidParameterTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public ApplyCommandInvalidParameterTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Apply")]
    [Trait("Type", "Validation")]
    public async Task ToolApply_MissingName_ReturnsError()
    {
        // Act - missing --name option
        var result = await Runner.RunAsync("tool", "apply");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is missing");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Tool name must not be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Apply")]
    [Trait("Type", "Validation")]
    public async Task ToolApply_InvalidOption_ReturnsError()
    {
        // Act - use an invalid/unknown option
        var result = await Runner.RunAsync("tool", "apply", "--name", "some-tool", "--invalid-option", "value");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when an invalid option is provided");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("invalid-option", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Apply")]
    [Trait("Type", "Validation")]
    public async Task ToolApply_NameTooLong_ReturnsError()
    {
        // Act - name exceeds 128 characters
        var longName = new string('a', 129);
        var result = await Runner.RunAsync("tool", "apply", "--name", longName);

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name exceeds 128 characters");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("128 characters", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Apply")]
    [Trait("Type", "Validation")]
    public async Task ToolApply_NameWithInvalidCharacters_ReturnsError()
    {
        // Act - name contains special characters
        var result = await Runner.RunAsync("tool", "apply", "--name", "invalid@name!");

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
    [Trait("Command", "Apply")]
    [Trait("Type", "Validation")]
    public async Task ToolApply_NameWithSpaces_ReturnsError()
    {
        // Act - name contains spaces
        var result = await Runner.RunAsync("tool", "apply", "--name", "tool with spaces");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name contains spaces");
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(
            result.Output.Contains("letters", StringComparison.OrdinalIgnoreCase) ||
            result.Output.Contains("a-z", StringComparison.OrdinalIgnoreCase),
            "Output should indicate valid character requirements");
    }
}
