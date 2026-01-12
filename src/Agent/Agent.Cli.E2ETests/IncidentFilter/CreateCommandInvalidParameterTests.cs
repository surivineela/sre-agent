// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.IncidentFilter;

/// <summary>
/// Tests for invalid parameter validation in 'srectl incident-filter create' command.
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
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterCreate_MissingName_ReturnsError()
    {
        // Act - missing --name option
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--platform", "IcM"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --name is missing");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Incident filter name must not be empty", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterCreate_MissingPlatform_ReturnsError()
    {
        // Act - missing --platform option
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", "TestFilter"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when --platform is missing");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--platform", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterCreate_IcmOptionsWithAzMonitorPlatform_ReturnsError()
    {
        // Act - using IcM-specific options with AzMonitor platform
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", "TestFilter",
            "--platform", "AzMonitor",
            "--monitor-id", "monitor-123"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when using IcM options with AzMonitor platform");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--monitor-id", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IcM", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterCreate_AzMonitorOptionsWithIcmPlatform_ReturnsError()
    {
        // Act - using AzMonitor-specific options with IcM platform
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", "TestFilter",
            "--platform", "IcM",
            "--target-resource-type", "Microsoft.Compute/virtualMachines"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when using AzMonitor options with IcM platform");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--target-resource-type", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AzMonitor", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterCreate_CreatedByWithAzMonitorPlatform_ReturnsError()
    {
        // Act - using --created-by (IcM option) with AzMonitor platform
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", "TestFilter",
            "--platform", "AzMonitor",
            "--created-by", "user@contoso.com"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when using --created-by with AzMonitor platform");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--created-by", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterCreate_NameTooLong_ReturnsError()
    {
        // Act - name exceeds 128 characters
        var longName = new string('a', 129);
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", longName,
            "--platform", "IcM"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when name exceeds 128 characters");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("128 characters", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterCreate_NameWithInvalidCharacters_ReturnsError()
    {
        // Act - name contains special characters
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", "invalid@name!",
            "--platform", "IcM"
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
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterCreate_NameWithSpaces_ReturnsError()
    {
        // Act - name contains spaces
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", "filter with spaces",
            "--platform", "IcM"
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
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    [Trait("Type", "Validation")]
    public async Task IncidentFilterCreate_TargetResourceWithIcmPlatform_ReturnsError()
    {
        // Act - using --target-resource (AzMonitor option) with IcM platform
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", "TestFilter",
            "--platform", "IcM",
            "--target-resource", "/subscriptions/xxx/resourceGroups/rg1"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when using --target-resource with IcM platform");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--target-resource", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
