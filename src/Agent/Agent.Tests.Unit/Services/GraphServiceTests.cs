// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Interfaces;
using Agent.Plugins.Interface;
using Agent.Plugins.Services;
using Gremlin.Net.Driver;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Services
{
    public class GraphServiceTests
    {
        private readonly Mock<IGraphDatabaseClient> _mockGraphDatabaseClient;
        private readonly Mock<ILogger<GraphService>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IAuthenticationService> _mockAuthenticationService;
        private readonly Mock<ICrawlerService> _mockCrawlerService;
        private readonly Mock<IThreadRepository> _mockThreadRepository;
        private readonly Mock<IGithubIssuePlugin> _mockGithubIssuePlugin;
        private readonly Mock<IAzureDevOpsWorkItemPlugin> _mockAzureDevOpsWorkItemPlugin;
        private readonly DashboardSettings _dashboardSettings;
        private readonly GraphService _graphService;

        public GraphServiceTests()
        {
            _mockGraphDatabaseClient = new Mock<IGraphDatabaseClient>();
            _mockLogger = new Mock<ILogger<GraphService>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockAuthenticationService = new Mock<IAuthenticationService>();
            _mockCrawlerService = new Mock<ICrawlerService>();
            _mockThreadRepository = new Mock<IThreadRepository>();
            _mockGithubIssuePlugin = new Mock<IGithubIssuePlugin>();
            _mockAzureDevOpsWorkItemPlugin = new Mock<IAzureDevOpsWorkItemPlugin>();

            _dashboardSettings = new DashboardSettings
            {
                GrafanaUrl = "https://grafana.example.com/"
            };

            _graphService = new GraphService(
                _mockGraphDatabaseClient.Object,
                _dashboardSettings,
                _mockLogger.Object,
                _mockHttpClientFactory.Object,
                _mockAuthenticationService.Object,
                _mockCrawlerService.Object,
                _mockThreadRepository.Object,
                _mockGithubIssuePlugin.Object,
                _mockAzureDevOpsWorkItemPlugin.Object);
        }

        #region SearchResourcesAsync Tests

        [Fact]
        public async Task SearchResourcesAsync_WithNameFilter_ReturnsMatchingResources()
        {
            // Arrange
            var mockResults = new List<Dictionary<string, object>>
            {
                new() {
                    { "resourceId", "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/webapp1" },
                    { "name", "webapp1" },
                    { "type", "microsoft.web/sites" },
                    { "kind", "app" },
                    { "subscriptionId", "sub1" },
                    { "resourceGroup", "rg1" },
                    { "location", "eastus" },
                    { "properties", new Dictionary<string, object>() }
                },
                new() {
                    { "resourceId", "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/webapp2" },
                    { "name", "webapp2" },
                    { "type", "microsoft.web/sites" },
                    { "kind", "app" },
                    { "subscriptionId", "sub1" },
                    { "resourceGroup", "rg1" },
                    { "location", "eastus" },
                    { "properties", new Dictionary<string, object>() }
                }
            };

            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>([2], new Dictionary<string, object>());

            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.IsAny<string>()))
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.IsAny<string>()))
                .ReturnsAsync(resultSet);

            var (resources, totalCount) = await _graphService.SearchResourcesAsync("web", null, null, 0, 20);

            Assert.Equal(2, resources.Count);
            Assert.Equal(2, totalCount);
            Assert.All(resources, r => Assert.Contains("web", r.Name.ToLower()));
        }

        [Fact]
        public async Task SearchResourcesAsync_WithTypeFilter_ReturnsMatchingResources()
        {
            var mockResults = new List<Dictionary<string, object>>
            {
                new() {
                    { "resourceId", "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.App/containerApps/app1" },
                    { "name", "app1" },
                    { "type", "microsoft.app/containerapps" },
                    { "kind", "containerapp" },
                    { "subscriptionId", "sub1" },
                    { "resourceGroup", "rg1" },
                    { "location", "eastus" },
                    { "properties", new Dictionary<string, object>() }
                }
            };

            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>([1], new Dictionary<string, object>());

            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.Is<string>(q => q.Contains("microsoft.app/containerapps"))))
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.Is<string>(q => q.Contains("microsoft.app/containerapps"))))
                .ReturnsAsync(resultSet);

            var (resources, totalCount) = await _graphService.SearchResourcesAsync(null, "microsoft.app/containerapps", null, 0, 20);

            Assert.Single(resources);
            Assert.Equal(1, totalCount);
            Assert.Equal("microsoft.app/containerapps", resources[0].Type);
        }

        [Fact]
        public async Task SearchResourcesAsync_WithSubscriptionFilter_ReturnsMatchingResources()
        {
            var mockResults = new List<Dictionary<string, object>>
            {
                new() {
                    { "resourceId", "/subscriptions/sub123/resourceGroups/rg1/providers/Microsoft.Web/sites/app1" },
                    { "name", "app1" },
                    { "type", "microsoft.web/sites" },
                    { "kind", "app" },
                    { "subscriptionId", "sub123" },
                    { "resourceGroup", "rg1" },
                    { "location", "eastus" },
                    { "properties", new Dictionary<string, object>() }
                }
            };

            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>([1], new Dictionary<string, object>());

            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.Is<string>(q => q.Contains("sub123"))))
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.Is<string>(q => q.Contains("sub123"))))
                .ReturnsAsync(resultSet);

            var (resources, totalCount) = await _graphService.SearchResourcesAsync(null, null, "sub123", 0, 20);

            Assert.Single(resources);
            Assert.Equal(1, totalCount);
            Assert.Equal("sub123", resources[0].SubscriptionId);
        }

        [Fact]
        public async Task SearchResourcesAsync_WithPagination_ReturnsCorrectPage()
        {
            var mockResults = new List<Dictionary<string, object>>();
            for (int i = 1; i <= 5; i++)
            {
                mockResults.Add(new Dictionary<string, object>
                {
                    { "resourceId", $"/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app{i}" },
                    { "name", $"app{i}" },
                    { "type", "microsoft.web/sites" },
                    { "kind", "app" },
                    { "subscriptionId", "sub1" },
                    { "resourceGroup", "rg1" },
                    { "location", "eastus" },
                    { "properties", new Dictionary<string, object>() }
                });
            }

            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>([5], new Dictionary<string, object>());

            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.IsAny<string>()))
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.IsAny<string>()))
                .ReturnsAsync(resultSet);

            // Act - Get second page with 2 items per page
            var (resources, totalCount) = await _graphService.SearchResourcesAsync(null, null, null, 1, 2);

            Assert.Equal(2, resources.Count);
            Assert.Equal(5, totalCount);
            Assert.Equal("app3", resources[0].Name); // Third item (page 1, index 0)
            Assert.Equal("app4", resources[1].Name); // Fourth item (page 1, index 1)
        }

        [Fact]
        public async Task SearchResourcesAsync_WithNoMatches_ReturnsEmptyList()
        {
            var emptyResults = new List<Dictionary<string, object>>();
            var resultSet = new ResultSet<Dictionary<string, object>>(emptyResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>([0], new Dictionary<string, object>());

            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.IsAny<string>()))
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.IsAny<string>()))
                .ReturnsAsync(resultSet);

            var (resources, totalCount) = await _graphService.SearchResourcesAsync("nonexistent", null, null, 0, 20);

            Assert.Empty(resources);
            Assert.Equal(0, totalCount);
        }

        [Fact]
        public async Task SearchResourcesAsync_WithAllFilters_ReturnsMatchingResources()
        {
            var mockResults = new List<Dictionary<string, object>>
            {
                new() {
                    { "resourceId", "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/webapp1" },
                    { "name", "webapp1" },
                    { "type", "microsoft.web/sites" },
                    { "kind", "app" },
                    { "subscriptionId", "sub1" },
                    { "resourceGroup", "rg1" },
                    { "location", "eastus" },
                    { "properties", new Dictionary<string, object>() }
                }
            };

            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>([1], new Dictionary<string, object>());

            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.Is<string>(q =>
                    q.Contains("sub1") &&
                    q.Contains("microsoft.web/sites"))))
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.Is<string>(q =>
                    q.Contains("sub1") &&
                    q.Contains("microsoft.web/sites"))))
                .ReturnsAsync(resultSet);

            var (resources, totalCount) = await _graphService.SearchResourcesAsync("web", "microsoft.web/sites", "sub1", 0, 20);

            Assert.Single(resources);
            Assert.Equal(1, totalCount);
            Assert.Equal("webapp1", resources[0].Name);
            Assert.Equal("microsoft.web/sites", resources[0].Type);
            Assert.Equal("sub1", resources[0].SubscriptionId);
        }

        [Fact]
        public async Task SearchResourcesAsync_WithCaseInsensitiveNameFilter_ReturnsMatchingResources()
        {
            var mockResults = new List<Dictionary<string, object>>
            {
                new() {
                    { "resourceId", "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/WebApp1" },
                    { "name", "WebApp1" },
                    { "type", "microsoft.web/sites" },
                    { "kind", "app" },
                    { "subscriptionId", "sub1" },
                    { "resourceGroup", "rg1" },
                    { "location", "eastus" },
                    { "properties", new Dictionary<string, object>() }
                }
            };

            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>([1], new Dictionary<string, object>());

            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.IsAny<string>()))
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.IsAny<string>()))
                .ReturnsAsync(resultSet);

            // Act - Search with lowercase "webapp" should match "WebApp1"
            var (resources, totalCount) = await _graphService.SearchResourcesAsync("webapp", null, null, 0, 20);

            Assert.Single(resources);
            Assert.Equal(1, totalCount);
            Assert.Equal("WebApp1", resources[0].Name);
        }

        [Fact]
        public async Task SearchResourcesAsync_WithProperties_MapsPropertiesCorrectly()
        {
            var mockProperties = new Dictionary<string, object>
            {
                { "state", "Running" },
                { "sku", new List<object> { "Standard" } }, // Test list handling
                { "tags", new Dictionary<string, object> { { "env", "prod" } } }
            };

            var mockResults = new List<Dictionary<string, object>>
            {
                new() {
                    { "resourceId", "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Web/sites/app1" },
                    { "name", "app1" },
                    { "type", "microsoft.web/sites" },
                    { "kind", "app" },
                    { "subscriptionId", "sub1" },
                    { "resourceGroup", "rg1" },
                    { "location", "eastus" },
                    { "properties", mockProperties }
                }
            };

            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>([1], new Dictionary<string, object>());

            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.IsAny<string>()))
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.IsAny<string>()))
                .ReturnsAsync(resultSet);

            var (resources, totalCount) = await _graphService.SearchResourcesAsync(null, null, null, 0, 20);

            Assert.Single(resources);
            var resource = resources[0];

            // Validate and work with strongly-typed, non-null properties without using null-forgiving operator
            if (resource.Properties is Dictionary<string, object> properties)
            {
                Assert.True(properties.ContainsKey("state"));
                Assert.Equal("Running", properties["state"]);
                Assert.True(properties.ContainsKey("sku"));
                Assert.Equal("Standard", properties["sku"]); // List should be flattened to first element
            }
            else
            {
                Assert.Fail("Expected non-null Properties dictionary");
            }
        }

        [Fact]
        public async Task SearchResourcesAsync_WithDefaultPagination_UsesDefaultValues()
        {
            var mockResults = new List<Dictionary<string, object>>();
            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>([0], new Dictionary<string, object>());

            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.IsAny<string>()))
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.IsAny<string>()))
                .ReturnsAsync(resultSet);

            var (resources, totalCount) = await _graphService.SearchResourcesAsync();

            // Assert - Should use default values: pageIndex=0, pageSize=20
            Assert.Empty(resources);
            _mockGraphDatabaseClient.Verify(x => x.Query<Dictionary<string, object>>(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task SearchResourcesAsync_WithSingleQuoteInSubscriptionId_EscapesCorrectly()
        {
            string maliciousSubscriptionId = "sub1'test";
            var mockResults = new List<Dictionary<string, object>>();
            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>(new List<long> { 0 }, new Dictionary<string, object>());

            string? capturedQuery = null;
            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.IsAny<string>()))
                .Callback<string>(q => capturedQuery = q)
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.IsAny<string>()))
                .ReturnsAsync(resultSet);

            var (resources, totalCount) = await _graphService.SearchResourcesAsync(null, null, maliciousSubscriptionId, 0, 20);

            Assert.NotNull(capturedQuery);
            // Verify that the single quote is escaped in the generated query
            Assert.Contains("sub1\\'test", capturedQuery);
            // The entire escaped value should be within quotes, treated as a literal string
            Assert.Contains("'sub1\\'test'", capturedQuery);
            // Verify the query structure is maintained - should still be a .has() call
            Assert.Contains(".has('subscriptionId'", capturedQuery);
        }

        [Fact]
        public async Task SearchResourcesAsync_WithBackslashInType_EscapesCorrectly()
        {
            string typeWithBackslash = "microsoft\\test";
            var mockResults = new List<Dictionary<string, object>>();
            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>(new List<long> { 0 }, new Dictionary<string, object>());

            string? capturedQuery = null;
            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.IsAny<string>()))
                .Callback<string>(q => capturedQuery = q)
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.IsAny<string>()))
                .ReturnsAsync(resultSet);

            var (resources, totalCount) = await _graphService.SearchResourcesAsync(null, typeWithBackslash, null, 0, 20);

            Assert.NotNull(capturedQuery);
            // Verify that backslashes are properly escaped
            Assert.Contains("microsoft\\\\test", capturedQuery);
        }

        [Fact]
        public async Task SearchResourcesAsync_WithGremlinInjectionAttempt_NeutralizesAttack()
        {
            string injectionAttempt = "test').drop().constant('x";
            var mockResults = new List<Dictionary<string, object>>();
            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>(new List<long> { 0 }, new Dictionary<string, object>());

            string? capturedQuery = null;
            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.IsAny<string>()))
                .Callback<string>(q => capturedQuery = q)
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.IsAny<string>()))
                .ReturnsAsync(resultSet);

            var (resources, totalCount) = await _graphService.SearchResourcesAsync(null, injectionAttempt, null, 0, 20);

            Assert.NotNull(capturedQuery);
            // Verify the injection is neutralized - single quotes should be escaped
            Assert.Contains("\\'", capturedQuery);
            // The escaped value should be: 'test\\').drop().constant(\\'x'
            Assert.Contains("test\\').drop().constant(\\'x", capturedQuery);
            // Verify the query structure remains intact - should be a .has() call
            Assert.Contains(".has('resourceType'", capturedQuery);
        }

        [Fact]
        public async Task SearchResourcesAsync_WithComplexInjectionPayload_HandlesSecurely()
        {
            string complexPayload = "sub1\\').or(has('evil','true";
            var mockResults = new List<Dictionary<string, object>>();
            var resultSet = new ResultSet<Dictionary<string, object>>(mockResults, new Dictionary<string, object>());
            var countResultSet = new ResultSet<long>(new List<long> { 0 }, new Dictionary<string, object>());

            string? capturedQuery = null;
            _mockGraphDatabaseClient
                .Setup(x => x.Query<long>(It.IsAny<string>()))
                .Callback<string>(q => capturedQuery = q)
                .ReturnsAsync(countResultSet);

            _mockGraphDatabaseClient
                .Setup(x => x.Query<Dictionary<string, object>>(It.IsAny<string>()))
                .ReturnsAsync(resultSet);

            var (resources, totalCount) = await _graphService.SearchResourcesAsync(null, null, complexPayload, 0, 20);

            Assert.NotNull(capturedQuery);
            // Both backslash and single quote should be escaped
            Assert.Contains("\\\\", capturedQuery); // Escaped backslash
            Assert.Contains("\\'", capturedQuery); // Escaped single quote
            // The escaped value should contain the neutralized payload
            // sub1\').or(has('evil','true becomes sub1\\').or(has(\\'evil\\',\\'true
            Assert.Contains("sub1\\\\\\').or(has(\\'evil\\',\\'true", capturedQuery);
            // Verify the query structure is maintained
            Assert.Contains(".has('subscriptionId'", capturedQuery);
        }

        #endregion
    }
}
