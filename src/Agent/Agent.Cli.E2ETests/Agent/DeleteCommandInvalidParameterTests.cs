// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.AgentCommands;

/// <summary>
/// Tests for invalid parameter validation in 'srectl agent delete' command.
/// Only tests invalid parameter combinations and error messages.
/// Business logic errors (agent not found, etc.) are tested in DeleteCommandTests.
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
    [Trait("Category", "Agent")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task AgentDelete_MissingName_ReturnsError()
    {
        // Act - missing --name option
        var result = await Runner.RunAsync("agent", "delete");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is missing");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Agent name must not be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task AgentDelete_InvalidOption_ReturnsError()
    {
        // Act - use an invalid/unknown option
        var result = await Runner.RunAsync("agent", "delete", "--name", "some-agent", "--invalid-option", "value");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when an invalid option is provided");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("invalid-option", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task AgentDelete_NameTooLong_ReturnsError()
    {
        // Act - name exceeds 128 characters
        var longName = new string('a', 129);
        var result = await Runner.RunAsync("agent", "delete", "--name", longName);

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name exceeds 128 characters");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("128 characters", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task AgentDelete_NameWithInvalidCharacters_ReturnsError()
    {
        // Act - name contains invalid characters
        var result = await Runner.RunAsync("agent", "delete", "--name", "agent@name!");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name contains invalid characters");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("letters", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("numbers", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task AgentDelete_EmptyName_ReturnsError()
    {
        // Act - name is empty string
        var result = await Runner.RunAsync("agent", "delete", "--name", "");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name is empty");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Agent name must not be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task AgentDelete_NameWithSpaces_ReturnsError()
    {
        // Act - name contains spaces
        var result = await Runner.RunAsync("agent", "delete", "--name", "agent name");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name contains spaces");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("letters", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("numbers", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
