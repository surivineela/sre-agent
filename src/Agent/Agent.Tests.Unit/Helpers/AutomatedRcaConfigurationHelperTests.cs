// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Xunit;

namespace Agent.Tests.Unit.Helpers;

public class AutomatedRcaConfigurationHelperTests
{
    [Fact]
    public void ResolveResultTag_ReturnsOverride_WhenConfigured()
    {
        var settings = new AutomatedRCASettings
        {
            DefaultResultTag = "DefaultTag",
            ResultTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["scale_controller_preflight_agent"] = "ScaleTag"
            }
        };

        var resolved = AutomatedRcaConfigurationHelper.ResolveResultTag(settings, "Scale_Controller_Preflight_Agent");

        Assert.Equal("ScaleTag", resolved);
    }

    [Fact]
    public void ResolveResultTag_FallsBackToDefault_ThenLegacy()
    {
        var settingsWithDefault = new AutomatedRCASettings
        {
            DefaultResultTag = "ConfiguredDefault"
        };

        var resolvedDefault = AutomatedRcaConfigurationHelper.ResolveResultTag(settingsWithDefault, "unknown_agent");
        Assert.Equal("ConfiguredDefault", resolvedDefault);

        var settingsWithoutDefaults = new AutomatedRCASettings
        {
            DefaultResultTag = string.Empty
        };

        var resolvedLegacy = AutomatedRcaConfigurationHelper.ResolveResultTag(settingsWithoutDefaults, null);
        Assert.Equal("RCAPreflightProcessed", resolvedLegacy);

        var resolvedNullSettings = AutomatedRcaConfigurationHelper.ResolveResultTag(null, "anything");
        Assert.Equal("RCAPreflightProcessed", resolvedNullSettings);
    }

    [Fact]
    public void BuildThreadLink_ReturnsLocalPath_WhenBaseUrlMissing()
    {
        var settings = new AutomatedRCASettings
        {
            WebBaseUrl = string.Empty,
            AccessNote = "CustomNote"
        };

        var result = AutomatedRcaConfigurationHelper.BuildThreadLink(settings, Guid.Parse("7E44A8B5-2C28-4B66-98A1-CE074533BA32"));

        Assert.True(result.IsLocal);
        Assert.Equal("/static/#/views/activities/threads/7e44a8b5-2c28-4b66-98a1-ce074533ba32", result.Link);
        Assert.Equal(string.Empty, result.AccessNote);
    }

    [Theory]
    [InlineData("https://portal.example.com/", "https://portal.example.com/sreDeepLink/views%2Factivities%2Fthreads%2Fcb252b9b-6f73-4b68-a7da-5b111d3cd808")]
    [InlineData("https://portal.example.com", "https://portal.example.com/sreDeepLink/views%2Factivities%2Fthreads%2Fcb252b9b-6f73-4b68-a7da-5b111d3cd808")]
    public void BuildThreadLink_ReturnsAbsoluteLink_ForRemoteBaseUrl(string baseUrl, string expected)
    {
        var settings = new AutomatedRCASettings
        {
            WebBaseUrl = baseUrl,
            AccessNote = "\nCustom note"
        };

        var result = AutomatedRcaConfigurationHelper.BuildThreadLink(settings, Guid.Parse("CB252B9B-6F73-4B68-A7DA-5B111D3CD808"));

        Assert.False(result.IsLocal);
        Assert.Equal(expected, result.Link);
        Assert.Equal("\nCustom note", result.AccessNote);
    }

    [Fact]
    public void BuildThreadLink_UsesDefaultNote_WhenNotProvided()
    {
        var settings = new AutomatedRCASettings
        {
            WebBaseUrl = "https://portal.example.com"
        };

        var result = AutomatedRcaConfigurationHelper.BuildThreadLink(settings, Guid.NewGuid());

        Assert.False(result.IsLocal);
        Assert.Equal(settings.AccessNote, result.AccessNote);
        Assert.StartsWith("https://portal.example.com", result.Link);
    }
}
