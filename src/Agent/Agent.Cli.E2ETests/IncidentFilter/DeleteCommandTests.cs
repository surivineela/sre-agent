// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.IncidentFilter;

/// <summary>
/// E2E tests for 'srectl incident-filter delete' command with mock backend.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class DeleteCommandTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public DeleteCommandTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Delete")]
    public async Task IncidentFilterDelete_DeletesFilterFromServer()
    {
        // Arrange: Create and apply a filter
        var filterName = "delete-test-filter";
        Runner.CreateDirectory("IncidentFilters");
        Runner.CreateFile($"IncidentFilters/{filterName}.yaml",
            TestYamlHelper.GetIcmIncidentFilterV2(filterName));
        await Runner.RunAsync("incident-filter", "apply", "--name", filterName);

        // Verify filter exists on server
        var verifyResult = await Runner.RunAsync("incident-filter", "get", "--name", filterName);
        Assert.True(verifyResult.Success, "Filter should exist before deletion");

        // Inject "n" response for local file cleanup prompt
        Agent.Cli.Helpers.ConsoleUI.ReadLineHandler = () => "n";

        // Act: Delete the filter
        var result = await Runner.RunAsync("incident-filter", "delete", "--name", filterName);

        // Assert: Command should succeed
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("deleted", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify filter no longer exists on server
        var listResult = await Runner.RunAsync("incident-filter", "get", "--name", filterName);
        Assert.False(listResult.Success);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Delete")]
    public async Task IncidentFilterDelete_DryRun_DoesNotDeleteFilter()
    {
        // Arrange: Create and apply a filter
        var filterName = "dry-run-delete-filter";
        Runner.CreateDirectory("IncidentFilters");
        Runner.CreateFile($"IncidentFilters/{filterName}.yaml",
            TestYamlHelper.GetIcmIncidentFilterV2(filterName));
        await Runner.RunAsync("incident-filter", "apply", "--name", filterName);

        // Act: Delete with dry-run
        var result = await Runner.RunAsync("incident-filter", "delete", "--name", filterName, "--dry-run");

        // Assert: Command should succeed
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");

        // Verify filter still exists on server (dry run should not delete)
        var listResult = await Runner.RunAsync("incident-filter", "get", "--name", filterName);
        Assert.True(listResult.Success);
        Assert.Contains(filterName, listResult.StandardOutput);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Delete")]
    public async Task IncidentFilterDelete_NonExistentFilter_Succeeds()
    {
        // Act: Delete a filter that doesn't exist
        var result = await Runner.RunAsync("incident-filter", "delete", "--name", "non-existent-filter");

        // Assert: Command should succeed (idempotent deletion)
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Delete")]
    public async Task IncidentFilterDelete_WithLocalFileCleanup_DeletesLocalFiles()
    {
        // Arrange: Create and apply a filter
        var filterName = "cleanup-test-filter";
        Runner.CreateDirectory("IncidentFilters");
        var filterPath = $"IncidentFilters/{filterName}.yaml";
        Runner.CreateFile(filterPath, TestYamlHelper.GetIcmIncidentFilterV2(filterName));
        await Runner.RunAsync("incident-filter", "apply", "--name", filterName);

        // Verify local file exists
        Assert.True(Runner.FileExists(filterPath), "Local file should exist before deletion");

        // Inject "y" response for local file cleanup prompt
        Agent.Cli.Helpers.ConsoleUI.ReadLineHandler = () => "y";

        // Act: Delete the filter and confirm local cleanup
        var result = await Runner.RunAsync("incident-filter", "delete", "--name", filterName);

        // Assert: Command should succeed and local file should be deleted
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.False(Runner.FileExists(filterPath), "Local file should be deleted after cleanup confirmation");
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Delete")]
    public async Task IncidentFilterDelete_WithoutLocalFileCleanup_PreservesLocalFiles()
    {
        // Arrange: Create and apply a filter
        var filterName = "preserve-test-filter";
        Runner.CreateDirectory("IncidentFilters");
        var filterPath = $"IncidentFilters/{filterName}.yaml";
        Runner.CreateFile(filterPath, TestYamlHelper.GetIcmIncidentFilterV2(filterName));
        await Runner.RunAsync("incident-filter", "apply", "--name", filterName);

        // Inject "n" response for local file cleanup prompt
        Agent.Cli.Helpers.ConsoleUI.ReadLineHandler = () => "n";

        // Act: Delete the filter and decline local cleanup
        var result = await Runner.RunAsync("incident-filter", "delete", "--name", filterName);

        // Assert: Command should succeed and local file should be preserved
        _output.WriteLine(result.Output);
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.True(Runner.FileExists(filterPath), "Local file should be preserved when cleanup is declined");
    }
}
