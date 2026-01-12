// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Cli.Tests.E2E.IncidentFilter;

/// <summary>
/// E2E tests for 'srectl incident-filter create' command.
/// Tests the creation of incident filter YAML files locally.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class CreateCommandTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public CreateCommandTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    public async Task IncidentFilterCreate_IcmPlatform_CreatesYamlFile()
    {
        // Arrange
        var filterName = "test-icm-filter";

        // Act
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", filterName,
            "--platform", "IcM",
            "--handling-agent", "DefaultAgent"
        );

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}, Error: {result.StandardError}");

        // Verify the YAML file was created
        var expectedPath = $"IncidentFilters/{filterName}.yaml";
        Assert.True(Runner.FileExists(expectedPath), $"YAML file should exist at {expectedPath}");

        // Verify the YAML content
        var yamlContent = Runner.ReadFile(expectedPath);
        _output.WriteLine("=== YAML Content ===");
        _output.WriteLine(yamlContent);
        _output.WriteLine("====================");

        // Parse and validate YAML structure
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        // Validate required fields
        Assert.True(yamlDict.ContainsKey("api_version"), "YAML should contain api_version");
        Assert.True(yamlDict.ContainsKey("kind"), "YAML should contain kind");
        Assert.Equal("IncidentFilter", yamlDict["kind"].ToString());

        // Validate metadata
        Assert.True(yamlDict.ContainsKey("metadata"), "YAML should contain metadata");
        var metadata = yamlDict["metadata"] as Dictionary<object, object>;
        Assert.NotNull(metadata);
        Assert.Equal(filterName, metadata["name"].ToString());

        // Validate spec
        Assert.True(yamlDict.ContainsKey("spec"), "YAML should contain spec");
        var spec = yamlDict["spec"] as Dictionary<object, object>;
        Assert.NotNull(spec);
        Assert.Equal("IcM", spec["incidentPlatform"].ToString());
        Assert.Equal("DefaultAgent", spec["handlingAgent"].ToString());

        // Verify success message in output
        Assert.Contains("created", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    public async Task IncidentFilterCreate_AzMonitorPlatform_CreatesYamlFile()
    {
        // Arrange
        var filterName = "test-azmonitor-filter";

        // Act
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", filterName,
            "--platform", "AzMonitor",
            "--priority", "P1",
            "--target-resource-type", "Microsoft.Compute/virtualMachines"
        );

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}, Error: {result.StandardError}");

        // Verify the YAML file was created
        var expectedPath = $"IncidentFilters/{filterName}.yaml";
        Assert.True(Runner.FileExists(expectedPath), $"YAML file should exist at {expectedPath}");

        // Verify the YAML content
        var yamlContent = Runner.ReadFile(expectedPath);
        _output.WriteLine("=== YAML Content ===");
        _output.WriteLine(yamlContent);
        _output.WriteLine("====================");

        // Validate content contains expected values
        Assert.Contains("incidentPlatform: AzMonitor", yamlContent);
        Assert.Contains("priority: P1", yamlContent);
        Assert.Contains("azMonitorFilterSettings:", yamlContent);
        Assert.Contains("targetResourceType: Microsoft.Compute/virtualMachines", yamlContent);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    public async Task IncidentFilterCreate_WithIcmSpecificOptions_IncludesIcmSettings()
    {
        // Arrange
        var filterName = "icm-specific-filter";

        // Act
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", filterName,
            "--platform", "IcM",
            "--monitor-id", "monitor-123",
            "--created-by", "system@contoso.com"
        );

        // Assert
        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}");

        var expectedPath = $"IncidentFilters/{filterName}.yaml";
        var yamlContent = Runner.ReadFile(expectedPath);
        _output.WriteLine(yamlContent);

        // Validate IcM-specific settings
        Assert.Contains("icmFilterSettings:", yamlContent);
        Assert.Contains("monitorId: monitor-123", yamlContent);
        Assert.Contains("createdBy: system@contoso.com", yamlContent);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    public async Task IncidentFilterCreate_WithAllCommonOptions_IncludesAllFields()
    {
        // Arrange
        var filterName = "full-options-filter";

        // Act
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", filterName,
            "--platform", "IcM",
            "--handling-agent", "MyAgent",
            "--impacted-service", "MyService",
            "--priority", "P2",
            "--incident-type", "LiveSite",
            "--alert-id", "alert-456",
            "--title-contains", "critical error",
            "--agent-mode", "auto",
            "--owning-team-id", "team-123",
            "--max-investigation-attempts", "5",
            "--deep-investigation"
        );

        // Assert
        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}");

        var expectedPath = $"IncidentFilters/{filterName}.yaml";
        var yamlContent = Runner.ReadFile(expectedPath);
        _output.WriteLine(yamlContent);

        // Validate all common fields
        Assert.Contains("handlingAgent: MyAgent", yamlContent);
        Assert.Contains("impactedService: MyService", yamlContent);
        Assert.Contains("priority: P2", yamlContent);
        Assert.Contains("incidentType: LiveSite", yamlContent);
        Assert.Contains("alertId: alert-456", yamlContent);
        Assert.Contains("titleContains: critical error", yamlContent);
        Assert.Contains("agentMode: auto", yamlContent);
        Assert.Contains("owningTeamId: team-123", yamlContent);
        Assert.Contains("maxAutomatedInvestigationAttempts: 5", yamlContent);
        Assert.Contains("deepInvestigationEnabled: true", yamlContent);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    public async Task IncidentFilterCreate_WithDisabledFlag_SetsIsEnabledFalse()
    {
        // Arrange
        var filterName = "disabled-filter";

        // Act
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", filterName,
            "--platform", "IcM",
            "--disabled"
        );

        // Assert
        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}");

        var expectedPath = $"IncidentFilters/{filterName}.yaml";
        var yamlContent = Runner.ReadFile(expectedPath);
        _output.WriteLine(yamlContent);

        Assert.Contains("isEnabled: false", yamlContent);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    public async Task IncidentFilterCreate_PagerDutyPlatform_CreatesYamlFile()
    {
        // Arrange
        var filterName = "pagerduty-filter";

        // Act
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", filterName,
            "--platform", "PagerDuty"
        );

        // Assert
        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}");

        var expectedPath = $"IncidentFilters/{filterName}.yaml";
        Assert.True(Runner.FileExists(expectedPath));

        var yamlContent = Runner.ReadFile(expectedPath);
        Assert.Contains("incidentPlatform: PagerDuty", yamlContent);
    }

    [Fact]
    [Trait("Category", "IncidentFilter")]
    [Trait("Command", "Create")]
    public async Task IncidentFilterCreate_ServiceNowPlatform_CreatesYamlFile()
    {
        // Arrange
        var filterName = "servicenow-filter";

        // Act
        var result = await Runner.RunAsync(
            "incident-filter", "create",
            "--name", filterName,
            "--platform", "ServiceNow"
        );

        // Assert
        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}");

        var expectedPath = $"IncidentFilters/{filterName}.yaml";
        Assert.True(Runner.FileExists(expectedPath));

        var yamlContent = Runner.ReadFile(expectedPath);
        Assert.Contains("incidentPlatform: ServiceNow", yamlContent);
    }
}
