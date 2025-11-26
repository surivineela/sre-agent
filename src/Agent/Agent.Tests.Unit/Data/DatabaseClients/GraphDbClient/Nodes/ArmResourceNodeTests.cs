// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Shouldly;

namespace Agent.Tests.Unit.Data.DatabaseClients.GraphDbClient.Nodes;

/// <summary>
/// Unit tests for ArmResourceNode location handling and normalization
/// </summary>
public class ArmResourceNodeTests
{
    #region NormalizeLocation Tests

    [Fact]
    public void NormalizeLocation_WithNull_ReturnsEmptyString()
    {
        var result = LocationExtensions.NormalizeLocation(null!);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void NormalizeLocation_WithEmptyString_ReturnsEmptyString()
    {
        var result = LocationExtensions.NormalizeLocation(string.Empty);

        result.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("  \t  ")]
    public void NormalizeLocation_WithWhitespace_ReturnsEmptyString(string whitespace)
    {
        var result = LocationExtensions.NormalizeLocation(whitespace);

        result.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("eastus", "eastus")]
    [InlineData("westus", "westus")]
    [InlineData("northeurope", "northeurope")]
    public void NormalizeLocation_WithNormalizedLocation_ReturnsSameLocation(string input, string expected)
    {
        var result = LocationExtensions.NormalizeLocation(input);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("East US", "eastus")]
    [InlineData("West US", "westus")]
    [InlineData("North Europe", "northeurope")]
    [InlineData("West Europe", "westeurope")]
    public void NormalizeLocation_WithSpaces_RemovesSpacesAndLowerCases(string input, string expected)
    {
        var result = LocationExtensions.NormalizeLocation(input);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("East-US", "eastus")]
    [InlineData("West_US", "westus")]
    [InlineData("North.Europe", "northeurope")]
    public void NormalizeLocation_WithSpecialCharacters_RemovesSpecialCharactersAndLowerCases(string input, string expected)
    {
        var result = LocationExtensions.NormalizeLocation(input);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("EASTUS", "eastus")]
    [InlineData("WestUS", "westus")]
    [InlineData("NorthEurope", "northeurope")]
    public void NormalizeLocation_WithMixedCase_ConvertsToLowerCase(string input, string expected)
    {
        var result = LocationExtensions.NormalizeLocation(input);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("South East Asia", "southeastasia")]
    [InlineData("Central-US", "centralus")]
    [InlineData("UK_West", "ukwest")]
    [InlineData("South.Central.US", "southcentralus")]
    [InlineData("North Central US (Stage)", "northcentralusstage")]
    public void NormalizeLocation_WithComplexFormats_NormalizesCorrectly(string input, string expected)
    {
        var result = LocationExtensions.NormalizeLocation(input);

        result.ShouldBe(expected);
    }

    #endregion

    #region ArmResourceNode Constructor Tests

    [Fact]
    public void Constructor_WithNullLocation_SetsLocationToEmptyString()
    {
        var node = new ArmResourceNode(
            resourceType: "Microsoft.Web/sites",
            resourceId: "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app1",
            subscriptionId: "sub1",
            resourceGroupName: "rg1",
            resourceName: "app1",
            location: null);

        node.Location.ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_WithEmptyLocation_SetsLocationToEmptyString()
    {
        var node = new ArmResourceNode(
            resourceType: "Microsoft.Web/sites",
            resourceId: "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app1",
            subscriptionId: "sub1",
            resourceGroupName: "rg1",
            resourceName: "app1",
            location: string.Empty);

        node.Location.ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_WithValidLocation_NormalizesLocation()
    {
        var node = new ArmResourceNode(
            resourceType: "Microsoft.Web/sites",
            resourceId: "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app1",
            subscriptionId: "sub1",
            resourceGroupName: "rg1",
            resourceName: "app1",
            location: "East US");

        node.Location.ShouldBe("eastus");
    }

    [Fact]
    public void Constructor_WithoutLocationParameter_SetsLocationToEmptyString()
    {
        var node = new ArmResourceNode(
            resourceType: "Microsoft.Web/sites",
            resourceId: "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app1",
            subscriptionId: "sub1",
            resourceGroupName: "rg1",
            resourceName: "app1");

        node.Location.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("westus", "westus")]
    [InlineData("West US", "westus")]
    [InlineData("WESTUS", "westus")]
    [InlineData("West-US", "westus")]
    public void Constructor_WithVariousLocationFormats_NormalizesConsistently(string input, string expected)
    {
        var node = new ArmResourceNode(
            resourceType: "Microsoft.Web/sites",
            resourceId: "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app1",
            subscriptionId: "sub1",
            resourceGroupName: "rg1",
            resourceName: "app1",
            location: input);

        node.Location.ShouldBe(expected);
    }

    #endregion

    #region Resource Properties Tests

    [Fact]
    public void Constructor_WithAllParameters_SetsAllPropertiesCorrectly()
    {
        var node = new ArmResourceNode(
            resourceType: "Microsoft.Web/sites",
            resourceId: "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app1",
            subscriptionId: "sub1",
            resourceGroupName: "rg1",
            resourceName: "app1",
            resourceKind: "app",
            remarks: "Test app",
            location: "East US",
            appHealthInfo: null);

        node.ResourceType.ShouldBe("microsoft.web/sites"); // ResourceType is normalized to lowercase
        node.ResourceId.ShouldBe("/subscriptions/sub1/resourcegroups/rg1/providers/microsoft.web/sites/app1"); // ResourceId is normalized to lowercase
        node.SubscriptionId.ShouldBe("sub1");
        node.ResourceGroupName.ShouldBe("rg1");
        node.ResourceName.ShouldBe("app1");
        node.ResourceKind.ShouldBe("webApp"); // ResourceKind is transformed by ResourceKindHelper
        node.Remarks.ShouldBe("Test app");
        node.Location.ShouldBe("eastus");
    }

    #endregion
}
