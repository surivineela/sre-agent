// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;
using Xunit;

namespace Agent.Cli.UnitTests.Models;

public class IncidentFilterV2Tests
{
    #region ParseYaml Tests

    [Fact]
    public void ParseYaml_WithValidMinimalYaml_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: IncidentFilter
metadata:
  name: test-filter
spec:
  incidentPlatform: IcM
  isEnabled: true
";

        // Act
        var filter = IncidentFilterV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(filter);
        Assert.Equal("azuresre.ai/v2", filter.ApiVersion);
        Assert.Equal("IncidentFilter", filter.Kind);
        Assert.NotNull(filter.Metadata);
        Assert.Equal("test-filter", filter.Metadata.Name);
        Assert.NotNull(filter.Spec);
        Assert.Equal("IcM", filter.Spec.IncidentPlatform);
        Assert.True(filter.Spec.IsEnabled);
    }

    [Fact]
    public void ParseYaml_WithAllCommonProperties_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: IncidentFilter
metadata:
  name: full-filter
spec:
  incidentPlatform: IcM
  impactedService: MyService
  priority: P1
  incidentType: LiveSite
  alertId: alert-123
  titleContains: critical error
  agentMode: auto
  handlingAgent: DefaultAgent
  owningTeamId: team-123
  maxAutomatedInvestigationAttempts: 5
  deepInvestigationEnabled: true
  isEnabled: true
";

        // Act
        var filter = IncidentFilterV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(filter);
        Assert.Equal("azuresre.ai/v2", filter.ApiVersion);
        Assert.Equal("IncidentFilter", filter.Kind);
        Assert.NotNull(filter.Metadata);
        Assert.Equal("full-filter", filter.Metadata.Name);

        Assert.NotNull(filter.Spec);
        Assert.Equal("IcM", filter.Spec.IncidentPlatform);
        Assert.Equal("MyService", filter.Spec.ImpactedService);
        Assert.Equal("P1", filter.Spec.Priority);
        Assert.Equal("LiveSite", filter.Spec.IncidentType);
        Assert.Equal("alert-123", filter.Spec.AlertId);
        Assert.Equal("critical error", filter.Spec.TitleContains);
        Assert.Equal("auto", filter.Spec.AgentMode);
        Assert.Equal("DefaultAgent", filter.Spec.HandlingAgent);
        Assert.Equal("team-123", filter.Spec.OwningTeamId);
        Assert.Equal(5, filter.Spec.MaxAutomatedInvestigationAttempts);
        Assert.True(filter.Spec.DeepInvestigationEnabled);
        Assert.True(filter.Spec.IsEnabled);
    }

    [Fact]
    public void ParseYaml_WithIcmFilterSettings_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: IncidentFilter
metadata:
  name: icm-filter
spec:
  incidentPlatform: IcM
  handlingAgent: IcmAgent
  isEnabled: true
  icmFilterSettings:
    monitorId: monitor-123
    createdBy: system@contoso.com
";

        // Act
        var filter = IncidentFilterV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(filter);
        Assert.Equal("icm-filter", filter.Metadata.Name);
        Assert.Equal("IcM", filter.Spec.IncidentPlatform);
        Assert.Equal("IcmAgent", filter.Spec.HandlingAgent);
        Assert.NotNull(filter.Spec.IcmFilterSettings);
        Assert.Equal("monitor-123", filter.Spec.IcmFilterSettings.MonitorId);
        Assert.Equal("system@contoso.com", filter.Spec.IcmFilterSettings.CreatedBy);
        Assert.Null(filter.Spec.AzMonitorFilterSettings);
    }

    [Fact]
    public void ParseYaml_WithAzMonitorFilterSettings_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: IncidentFilter
metadata:
  name: azmonitor-filter
spec:
  incidentPlatform: AzMonitor
  priority: P1
  isEnabled: true
  azMonitorFilterSettings:
    targetResourceType: Microsoft.Compute/virtualMachines
    targetResource: /subscriptions/xxx/resourceGroups/myRG/providers/Microsoft.Compute/virtualMachines/myVM
";

        // Act
        var filter = IncidentFilterV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(filter);
        Assert.Equal("azmonitor-filter", filter.Metadata.Name);
        Assert.Equal("AzMonitor", filter.Spec.IncidentPlatform);
        Assert.Equal("P1", filter.Spec.Priority);
        Assert.NotNull(filter.Spec.AzMonitorFilterSettings);
        Assert.Equal("Microsoft.Compute/virtualMachines", filter.Spec.AzMonitorFilterSettings.TargetResourceType);
        Assert.Equal("/subscriptions/xxx/resourceGroups/myRG/providers/Microsoft.Compute/virtualMachines/myVM", filter.Spec.AzMonitorFilterSettings.TargetResource);
        Assert.Null(filter.Spec.IcmFilterSettings);
    }

    [Fact]
    public void ParseYaml_WithOptionalFieldsOmitted_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: IncidentFilter
metadata:
  name: simple-filter
spec:
  incidentPlatform: PagerDuty
";

        // Act
        var filter = IncidentFilterV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(filter);
        Assert.Equal("azuresre.ai/v2", filter.ApiVersion);
        Assert.Equal("IncidentFilter", filter.Kind);
        Assert.NotNull(filter.Metadata);
        Assert.Equal("simple-filter", filter.Metadata.Name);
        Assert.Null(filter.Metadata.Owner);
        Assert.NotNull(filter.Spec);
        Assert.Equal("PagerDuty", filter.Spec.IncidentPlatform);
        Assert.Null(filter.Spec.ImpactedService);
        Assert.Null(filter.Spec.Priority);
        Assert.Null(filter.Spec.HandlingAgent);
        Assert.Null(filter.Spec.IsEnabled);
        Assert.Null(filter.Spec.IcmFilterSettings);
        Assert.Null(filter.Spec.AzMonitorFilterSettings);
    }

    [Fact]
    public void ParseYaml_WithDisabledFilter_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: IncidentFilter
metadata:
  name: disabled-filter
spec:
  incidentPlatform: IcM
  isEnabled: false
";

        // Act
        var filter = IncidentFilterV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(filter);
        Assert.Equal("disabled-filter", filter.Metadata.Name);
        Assert.False(filter.Spec.IsEnabled);
    }

    #endregion

    #region ToYaml Tests

    [Fact]
    public void ToYaml_WithMinimalProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var filter = new IncidentFilterV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-filter"
            },
            Spec = new IncidentFilterSpecV2
            {
                IncidentPlatform = "IcM",
                IsEnabled = true
            }
        };

        // Act
        var yaml = filter.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("api_version: azuresre.ai/v2", yaml);
        Assert.Contains("kind: IncidentFilter", yaml);
        Assert.Contains("name: test-filter", yaml);
        Assert.Contains("incidentPlatform: IcM", yaml);
        Assert.Contains("isEnabled: true", yaml);
    }

    [Fact]
    public void ToYaml_WithAllCommonProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var filter = new IncidentFilterV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "full-filter"
            },
            Spec = new IncidentFilterSpecV2
            {
                IncidentPlatform = "IcM",
                ImpactedService = "MyService",
                Priority = "P1",
                IncidentType = "LiveSite",
                AlertId = "alert-123",
                TitleContains = "critical error",
                AgentMode = "auto",
                HandlingAgent = "DefaultAgent",
                OwningTeamId = "team-123",
                MaxAutomatedInvestigationAttempts = 5,
                DeepInvestigationEnabled = true,
                IsEnabled = true
            }
        };

        // Act
        var yaml = filter.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("api_version: azuresre.ai/v2", yaml);
        Assert.Contains("kind: IncidentFilter", yaml);
        Assert.Contains("name: full-filter", yaml);
        Assert.Contains("incidentPlatform: IcM", yaml);
        Assert.Contains("impactedService: MyService", yaml);
        Assert.Contains("priority: P1", yaml);
        Assert.Contains("incidentType: LiveSite", yaml);
        Assert.Contains("alertId: alert-123", yaml);
        Assert.Contains("titleContains: critical error", yaml);
        Assert.Contains("agentMode: auto", yaml);
        Assert.Contains("handlingAgent: DefaultAgent", yaml);
        Assert.Contains("owningTeamId: team-123", yaml);
        Assert.Contains("maxAutomatedInvestigationAttempts: 5", yaml);
        Assert.Contains("deepInvestigationEnabled: true", yaml);
        Assert.Contains("isEnabled: true", yaml);
    }

    [Fact]
    public void ToYaml_WithIcmFilterSettings_ShouldSerializeCorrectly()
    {
        // Arrange
        var filter = new IncidentFilterV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "icm-filter"
            },
            Spec = new IncidentFilterSpecV2
            {
                IncidentPlatform = "IcM",
                HandlingAgent = "IcmAgent",
                IsEnabled = true,
                IcmFilterSettings = new IcmFilterSettingsV2
                {
                    MonitorId = "monitor-123",
                    CreatedBy = "system@contoso.com"
                }
            }
        };

        // Act
        var yaml = filter.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("incidentPlatform: IcM", yaml);
        Assert.Contains("icmFilterSettings:", yaml);
        Assert.Contains("monitorId: monitor-123", yaml);
        Assert.Contains("createdBy: system@contoso.com", yaml);
    }

    [Fact]
    public void ToYaml_WithAzMonitorFilterSettings_ShouldSerializeCorrectly()
    {
        // Arrange
        var filter = new IncidentFilterV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "azmonitor-filter"
            },
            Spec = new IncidentFilterSpecV2
            {
                IncidentPlatform = "AzMonitor",
                Priority = "P1",
                IsEnabled = true,
                AzMonitorFilterSettings = new AzMonitorFilterSettingsV2
                {
                    TargetResourceType = "Microsoft.Compute/virtualMachines",
                    TargetResource = "/subscriptions/xxx/resourceGroups/myRG"
                }
            }
        };

        // Act
        var yaml = filter.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("incidentPlatform: AzMonitor", yaml);
        Assert.Contains("azMonitorFilterSettings:", yaml);
        Assert.Contains("targetResourceType: Microsoft.Compute/virtualMachines", yaml);
        Assert.Contains("targetResource: /subscriptions/xxx/resourceGroups/myRG", yaml);
    }

    [Fact]
    public void ToYaml_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalFilter = new IncidentFilterV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-filter"
            },
            Spec = new IncidentFilterSpecV2
            {
                IncidentPlatform = "IcM",
                ImpactedService = "MyService",
                Priority = "P1",
                HandlingAgent = "DefaultAgent",
                MaxAutomatedInvestigationAttempts = 5,
                DeepInvestigationEnabled = true,
                IsEnabled = true,
                IcmFilterSettings = new IcmFilterSettingsV2
                {
                    MonitorId = "monitor-123",
                    CreatedBy = "system@contoso.com"
                }
            }
        };

        // Act
        var yaml = originalFilter.ToYaml();
        var deserializedFilter = IncidentFilterV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedFilter);
        Assert.Equal(originalFilter.ApiVersion, deserializedFilter.ApiVersion);
        Assert.Equal(originalFilter.Kind, deserializedFilter.Kind);
        Assert.Equal(originalFilter.Metadata.Name, deserializedFilter.Metadata.Name);
        Assert.Equal(originalFilter.Spec.IncidentPlatform, deserializedFilter.Spec.IncidentPlatform);
        Assert.Equal(originalFilter.Spec.ImpactedService, deserializedFilter.Spec.ImpactedService);
        Assert.Equal(originalFilter.Spec.Priority, deserializedFilter.Spec.Priority);
        Assert.Equal(originalFilter.Spec.HandlingAgent, deserializedFilter.Spec.HandlingAgent);
        Assert.Equal(originalFilter.Spec.MaxAutomatedInvestigationAttempts, deserializedFilter.Spec.MaxAutomatedInvestigationAttempts);
        Assert.Equal(originalFilter.Spec.DeepInvestigationEnabled, deserializedFilter.Spec.DeepInvestigationEnabled);
        Assert.Equal(originalFilter.Spec.IsEnabled, deserializedFilter.Spec.IsEnabled);
        Assert.NotNull(deserializedFilter.Spec.IcmFilterSettings);
        Assert.Equal(originalFilter.Spec.IcmFilterSettings!.MonitorId, deserializedFilter.Spec.IcmFilterSettings.MonitorId);
        Assert.Equal(originalFilter.Spec.IcmFilterSettings.CreatedBy, deserializedFilter.Spec.IcmFilterSettings.CreatedBy);
    }

    [Fact]
    public void ToYaml_RoundTrip_WithAzMonitorSettings_ShouldPreserveData()
    {
        // Arrange
        var originalFilter = new IncidentFilterV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "azmonitor-test"
            },
            Spec = new IncidentFilterSpecV2
            {
                IncidentPlatform = "AzMonitor",
                Priority = "P2",
                IsEnabled = false,
                AzMonitorFilterSettings = new AzMonitorFilterSettingsV2
                {
                    TargetResourceType = "Microsoft.Storage/storageAccounts",
                    TargetResource = "/subscriptions/abc/resourceGroups/rg1"
                }
            }
        };

        // Act
        var yaml = originalFilter.ToYaml();
        var deserializedFilter = IncidentFilterV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedFilter);
        Assert.Equal(originalFilter.Spec.IncidentPlatform, deserializedFilter.Spec.IncidentPlatform);
        Assert.Equal(originalFilter.Spec.Priority, deserializedFilter.Spec.Priority);
        Assert.Equal(originalFilter.Spec.IsEnabled, deserializedFilter.Spec.IsEnabled);
        Assert.NotNull(deserializedFilter.Spec.AzMonitorFilterSettings);
        Assert.Equal(originalFilter.Spec.AzMonitorFilterSettings!.TargetResourceType, deserializedFilter.Spec.AzMonitorFilterSettings.TargetResourceType);
        Assert.Equal(originalFilter.Spec.AzMonitorFilterSettings.TargetResource, deserializedFilter.Spec.AzMonitorFilterSettings.TargetResource);
    }

    #endregion

    #region Normalize Tests

    [Fact]
    public void Normalize_WithTitleContainsTrailingWhitespace_ShouldTrimCorrectly()
    {
        // Arrange
        var filter = new IncidentFilterV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-filter"
            },
            Spec = new IncidentFilterSpecV2
            {
                IncidentPlatform = "IcM",
                TitleContains = "critical error   \n  with trailing spaces  "
            }
        };

        // Act
        filter.Normalize();

        // Assert - trailing whitespace should be removed from each line
        Assert.NotNull(filter.Spec.TitleContains);
        Assert.DoesNotContain("   \n", filter.Spec.TitleContains);
    }

    [Fact]
    public void Normalize_WithNullTitleContains_ShouldNotThrow()
    {
        // Arrange
        var filter = new IncidentFilterV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-filter"
            },
            Spec = new IncidentFilterSpecV2
            {
                IncidentPlatform = "IcM",
                TitleContains = null
            }
        };

        // Act & Assert - should not throw
        var exception = Record.Exception(() => filter.Normalize());
        Assert.Null(exception);
    }

    #endregion

    #region Platform Type Tests

    [Theory]
    [InlineData("IcM")]
    [InlineData("AzMonitor")]
    [InlineData("PagerDuty")]
    [InlineData("ServiceNow")]
    public void ParseYaml_WithDifferentPlatforms_ShouldDeserializeCorrectly(string platform)
    {
        // Arrange
        var yaml = $@"
api_version: azuresre.ai/v2
kind: IncidentFilter
metadata:
  name: {platform.ToLower()}-filter
spec:
  incidentPlatform: {platform}
  isEnabled: true
";

        // Act
        var filter = IncidentFilterV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(filter);
        Assert.Equal(platform, filter.Spec.IncidentPlatform);
    }

    #endregion

    #region Default Value Tests

    [Fact]
    public void NewIncidentFilterV2_ShouldHaveCorrectDefaults()
    {
        // Act
        var filter = new IncidentFilterV2();

        // Assert
        Assert.Equal("azuresre.ai/v2", filter.ApiVersion);
        Assert.Equal("IncidentFilter", filter.Kind);
        Assert.NotNull(filter.Metadata);
        Assert.NotNull(filter.Spec);
    }

    [Fact]
    public void NewIncidentFilterSpecV2_ShouldHaveNullDefaults()
    {
        // Act
        var spec = new IncidentFilterSpecV2();

        // Assert
        Assert.Null(spec.IncidentPlatform);
        Assert.Null(spec.ImpactedService);
        Assert.Null(spec.Priority);
        Assert.Null(spec.IncidentType);
        Assert.Null(spec.AlertId);
        Assert.Null(spec.TitleContains);
        Assert.Null(spec.AgentMode);
        Assert.Null(spec.HandlingAgent);
        Assert.Null(spec.OwningTeamId);
        Assert.Null(spec.MaxAutomatedInvestigationAttempts);
        Assert.Null(spec.DeepInvestigationEnabled);
        Assert.Null(spec.IsEnabled);
        Assert.Null(spec.IcmFilterSettings);
        Assert.Null(spec.AzMonitorFilterSettings);
    }

    [Fact]
    public void NewIcmFilterSettingsV2_ShouldHaveNullDefaults()
    {
        // Act
        var settings = new IcmFilterSettingsV2();

        // Assert
        Assert.Null(settings.MonitorId);
        Assert.Null(settings.CreatedBy);
    }

    [Fact]
    public void NewAzMonitorFilterSettingsV2_ShouldHaveNullDefaults()
    {
        // Act
        var settings = new AzMonitorFilterSettingsV2();

        // Assert
        Assert.Null(settings.TargetResourceType);
        Assert.Null(settings.TargetResource);
    }

    #endregion

    #region Normalize Platform Tests

    [Fact]
    public void Normalize_WithLowercasePlatform_ShouldNormalizeToCanonicalSpelling()
    {
        // Arrange
        var filter = new IncidentFilterV2
        {
            Metadata = new ResourceMetadataModel { Name = "test" },
            Spec = new IncidentFilterSpecV2 { IncidentPlatform = "icm" }
        };

        // Act
        filter.Normalize();

        // Assert
        Assert.Equal("IcM", filter.Spec.IncidentPlatform);
    }

    [Fact]
    public void Normalize_WithMixedCasePlatform_ShouldNormalizeToCanonicalSpelling()
    {
        // Arrange
        var filter = new IncidentFilterV2
        {
            Metadata = new ResourceMetadataModel { Name = "test" },
            Spec = new IncidentFilterSpecV2 { IncidentPlatform = "pagerDUTY" }
        };

        // Act
        filter.Normalize();

        // Assert - case-insensitive lookup normalizes to canonical spelling
        Assert.Equal("PagerDuty", filter.Spec.IncidentPlatform);
    }

    #endregion
}
