// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.IncidentFilter;

/// <summary>
/// Tests for invalid parameter validation in 'srectl incident-filter delete' command.
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
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterDelete_MissingName_ReturnsError()
    {
        // Act - missing --name option
        var result = await Runner.RunAsync("incident-filter", "delete");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is missing");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Incident filter name must not be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterDelete_EmptyName_ReturnsError()
    {
        // Act - empty name
        var result = await Runner.RunAsync("incident-filter", "delete", "--name", "");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is empty");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Incident filter name must not be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterDelete_WhitespaceName_ReturnsError()
    {
        // Act - whitespace name
        var result = await Runner.RunAsync("incident-filter", "delete", "--name", "   ");

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is whitespace");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Incident filter name must not be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterDelete_NameTooLong_ReturnsError()
    {
        // Act - name exceeds 128 characters
        var longName = new string('a', 129);
        var result = await Runner.RunAsync("incident-filter", "delete", "--name", longName);

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name exceeds 128 characters");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("128 characters", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterDelete_NameWithInvalidCharacters_ReturnsError()
    {
        // Act - name contains special characters
        var result = await Runner.RunAsync("incident-filter", "delete", "--name", "invalid@name!");

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
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Delete")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterDelete_NameWithSpaces_ReturnsError()
    {
        // Act - name contains spaces
        var result = await Runner.RunAsync("incident-filter", "delete", "--name", "filter with spaces");

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
