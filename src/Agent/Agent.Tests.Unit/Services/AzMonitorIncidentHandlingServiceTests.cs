// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DataModels;
using Agent.Data.DataModels.IncidentModel;
using Agent.Data.Interface.IncidentAPI;
using Agent.Framework;
using Agent.Runtime.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Services
{
    public class AzMonitorIncidentHandlingServiceTests
    {
        private readonly Mock<IIncidentFilterManagementService<AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload>> _mockFilterManagementService;
        private readonly Mock<IIncidentManagementService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload>> _mockIncidentManagementService;
        private readonly Mock<IAzMonitorAlertService> _mockAzMonitorAlertService;
        private readonly Mock<ILogger<AzMonitorIncidentHandlingService>> _mockLogger;
        private readonly Mock<IThreadRepository> _mockRepository;
        private readonly Mock<IAgentInboundCommunicationService> _mockInboundCommunicationService;
        private readonly Mock<IAgentOutboundCommunicationService> _mockOutboundCommunicationService;
        private readonly Mock<CosmosClient> _mockCosmosClient;
        private readonly Mock<Container> _mockContainer;
        private readonly Mock<IGraphDatabaseClient> _mockGraphDbClient;
        private readonly Mock<IIncidentStatusMetricsService> _mockIncidentStatusMetricsService;
        private readonly Mock<IIncidentHandlerManagementService> _mockHandlerManagementService;
        private readonly Mock<IIncidentAnalysisService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload, AlertItem>> _mockIncidentAnalysisService;
        private readonly Mock<IAgentFactory<AgentContext>> _mockAgentFactory;
        private readonly CosmosDBSettings _cosmosDbSettings;
        private readonly ExperimentalSettings _experimentalSettings;

        public AzMonitorIncidentHandlingServiceTests()
        {
            _mockFilterManagementService = new Mock<IIncidentFilterManagementService<AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload>>();
            _mockIncidentManagementService = new Mock<IIncidentManagementService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload>>();
            _mockAzMonitorAlertService = new Mock<IAzMonitorAlertService>();
            _mockLogger = new Mock<ILogger<AzMonitorIncidentHandlingService>>();
            _mockRepository = new Mock<IThreadRepository>();
            _mockInboundCommunicationService = new Mock<IAgentInboundCommunicationService>();
            _mockOutboundCommunicationService = new Mock<IAgentOutboundCommunicationService>();
            _mockCosmosClient = new Mock<CosmosClient>();
            _mockContainer = new Mock<Container>();
            _mockGraphDbClient = new Mock<IGraphDatabaseClient>();
            _mockIncidentStatusMetricsService = new Mock<IIncidentStatusMetricsService>();
            _mockHandlerManagementService = new Mock<IIncidentHandlerManagementService>();
            _mockIncidentAnalysisService = new Mock<IIncidentAnalysisService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload, AlertItem>>();
            _mockAgentFactory = new Mock<IAgentFactory<AgentContext>>();

            _cosmosDbSettings = new CosmosDBSettings
            {
                Docs = new DocsSettings
                {
                    Database = "TestDb"
                }
            };

            _experimentalSettings = new ExperimentalSettings
            {
                UseYamlForIncidentHandling = false
            };

            _mockCosmosClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_mockContainer.Object);
        }

        private AzMonitorIncidentHandlingService CreateService()
        {
            return new AzMonitorIncidentHandlingService(
                _mockFilterManagementService.Object,
                _mockIncidentManagementService.Object,
                _mockAzMonitorAlertService.Object,
                _mockLogger.Object,
                _mockRepository.Object,
                _mockInboundCommunicationService.Object,
                _mockOutboundCommunicationService.Object,
                _mockCosmosClient.Object,
                _cosmosDbSettings,
                _mockGraphDbClient.Object,
                _mockIncidentStatusMetricsService.Object,
                _mockHandlerManagementService.Object,
                _mockIncidentStatusMetricsService.Object,
                _mockOutboundCommunicationService.Object,
                _mockIncidentAnalysisService.Object,
                null!, // Tracer - not needed for these tests
                _mockAgentFactory.Object,
                _experimentalSettings
            );
        }

        private AzMonitorAlertDocument CreateTestIncidentDocument(string id, string title = "High CPU Alert")
        {
            return new AzMonitorAlertDocument
            {
                Id = id,
                Title = title,
                Description = "CPU usage exceeded 80%",
                Priority = "Sev2",
                Status = "New",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ImpactedServiceName = "TestService",
                ImpactedServiceId = "service-123",
                IncidentType = "AzMonitor"
            };
        }

        private AzMonitorIncidentFilterDocument CreateTestFilter(string id, string impactedService = "TestService")
        {
            return new AzMonitorIncidentFilterDocument
            {
                Id = id,
                Name = "Test Filter",
                ImpactedService = impactedService,
                Priority = "Sev2",
                IncidentType = "AzMonitor",
                TitleContains = "CPU",
                TargetResourceType = "microsoft.containerservice/managedclusters",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private IncidentHandlerDocument CreateTestHandler(string id, string filterId)
        {
            return new IncidentHandlerDocument(
                Id: id,
                DocumentType: "IncidentHandlerAzMonitor",
                Name: "Test Handler",
                Description: "Handles AKS incidents",
                IncidentFilterId: filterId,
                IncidentProcessingGuide: ["Check pod health", "Review HPA settings"],
                Tools: ["QueryKusto", "GetMetrics"],
                Incidents: [],
                CustomInstructions: "Focus on resource constraints",
                CreatedAt: DateTime.UtcNow
            );
        }

        private IncidentHandlingRequestModel<AzMonitorIncidentFilterDocumentPayload> CreateTestRequest()
        {
            return new IncidentHandlingRequestModel<AzMonitorIncidentFilterDocumentPayload>
            {
                IncidentId = "alert-123",
                Title = "High CPU Alert",
                Description = "CPU usage exceeded 80%",
                Severity = "Sev2",
                Source = "AzMonitor",
                CreatedTime = DateTimeOffset.UtcNow,
                ImpactedService = "TestService",
                IncidentFilter = null,
                IncidentHandler = null
            };
        }

        [Fact]
        public async Task GetIncidentFilterAndHandlerAsync_BothNull_FindsHandlerFromDatabase()
        {
            var service = CreateService();
            var incidentDoc = CreateTestIncidentDocument("alert-123");
            var filter = CreateTestFilter("filter-1", "TestService");
            var handler = CreateTestHandler("handler-1", "filter-1");
            var request = CreateTestRequest();

            _mockFilterManagementService.Setup(s => s.ListIncidentFilters(false))
                .ReturnsAsync([filter]);

            _mockHandlerManagementService.Setup(s => s.ListIncidentHandlers())
                .ReturnsAsync([handler]);

            var (matchedFilter, matchedHandler) = await service.GetIncidentFilterAndHandlerAsync(request, incidentDoc);

            Assert.NotNull(matchedFilter);
            Assert.Equal("filter-1", matchedFilter.Id);
            Assert.NotNull(matchedHandler); // This is the key test - handler should be found!
            Assert.Equal("handler-1", matchedHandler.Id);
            Assert.Equal("filter-1", matchedHandler.IncidentFilterId);
        }

        [Fact]
        public async Task GetIncidentFilterAndHandlerAsync_BothNull_NoHandlerFound_ReturnsNullHandler()
        {
            var service = CreateService();
            var incidentDoc = CreateTestIncidentDocument("alert-123");
            var filter = CreateTestFilter("filter-1", "TestService");
            var request = CreateTestRequest();

            _mockFilterManagementService.Setup(s => s.ListIncidentFilters(false))
                .ReturnsAsync([filter]);

            _mockHandlerManagementService.Setup(s => s.ListIncidentHandlers())
                .ReturnsAsync([]); // No handlers

            var (matchedFilter, matchedHandler) = await service.GetIncidentFilterAndHandlerAsync(request, incidentDoc);

            Assert.NotNull(matchedFilter);
            Assert.Equal("filter-1", matchedFilter.Id);
            Assert.Null(matchedHandler); // No handler found
        }

        [Fact]
        public async Task GetIncidentFilterAndHandlerAsync_HandlerLookupByIncidentFilterId()
        {
            var service = CreateService();
            var incidentDoc = CreateTestIncidentDocument("alert-123");
            var filter = CreateTestFilter("filter-abc", "TestService");
            var handler1 = CreateTestHandler("handler-1", "filter-xyz"); // Wrong filter
            var handler2 = CreateTestHandler("handler-2", "filter-abc"); // Correct filter
            var request = CreateTestRequest();

            _mockFilterManagementService.Setup(s => s.ListIncidentFilters(false))
                .ReturnsAsync([filter]);

            _mockHandlerManagementService.Setup(s => s.ListIncidentHandlers())
                .ReturnsAsync([handler1, handler2]);

            var (matchedFilter, matchedHandler) = await service.GetIncidentFilterAndHandlerAsync(request, incidentDoc);

            Assert.NotNull(matchedFilter);
            Assert.Equal("filter-abc", matchedFilter.Id);
            Assert.NotNull(matchedHandler);
            Assert.Equal("handler-2", matchedHandler.Id); // Should match by IncidentFilterId
            Assert.Equal("filter-abc", matchedHandler.IncidentFilterId);
        }
    }
}
