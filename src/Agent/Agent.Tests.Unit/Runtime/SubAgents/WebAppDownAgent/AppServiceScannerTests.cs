// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.Repositories;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.Metrics;
using Agent.Runtime.SubAgents.WebAppDownAgent;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Agent.Tests.Unit.Runtime.SubAgents.WebAppDownAgent;

/// <summary>
/// Unit tests for AppServiceScanner location handling
/// </summary>
public class AppServiceScannerTests
{
    private readonly Mock<ILogger<AppServiceScanner>> _mockLogger;
    private readonly Mock<ScoreCardService> _mockScoreCardService;
    private readonly Mock<IGraphDatabaseClient> _mockGraphDatabaseClient;

    public AppServiceScannerTests()
    {
        _mockLogger = new Mock<ILogger<AppServiceScanner>>();
        _mockScoreCardService = new Mock<ScoreCardService>(
            Mock.Of<ILogger<ScoreCardService>>(),
            Mock.Of<IGraphDatabaseClient>(),
            new List<IResourceMetricsCollector>(),
            Mock.Of<IAppHealthHistoryRepository>());
        _mockGraphDatabaseClient = new Mock<IGraphDatabaseClient>();
    }

    private AppServiceScanner CreateScanner()
    {
        return new AppServiceScanner(
            _mockScoreCardService.Object,
            _mockGraphDatabaseClient.Object,
            _mockLogger.Object);
    }

    #region CreateArmResourceNodeFromDictionary Tests

    [Fact]
    public void CreateArmResourceNodeFromDictionary_WithValidLocation_CreatesNodeWithLocation()
    {

        var result = new Dictionary<string, object>
        {
            { "id", "vertex-123" },
            { "name", "test-app" },
            { "type", "microsoft.web/sites" },
            { "kind", "app" },
            {
                "properties", new Dictionary<string, object>
                {
                    { "resourceId", new[] { "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/test-app" } },
                    { "subscriptionId", new[] { "sub1" } },
                    { "resourceGroupName", new[] { "rg1" } },
                    { "resourceName", new[] { "test-app" } },
                    { "location", new[] { "westus" } }
                }
            }
        };

        var scanner = CreateScanner();
        var method = typeof(AppServiceScanner).GetMethod("CreateArmResourceNodeFromDictionary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


        var node = method?.Invoke(scanner, [result]) as ArmResourceNode;


        node.ShouldNotBeNull();
        node.Location.ShouldBe("westus");
    }

    [Fact]
    public void CreateArmResourceNodeFromDictionary_WithNullLocation_CreatesNodeWithEmptyLocation()
    {

        var result = new Dictionary<string, object>
        {
            { "id", "vertex-123" },
            { "name", "test-app" },
            { "type", "microsoft.web/sites" },
            { "kind", "app" },
            {
                "properties", new Dictionary<string, object>
                {
                    { "resourceId", new[] { "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/test-app" } },
                    { "subscriptionId", new[] { "sub1" } },
                    { "resourceGroupName", new[] { "rg1" } },
                    { "resourceName", new[] { "test-app" } }
                    // No location property
                }
            }
        };

        var scanner = CreateScanner();
        var method = typeof(AppServiceScanner).GetMethod("CreateArmResourceNodeFromDictionary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


        var node = method?.Invoke(scanner, [result]) as ArmResourceNode;


        node.ShouldNotBeNull();
        node.Location.ShouldBe(string.Empty);

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Location is missing or empty")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void CreateArmResourceNodeFromDictionary_WithEmptyOrWhitespaceLocation_CreatesNodeWithEmptyLocation(string emptyLocation)
    {

        var result = new Dictionary<string, object>
        {
            { "id", "vertex-123" },
            { "name", "test-app" },
            { "type", "microsoft.web/sites" },
            { "kind", "app" },
            {
                "properties", new Dictionary<string, object>
                {
                    { "resourceId", new[] { "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/test-app" } },
                    { "subscriptionId", new[] { "sub1" } },
                    { "resourceGroupName", new[] { "rg1" } },
                    { "resourceName", new[] { "test-app" } },
                    { "location", new[] { emptyLocation } }
                }
            }
        };

        var scanner = CreateScanner();
        var method = typeof(AppServiceScanner).GetMethod("CreateArmResourceNodeFromDictionary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


        var node = method?.Invoke(scanner, [result]) as ArmResourceNode;


        node.ShouldNotBeNull();
        node.Location.ShouldBe(string.Empty);

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Location is missing or empty")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("North Europe", "northeurope")]
    [InlineData("South-East Asia", "southeastasia")]
    [InlineData("UK West", "ukwest")]
    [InlineData("North Central US (Stage)", "northcentralusstage")]
    public void CreateArmResourceNodeFromDictionary_WithLocationNeedingNormalization_NormalizesLocation(string inputLocation, string expectedLocation)
    {

        var result = new Dictionary<string, object>
        {
            { "id", "vertex-123" },
            { "name", "test-app" },
            { "type", "microsoft.web/sites" },
            { "kind", "app" },
            {
                "properties", new Dictionary<string, object>
                {
                    { "resourceId", new[] { "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/test-app" } },
                    { "subscriptionId", new[] { "sub1" } },
                    { "resourceGroupName", new[] { "rg1" } },
                    { "resourceName", new[] { "test-app" } },
                    { "location", new[] { inputLocation } }
                }
            }
        };

        var scanner = CreateScanner();
        var method = typeof(AppServiceScanner).GetMethod("CreateArmResourceNodeFromDictionary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


        var node = method?.Invoke(scanner, [result]) as ArmResourceNode;


        node.ShouldNotBeNull();
        node.Location.ShouldBe(expectedLocation);
    }

    #endregion

    #region GetFirstPropertyValue Tests

    [Fact]
    public void GetFirstPropertyValue_WithValidValue_ReturnsValue()
    {

        var properties = new Dictionary<string, object>
        {
            { "testKey", new[] { "testValue" } }
        };

        var scanner = CreateScanner();
        var method = typeof(AppServiceScanner).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


        var result = method?.Invoke(scanner, [properties, "testKey"]) as string;


        result.ShouldBe("testValue");
    }

    [Fact]
    public void GetFirstPropertyValue_WithNullProperties_ReturnsNull()
    {

        var scanner = CreateScanner();
        var method = typeof(AppServiceScanner).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


        var result = method?.Invoke(scanner, [null, "testKey"]) as string;


        result.ShouldBeNull();
    }

    [Fact]
    public void GetFirstPropertyValue_WithMissingKey_ReturnsNull()
    {

        var properties = new Dictionary<string, object>();
        var scanner = CreateScanner();
        var method = typeof(AppServiceScanner).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


        var result = method?.Invoke(scanner, [properties, "missingKey"]) as string;


        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void GetFirstPropertyValue_WithWhitespaceValue_ReturnsNull(string whitespace)
    {

        var properties = new Dictionary<string, object>
        {
            { "testKey", new[] { whitespace } }
        };

        var scanner = CreateScanner();
        var method = typeof(AppServiceScanner).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


        var result = method?.Invoke(scanner, [properties, "testKey"]) as string;


        result.ShouldBeNull();
    }

    [Fact]
    public void GetFirstPropertyValue_WithArrayContainingWhitespaceAndValidValue_ReturnsFirstValidValue()
    {

        var properties = new Dictionary<string, object>
        {
            { "testKey", new[] { "", "  ", "validValue", "anotherValue" } }
        };

        var scanner = CreateScanner();
        var method = typeof(AppServiceScanner).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


        var result = method?.Invoke(scanner, [properties, "testKey"]) as string;


        result.ShouldBe("validValue");
    }

    [Fact]
    public void GetFirstPropertyValue_WithArrayOfOnlyWhitespace_ReturnsNull()
    {

        var properties = new Dictionary<string, object>
        {
            { "testKey", new[] { "", "  ", "\t" } }
        };

        var scanner = CreateScanner();
        var method = typeof(AppServiceScanner).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


        var result = method?.Invoke(scanner, [properties, "testKey"]) as string;


        result.ShouldBeNull();
    }

    #endregion
}
