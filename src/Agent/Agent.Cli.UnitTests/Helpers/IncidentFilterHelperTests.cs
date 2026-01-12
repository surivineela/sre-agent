// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Helpers;
using Xunit;

namespace Agent.Cli.UnitTests.Helpers;

/// <summary>
/// Unit tests for IncidentFilterHelper.
/// </summary>
public class IncidentFilterHelperTests
{
    #region Platform Constants Tests

    [Fact]
    public void Platform_All_ContainsAllPlatforms()
    {
        // Assert
        Assert.Equal(4, IncidentFilterHelper.Platform.All.Length);
        Assert.Contains(IncidentFilterHelper.Platform.IcM, IncidentFilterHelper.Platform.All);
        Assert.Contains(IncidentFilterHelper.Platform.AzMonitor, IncidentFilterHelper.Platform.All);
        Assert.Contains(IncidentFilterHelper.Platform.PagerDuty, IncidentFilterHelper.Platform.All);
        Assert.Contains(IncidentFilterHelper.Platform.ServiceNow, IncidentFilterHelper.Platform.All);
    }

    [Fact]
    public void Platform_Constants_HaveCorrectSpelling()
    {
        Assert.Equal("IcM", IncidentFilterHelper.Platform.IcM);
        Assert.Equal("AzMonitor", IncidentFilterHelper.Platform.AzMonitor);
        Assert.Equal("PagerDuty", IncidentFilterHelper.Platform.PagerDuty);
        Assert.Equal("ServiceNow", IncidentFilterHelper.Platform.ServiceNow);
    }

    #endregion

    #region NormalizePlatform Tests

    [Theory]
    [InlineData("icm", "IcM")]
    [InlineData("ICM", "IcM")]
    [InlineData("IcM", "IcM")]
    [InlineData("Icm", "IcM")]
    [InlineData("azmonitor", "AzMonitor")]
    [InlineData("AZMONITOR", "AzMonitor")]
    [InlineData("AzMonitor", "AzMonitor")]
    [InlineData("pagerduty", "PagerDuty")]
    [InlineData("PAGERDUTY", "PagerDuty")]
    [InlineData("PagerDuty", "PagerDuty")]
    [InlineData("servicenow", "ServiceNow")]
    [InlineData("SERVICENOW", "ServiceNow")]
    [InlineData("ServiceNow", "ServiceNow")]
    public void NormalizePlatform_WithValidPlatform_ReturnsCanonicalSpelling(string input, string expected)
    {
        // Act
        var result = IncidentFilterHelper.NormalizePlatform(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizePlatform_WithNullOrWhitespace_ReturnsOriginal(string? input)
    {
        // Act
        var result = IncidentFilterHelper.NormalizePlatform(input);

        // Assert
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData("UnknownPlatform")]
    [InlineData("CustomPlatform")]
    [InlineData("Other")]
    public void NormalizePlatform_WithUnknownPlatform_ReturnsOriginal(string input)
    {
        // Act
        var result = IncidentFilterHelper.NormalizePlatform(input);

        // Assert
        Assert.Equal(input, result);
    }

    #endregion

    #region IsValidPlatform Tests

    [Theory]
    [InlineData("IcM")]
    [InlineData("icm")]
    [InlineData("ICM")]
    [InlineData("AzMonitor")]
    [InlineData("azmonitor")]
    [InlineData("PagerDuty")]
    [InlineData("pagerduty")]
    [InlineData("ServiceNow")]
    [InlineData("servicenow")]
    public void IsValidPlatform_WithValidPlatform_ReturnsTrue(string platform)
    {
        // Act
        var result = IncidentFilterHelper.IsValidPlatform(platform);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UnknownPlatform")]
    [InlineData("CustomPlatform")]
    public void IsValidPlatform_WithInvalidPlatform_ReturnsFalse(string? platform)
    {
        // Act
        var result = IncidentFilterHelper.IsValidPlatform(platform);

        // Assert
        Assert.False(result);
    }

    #endregion
}
