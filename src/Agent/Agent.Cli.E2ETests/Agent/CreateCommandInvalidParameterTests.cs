// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.AgentCommands;

/// <summary>
/// Tests for invalid parameter validation in 'srectl agent create' command.
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
    [Trait("Category", "Agent")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task AgentCreate_MissingName_ReturnsError()
    {
        // Act - missing --name option (required parameter)
        var result = await Runner.RunAsync("agent", "create");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is missing");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--name", result.Output);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task AgentCreate_EmptyName_ReturnsError()
    {
        // Act - empty name
        var result = await Runner.RunAsync("agent", "create", "--name", "");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is empty");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Agent name must not be empty", result.Output);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task AgentCreate_NameWithSpaces_ReturnsError()
    {
        // Act - name with spaces
        var result = await Runner.RunAsync("agent", "create", "--name", "agent with spaces");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name contains spaces");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Agent name must only contain", result.Output);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task AgentCreate_NameTooLong_ReturnsError()
    {
        // Act - name exceeds 128 characters
        var longName = new string('a', 129);
        var result = await Runner.RunAsync("agent", "create", "--name", longName);

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name exceeds 128 characters");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Agent name must be less than 128 characters", result.Output);
        Assert.Contains("Current length: 129", result.Output);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task AgentCreate_NameWithInvalidCharacters_ReturnsError()
    {
        // Act - name contains special characters
        var result = await Runner.RunAsync("agent", "create", "--name", "invalid@agent!");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name contains invalid characters");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Agent name must only contain", result.Output);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task AgentCreate_UnrecognizedOption_ReturnsError()
    {
        // Act - unrecognized option
        var result = await Runner.RunAsync(
            "agent", "create",
            "--name", "test-agent",
            "--invalid-option", "some-value"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail with unrecognized option");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unrecognized", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
