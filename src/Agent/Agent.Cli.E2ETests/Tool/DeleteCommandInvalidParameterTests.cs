// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.Tool;

/// <summary>
/// Tests for invalid parameter validation in 'srectl tool delete' command.
/// Validates error handling for missing, empty, or invalid parameters.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class DeleteCommandInvalidParameterTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public DeleteCommandInvalidParameterTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task ToolDelete_MissingName_ReturnsError()
    {
        // Act: Try to delete without --name parameter
        var result = await Runner.RunAsync("tool", "delete");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is missing");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--name", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task ToolDelete_EmptyName_ReturnsError()
    {
        // Act: Try to delete with empty name
        var result = await Runner.RunAsync("tool", "delete", "--name", "");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is empty");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Invalid parameter: Tool name must not be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task ToolDelete_WhitespaceName_ReturnsError()
    {
        // Act: Try to delete with whitespace name
        var result = await Runner.RunAsync("tool", "delete", "--name", "   ");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is whitespace");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Invalid parameter: Tool name must not be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task ToolDelete_NameTooLong_ReturnsError()
    {
        // Act: name exceeds 128 characters
        var longName = new string('a', 129);
        var result = await Runner.RunAsync("tool", "delete", "--name", longName);

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name exceeds 128 characters");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("128 characters", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task ToolDelete_NameWithInvalidCharacters_ReturnsError()
    {
        // Act: name contains special characters
        var result = await Runner.RunAsync("tool", "delete", "--name", "invalid@name!");

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
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task ToolDelete_NameWithSpaces_ReturnsError()
    {
        // Act: name with spaces
        var result = await Runner.RunAsync("tool", "delete", "--name", "Tool With Spaces");

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
