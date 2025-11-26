// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.Repositories;
using Agent.Graph.Crawler;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Agent.Tests.Unit.Graph.Crawler;

/// <summary>
/// Unit tests for ScoreCardService location handling
/// </summary>
public class ScoreCardServiceTests
{
    private readonly Mock<ILogger<ScoreCardService>> _mockLogger;
    private readonly Mock<IGraphDatabaseClient> _mockGraphDatabaseClient;
    private readonly Mock<IAppHealthHistoryRepository> _mockAppHealthHistoryRepository;
    private static readonly string[] value = ["/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/test-app"];

    public ScoreCardServiceTests()
    {
        _mockLogger = new Mock<ILogger<ScoreCardService>>();
        _mockGraphDatabaseClient = new Mock<IGraphDatabaseClient>();
        _mockAppHealthHistoryRepository = new Mock<IAppHealthHistoryRepository>();
    }

    private ScoreCardService CreateService()
    {
        return new ScoreCardService(
            _mockLogger.Object,
            _mockGraphDatabaseClient.Object,
            [],
            _mockAppHealthHistoryRepository.Object);
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
            {
                "properties", new Dictionary<string, object>
                {
                    { "resourceId", value },
                    { "subscriptionId", new[] { "sub1" } },
                    { "resourceGroupName", new[] { "rg1" } },
                    { "resourceName", new[] { "test-app" } },
                    { "location", new[] { "eastus" } }
                }
            }
        };

        var service = CreateService();
        var method = typeof(ScoreCardService).GetMethod("CreateArmResourceNodeFromDictionary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var node = method?.Invoke(service, [result]) as ArmResourceNode;

        node.ShouldNotBeNull();
        node.Location.ShouldBe("eastus");
    }

    [Fact]
    public void CreateArmResourceNodeFromDictionary_WithNullLocation_CreatesNodeWithEmptyLocation()
    {
        var result = new Dictionary<string, object>
        {
            { "id", "vertex-123" },
            { "name", "test-app" },
            { "type", "microsoft.web/sites" },
            {
                "properties", new Dictionary<string, object>
                {
                    { "resourceId", value },
                    { "subscriptionId", new[] { "sub1" } },
                    { "resourceGroupName", new[] { "rg1" } },
                    { "resourceName", new[] { "test-app" } }
                    // No location property
                }
            }
        };

        var service = CreateService();
        var method = typeof(ScoreCardService).GetMethod("CreateArmResourceNodeFromDictionary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var node = method?.Invoke(service, [result]) as ArmResourceNode;

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

        var service = CreateService();
        var method = typeof(ScoreCardService).GetMethod("CreateArmResourceNodeFromDictionary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var node = method?.Invoke(service, [result]) as ArmResourceNode;

        node.ShouldNotBeNull();
        node.Location.ShouldBe(string.Empty);

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
    [InlineData("East US", "eastus")]
    [InlineData("West Europe", "westeurope")]
    [InlineData("Central-US", "centralus")]
    [InlineData("North Central US (Stage)", "northcentralusstage")]
    public void CreateArmResourceNodeFromDictionary_WithLocationNeedingNormalization_NormalizesLocation(string inputLocation, string expectedLocation)
    {
        var result = new Dictionary<string, object>
        {
            { "id", "vertex-123" },
            { "name", "test-app" },
            { "type", "microsoft.web/sites" },
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

        var service = CreateService();
        var method = typeof(ScoreCardService).GetMethod("CreateArmResourceNodeFromDictionary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var node = method?.Invoke(service, [result]) as ArmResourceNode;

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

        var service = CreateService();
        var method = typeof(ScoreCardService).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = method?.Invoke(service, [properties, "testKey"]) as string;

        result.ShouldBe("testValue");
    }

    [Fact]
    public void GetFirstPropertyValue_WithNullProperties_ReturnsNull()
    {
        var service = CreateService();
        var method = typeof(ScoreCardService).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = method?.Invoke(service, [null, "testKey"]) as string;

        result.ShouldBeNull();
    }

    [Fact]
    public void GetFirstPropertyValue_WithMissingKey_ReturnsNull()
    {
        var properties = new Dictionary<string, object>();
        var service = CreateService();
        var method = typeof(ScoreCardService).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = method?.Invoke(service, [properties, "missingKey"]) as string;

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

        var service = CreateService();
        var method = typeof(ScoreCardService).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = method?.Invoke(service, [properties, "testKey"]) as string;

        result.ShouldBeNull();
    }

    [Fact]
    public void GetFirstPropertyValue_WithArrayContainingWhitespaceAndValidValue_ReturnsFirstValidValue()
    {
        var properties = new Dictionary<string, object>
        {
            { "testKey", new[] { "", "  ", "validValue", "anotherValue" } }
        };

        var service = CreateService();
        var method = typeof(ScoreCardService).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = method?.Invoke(service, [properties, "testKey"]) as string;

        result.ShouldBe("validValue");
    }

    [Fact]
    public void GetFirstPropertyValue_WithArrayOfOnlyWhitespace_ReturnsNull()
    {
        var properties = new Dictionary<string, object>
        {
            { "testKey", new[] { "", "  ", "\t" } }
        };

        var service = CreateService();
        var method = typeof(ScoreCardService).GetMethod("GetFirstPropertyValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = method?.Invoke(service, [properties, "testKey"]) as string;

        result.ShouldBeNull();
    }

    #endregion
}
