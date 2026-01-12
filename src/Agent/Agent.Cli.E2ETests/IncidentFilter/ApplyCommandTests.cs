// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.IncidentFilter;

/// <summary>
/// E2E tests for 'srectl incident-filter apply' command with mock backend.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class ApplyCommandTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public ApplyCommandTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Apply")]
    public async Task IncidentFilterApply_CreatesFilterOnServer()
    {
        // Arrange: Create an incident filter YAML file
        var filterName = "test-apply-filter";
        var filterYaml = TestYamlHelper.GetIcmIncidentFilterV2(
            filterName,
            handlingAgent: "DefaultAgent",
            impactedService: "MyService");

        Runner.CreateDirectory("IncidentFilters");
        Runner.CreateFile($"IncidentFilters/{filterName}.yaml", filterYaml);

        // Act: Apply the incident filter
        var result = await Runner.RunAsync("incident-filter", "apply", "--name", filterName);

        // Assert: Command should succeed
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        // Verify filter was created on server using E2E command
        var listResult = await Runner.RunAsync("incident-filter", "get", "--name", filterName);
        Assert.True(listResult.Success);
        Assert.Contains(filterName, listResult.StandardOutput);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Apply")]
    public async Task IncidentFilterApply_DryRun_DoesNotCreateFilter()
    {
        // Arrange: Create an incident filter YAML file
        var filterName = "dry-run-filter";
        var filterYaml = TestYamlHelper.GetIcmIncidentFilterV2(filterName);

        Runner.CreateDirectory("IncidentFilters");
        Runner.CreateFile($"IncidentFilters/{filterName}.yaml", filterYaml);

        // Act: Apply the filter with --dry-run flag
        var result = await Runner.RunAsync("incident-filter", "apply", "--name", filterName, "--dry-run");

        // Assert: Command should succeed
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("validated successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        // Verify filter was NOT created on server (dry run should not persist)
        // The get command should fail with "not found" when querying the specific filter
        var listResult = await Runner.RunAsync("incident-filter", "get", "--name", filterName);
        Assert.False(listResult.Success, "Get by name should fail when filter doesn't exist on server");
        Assert.Contains("not found", listResult.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Apply")]
    public async Task IncidentFilterApply_FilterNotFound_ReturnsError()
    {
        // Act: Try to apply a non-existent filter
        var result = await Runner.RunAsync("incident-filter", "apply", "--name", "non-existent-filter");

        // Assert: Command should fail
        _output.WriteLine(result.Output);
        Assert.False(result.Success);
        Assert.Contains("not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Apply")]
    public async Task IncidentFilterApply_UpdatesExistingFilter()
    {
        // Arrange: Create and apply initial filter
        var filterName = "updateable-filter";
        var initialYaml = TestYamlHelper.GetIcmIncidentFilterV2(
            filterName,
            handlingAgent: "InitialAgent");

        Runner.CreateDirectory("IncidentFilters");
        Runner.CreateFile($"IncidentFilters/{filterName}.yaml", initialYaml);
        await Runner.RunAsync("incident-filter", "apply", "--name", filterName);

        // Update the YAML with new handling agent
        var updatedYaml = TestYamlHelper.GetIcmIncidentFilterV2(
            filterName,
            handlingAgent: "UpdatedAgent");
        Runner.CreateFile($"IncidentFilters/{filterName}.yaml", updatedYaml);

        // Act: Apply the updated filter
        var result = await Runner.RunAsync("incident-filter", "apply", "--name", filterName);

        // Assert: Command should succeed
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        // Verify filter exists on server (updated successfully)
        var listResult = await Runner.RunAsync("incident-filter", "get", "--name", filterName);
        Assert.True(listResult.Success);
        Assert.Contains(filterName, listResult.StandardOutput);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Apply")]
    public async Task IncidentFilterApply_AzMonitorFilter_CreatesFilterOnServer()
    {
        // Arrange: Create an AzMonitor incident filter YAML file
        var filterName = "azmonitor-apply-filter";
        var filterYaml = TestYamlHelper.GetAzMonitorIncidentFilterV2(
            filterName,
            priority: "P1",
            targetResourceType: "Microsoft.Compute/virtualMachines");

        Runner.CreateDirectory("IncidentFilters");
        Runner.CreateFile($"IncidentFilters/{filterName}.yaml", filterYaml);

        // Act: Apply the incident filter
        var result = await Runner.RunAsync("incident-filter", "apply", "--name", filterName);

        // Assert: Command should succeed
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");

        // Verify filter was created on server
        var listResult = await Runner.RunAsync("incident-filter", "get", "--name", filterName);
        Assert.True(listResult.Success);
        Assert.Contains(filterName, listResult.StandardOutput);
    }
}
