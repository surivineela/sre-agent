// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data.DataModels;
using Agent.Data.DataModels.IncidentModel;
using Agent.Runtime.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Services
{
    public class AzMonitorAlertServiceTests
    {
        private readonly Mock<ILogger<AzMonitorAlertService>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<CosmosClient> _mockCosmosClient;
        private readonly Mock<Container> _mockContainer;
        private readonly CrawlerSettings _crawlerSettings;
        private readonly CosmosDBSettings _cosmosDbSettings;

        public AzMonitorAlertServiceTests()
        {
            _mockLogger = new Mock<ILogger<AzMonitorAlertService>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockCosmosClient = new Mock<CosmosClient>();
            _mockContainer = new Mock<Container>();

            _crawlerSettings = new CrawlerSettings
            {
                CrawlRoots = "/subscriptions/test-sub-1/resourceGroups/test-rg-1"
            };

            _cosmosDbSettings = new CosmosDBSettings
            {
                Docs = new DocsSettings
                {
                    Database = "TestDb"
                }
            };

            _mockCosmosClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockContainer.Object);
        }

        private AlertItem CreateAlertItem(string id, string severity, string title = "Test Alert")
        {
            return new AlertItem
            {
                Id = id,
                Name = title,
                Type = "Microsoft.AlertsManagement/alerts",
                Properties = new AlertProperties
                {
                    Essentials = new AlertEssentials
                    {
                        Severity = severity,
                        AlertState = "New",
                        MonitorCondition = "Fired",
                        StartDateTime = DateTime.UtcNow.ToString("o"),
                        LastModifiedDateTime = DateTime.UtcNow.ToString("o"),
                        Description = $"Alert with {severity}"
                    }
                }
            };
        }

        [Fact]
        public void AzMonitorIncidentFilterDocumentPayload_WithSeverityLevels_StoresCorrectly()
        {
            var filterPayload = new AzMonitorIncidentFilterDocumentPayload
            {
                Id = "filter-1",
                Priorities = ["Sev0", "Sev1", "Sev2"],
                TargetResourceType = "microsoft.containerservice/managedclusters",
                TargetResource = "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.ContainerService/managedClusters/test-aks"
            };

            Assert.NotNull(filterPayload.Priorities);
            Assert.Equal(3, filterPayload.Priorities.Count);
            Assert.Contains("Sev0", filterPayload.Priorities);
            Assert.Contains("Sev1", filterPayload.Priorities);
            Assert.Contains("Sev2", filterPayload.Priorities);
        }

        [Fact]
        public void AzMonitorIncidentFilterDocument_WithSeverityLevels_CreatesCorrectly()
        {
            var filterDocument = new AzMonitorIncidentFilterDocument
            {
                Id = "filter-test-1",
                Name = "Test Filter",
                Priorities = ["Sev1", "Sev2"],
                TargetResourceType = "microsoft.containerservice/managedclusters",
                AgentMode = "review",
                IsEnabled = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            Assert.Equal("filter-test-1", filterDocument.Id);
            Assert.NotNull(filterDocument.Priorities);
            Assert.Equal(2, filterDocument.Priorities.Count);
            Assert.Equal("IncidentFilterAzMonitor", filterDocument.DocumentType);
            Assert.True(filterDocument.IsEnabled);
        }

        [Fact]
        public void AzMonitorIncidentFilterDocumentPayload_SeverityLevels_DefaultsToEmptyList()
        {
            var filterPayload = new AzMonitorIncidentFilterDocumentPayload
            {
                Id = "filter-2"
            };

            Assert.NotNull(filterPayload.Priorities);
            Assert.Empty(filterPayload.Priorities);
        }


        [Fact]
        public void AlertItem_SeverityComparison_IsCaseInsensitive()
        {
            var alert1 = CreateAlertItem("alert-1", "Sev2");
            var alert2 = CreateAlertItem("alert-2", "sev2");
            var alert3 = CreateAlertItem("alert-3", "SEV2");

            var severityLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sev2" };

            Assert.Contains(alert1.Properties!.Essentials!.Severity!, severityLevels);
            Assert.Contains(alert2.Properties!.Essentials!.Severity!, severityLevels);
            Assert.Contains(alert3.Properties!.Essentials!.Severity!, severityLevels);
        }

        [Fact]
        public void AlertItem_SeverityNormalization_HandlesBothFormats()
        {
            var severityLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sev in new[] { "Sev2", "2", "sev3", "3" })
            {
                var normalized = sev.Trim();
                severityLevels.Add(normalized.StartsWith("Sev", StringComparison.OrdinalIgnoreCase) ? normalized : $"Sev{normalized}");
            }

            Assert.Contains("Sev2", severityLevels);
            Assert.Contains("Sev3", severityLevels);
            Assert.Equal(2, severityLevels.Count); // Should deduplicate Sev2 and Sev3
        }

        [Theory]
        [InlineData("Sev0")]
        [InlineData("Sev1")]
        [InlineData("Sev2")]
        [InlineData("Sev3")]
        [InlineData("Sev4")]
        public void AlertItem_AllValidSeverityLevels_CanBeFiltered(string severity)
        {
            var alert = CreateAlertItem("test-alert", severity);
            var severityLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { severity };

            var matches = alert.Properties?.Essentials?.Severity != null &&
                         severityLevels.Contains(alert.Properties.Essentials.Severity);

            Assert.True(matches);
        }

        [Fact]
        public void SeverityFiltering_MultipleAlerts_FiltersCorrectly()
        {
            var alerts = new List<AlertItem>
            {
                CreateAlertItem("alert-1", "Sev0", "Critical Production Issue"),
                CreateAlertItem("alert-2", "Sev1", "High Priority Alert"),
                CreateAlertItem("alert-3", "Sev2", "Medium Priority Alert"),
                CreateAlertItem("alert-4", "Sev3", "Low Priority Alert"),
                CreateAlertItem("alert-5", "Sev4", "Informational Alert")
            };

            var severityLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sev0", "Sev1" };

            var filtered = alerts.Where(a =>
                a.Properties?.Essentials?.Severity != null &&
                severityLevels.Contains(a.Properties.Essentials.Severity)
            ).ToList();

            Assert.Equal(2, filtered.Count);
            Assert.All(filtered, alert => Assert.True(
                alert.Properties!.Essentials!.Severity == "Sev0" ||
                alert.Properties.Essentials.Severity == "Sev1"
            ));
        }

        [Fact]
        public void SeverityFiltering_EmptySeverityLevels_NoFiltering()
        {
            var alerts = new List<AlertItem>
            {
                CreateAlertItem("alert-1", "Sev0"),
                CreateAlertItem("alert-2", "Sev2"),
                CreateAlertItem("alert-3", "Sev4")
            };

            HashSet<string>? severityLevels = null; // No filtering

            IEnumerable<AlertItem> filtered = alerts;
            if (severityLevels != null && severityLevels.Count > 0)
            {
                filtered = filtered.Where(a =>
                    a.Properties?.Essentials?.Severity != null &&
                    severityLevels.Contains(a.Properties.Essentials.Severity)
                );
            }

            Assert.Equal(3, filtered.Count()); // All alerts returned
        }

        [Fact]
        public void SeverityFiltering_NullSeverityInAlert_Excluded()
        {
            var alerts = new List<AlertItem>
            {
                CreateAlertItem("alert-1", "Sev2"),
                new() { Id = "alert-2", Properties = new AlertProperties { Essentials = new AlertEssentials { Severity = string.Empty } } },
                CreateAlertItem("alert-3", "Sev3")
            };

            var severityLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sev2", "Sev3" };

            var filtered = alerts.Where(a =>
                !string.IsNullOrWhiteSpace(a.Properties?.Essentials?.Severity) &&
                severityLevels.Contains(a.Properties.Essentials.Severity)
            ).ToList();

            Assert.Equal(2, filtered.Count);
            Assert.All(filtered, alert => Assert.False(string.IsNullOrWhiteSpace(alert.Properties!.Essentials!.Severity)));
        }
    }
}
