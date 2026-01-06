// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Mcp.Tools;
using Shouldly;
using Xunit;

namespace Agent.Cli.UnitTests;

/// <summary>
/// Tests for the PlatformToolsProvider which loads platform tool names
/// from PublishedTools.json and ICM plugin definitions.
/// </summary>
public class PlatformToolsProviderTests
{
    [Fact]
    public void GetPlatformTools_ShouldReturnNonEmptySet()
    {
        // Act
        var tools = PlatformToolsProvider.GetPlatformTools();

        // Assert
        tools.ShouldNotBeNull();
        tools.Count.ShouldBeGreaterThan(0, "Should have at least ICM tools");
    }

    [Fact]
    public void GetPlatformTools_ShouldContainIcmTools()
    {
        // Act
        var tools = PlatformToolsProvider.GetPlatformTools();

        // Assert - should have ICM tools which are always available
        tools.ShouldContain("GetIncidentInfo");
        tools.ShouldContain("GetCustomFields");
        tools.ShouldContain("SearchIncidents");
        tools.ShouldContain("MitigateIncident");
        tools.ShouldContain("ResolveIncident");
        tools.ShouldContain("PostDiscussionEntry");
        tools.ShouldContain("AcknowledgeIncident");
    }

    [Theory]
    [InlineData("GetIncidentInfo")]
    [InlineData("getincidentinfo")]  // Case insensitive
    [InlineData("GETINCIDENTINFO")]  // Case insensitive
    public void IsPlatformTool_ShouldBeCaseInsensitive(string toolName)
    {
        // Act & Assert
        PlatformToolsProvider.IsPlatformTool(toolName).ShouldBeTrue();
    }

    [Fact]
    public void IsPlatformTool_ShouldReturnFalseForUnknownTool()
    {
        // Act & Assert
        PlatformToolsProvider.IsPlatformTool("SomeUnknownCustomTool").ShouldBeFalse();
        PlatformToolsProvider.IsPlatformTool("MyCustomKustoTool").ShouldBeFalse();
    }

    [Fact]
    public void GetPlatformTools_ShouldBeCached()
    {
        // Act
        var tools1 = PlatformToolsProvider.GetPlatformTools();
        var tools2 = PlatformToolsProvider.GetPlatformTools();

        // Assert - should be the same instance (cached)
        ReferenceEquals(tools1, tools2).ShouldBeTrue("Tools set should be cached");
    }
}
