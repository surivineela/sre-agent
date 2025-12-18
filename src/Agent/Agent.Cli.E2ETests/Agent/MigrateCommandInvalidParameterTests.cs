// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.AgentCommands;

/// <summary>
/// Tests for invalid parameter validation in 'srectl agent migrate' command.
/// Only tests invalid parameter combinations and error messages.
/// Business logic errors (agent not found, invalid YAML, etc.) are tested in MigrateCommandTests.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class MigrateCommandInvalidParameterTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public MigrateCommandInvalidParameterTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Validation")]
    public async Task AgentMigrate_MissingNameAndAll_ReturnsError()
    {
        // Act - missing both --name and --all options
        var result = await Runner.RunAsync("agent", "migrate");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when both --name and --all are missing");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Please specify --name or --all to migrate agents", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Validation")]
    public async Task AgentMigrate_BothNameAndAll_ReturnsError()
    {
        // Act - provide both --name and --all options
        var result = await Runner.RunAsync("agent", "migrate", "--name", "test-agent", "--all");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when both --name and --all are provided");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Cannot specify both --name and --all", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Validation")]
    public async Task AgentMigrate_InvalidOption_ReturnsError()
    {
        // Act - use an invalid/unknown option
        var result = await Runner.RunAsync("agent", "migrate", "--name", "some-agent", "--invalid-option", "value");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when an invalid option is provided");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("invalid-option", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Validation")]
    public async Task AgentMigrate_NameTooLong_ReturnsError()
    {
        // Arrange - create agents directory but with no matching file
        Directory.CreateDirectory(Path.Combine(Runner.WorkingDirectory, "agents"));

        // Act - name exceeds 128 characters
        var longName = new string('a', 129);
        var result = await Runner.RunAsync("agent", "migrate", "--name", longName);

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when agent file doesn't exist");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Agent file not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Validation")]
    public async Task AgentMigrate_NameWithInvalidCharacters_ReturnsError()
    {
        // Arrange - create agents directory but with no matching file
        Directory.CreateDirectory(Path.Combine(Runner.WorkingDirectory, "agents"));

        // Act - name contains invalid characters
        var result = await Runner.RunAsync("agent", "migrate", "--name", "agent@name!");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when agent file doesn't exist");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Agent file not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Validation")]
    public async Task AgentMigrate_EmptyName_ReturnsError()
    {
        // Act - name is empty string
        var result = await Runner.RunAsync("agent", "migrate", "--name", "");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name is empty");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Please specify --name or --all to migrate agents", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Validation")]
    public async Task AgentMigrate_NameWithSpaces_ReturnsError()
    {
        // Arrange - create agents directory but with no matching file
        Directory.CreateDirectory(Path.Combine(Runner.WorkingDirectory, "agents"));

        // Act - name contains spaces
        var result = await Runner.RunAsync("agent", "migrate", "--name", "agent name");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when agent file doesn't exist");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Agent file not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
