// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.IncidentFilter;

/// <summary>
/// E2E tests for 'srectl incident-filter get' command with mock backend.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class GetCommandTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public GetCommandTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Get")]
    public async Task IncidentFilterGet_NoFilters_ShowsEmptyMessage()
    {
        // Act: Get filters when none exist
        var result = await Runner.RunAsync("incident-filter", "get");

        // Assert: Should show informational message
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}");
        Assert.Contains("No incident filters found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Get")]
    public async Task IncidentFilterGet_ListsAllFilters()
    {
        // Arrange: Create and apply multiple filters
        Runner.CreateDirectory("IncidentFilters");

        var filter1 = "test-filter-1";
        Runner.CreateFile($"IncidentFilters/{filter1}.yaml", TestYamlHelper.GetIcmIncidentFilterV2(filter1));
        await Runner.RunAsync("incident-filter", "apply", "--name", filter1);

        var filter2 = "test-filter-2";
        Runner.CreateFile($"IncidentFilters/{filter2}.yaml", TestYamlHelper.GetAzMonitorIncidentFilterV2(filter2));
        await Runner.RunAsync("incident-filter", "apply", "--name", filter2);

        // Act: Get all filters
        var result = await Runner.RunAsync("incident-filter", "get");

        // Assert: Should list both filters
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains(filter1, result.Output);
        Assert.Contains(filter2, result.Output);
        Assert.Contains("2 incident filter", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Get")]
    public async Task IncidentFilterGet_ByName_ShowsSpecificFilter()
    {
        // Arrange: Create and apply a filter
        var filterName = "specific-filter";
        Runner.CreateDirectory("IncidentFilters");
        Runner.CreateFile($"IncidentFilters/{filterName}.yaml",
            TestYamlHelper.GetIcmIncidentFilterV2(filterName, handlingAgent: "MyAgent"));
        await Runner.RunAsync("incident-filter", "apply", "--name", filterName);

        // Act: Get specific filter by name
        var result = await Runner.RunAsync("incident-filter", "get", "--name", filterName);

        // Assert: Should show the specific filter details
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains(filterName, result.Output);
        Assert.Contains("IcM", result.Output);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Get")]
    public async Task IncidentFilterGet_ByName_NotFound_ReturnsError()
    {
        // Act: Get a non-existent filter by name
        var result = await Runner.RunAsync("incident-filter", "get", "--name", "non-existent-filter");

        // Assert: Should fail with not found error
        _output.WriteLine(result.Output);
        Assert.False(result.Success);
        Assert.Contains("not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Get")]
    public async Task IncidentFilterGet_WithDetail_ShowsFullYaml()
    {
        // Arrange: Create and apply a filter with many properties
        var filterName = "detail-filter";
        Runner.CreateDirectory("IncidentFilters");
        Runner.CreateFile($"IncidentFilters/{filterName}.yaml",
            TestYamlHelper.GetIcmIncidentFilterV2(
                filterName,
                handlingAgent: "DetailAgent",
                priority: "P1",
                monitorId: "monitor-xyz"));
        await Runner.RunAsync("incident-filter", "apply", "--name", filterName);

        // Act: Get filters with detail flag
        var result = await Runner.RunAsync("incident-filter", "get", "--detail");

        // Assert: Should show full YAML output
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("api_version", result.Output);
        Assert.Contains("kind: IncidentFilter", result.Output);
        Assert.Contains(filterName, result.Output);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Get")]
    public async Task IncidentFilterGet_ShowsPlatformAndStatus()
    {
        // Arrange: Create and apply filters with different platforms and statuses
        Runner.CreateDirectory("IncidentFilters");

        var enabledFilter = "enabled-filter";
        Runner.CreateFile($"IncidentFilters/{enabledFilter}.yaml",
            TestYamlHelper.GetIcmIncidentFilterV2(enabledFilter, isEnabled: true));
        await Runner.RunAsync("incident-filter", "apply", "--name", enabledFilter);

        var disabledFilter = "disabled-filter";
        Runner.CreateFile($"IncidentFilters/{disabledFilter}.yaml",
            TestYamlHelper.GetAzMonitorIncidentFilterV2(disabledFilter, isEnabled: false));
        await Runner.RunAsync("incident-filter", "apply", "--name", disabledFilter);

        // Act: Get all filters
        var result = await Runner.RunAsync("incident-filter", "get");

        // Assert: Should show platform and enabled/disabled status
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("IcM", result.Output);
        Assert.Contains("AzMonitor", result.Output);
        Assert.Contains("enabled", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("disabled", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
