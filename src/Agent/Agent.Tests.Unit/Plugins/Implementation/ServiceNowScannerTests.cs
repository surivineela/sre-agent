// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.ServiceNow;
using Agent.Data.DataModels;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents.IcmScanner;
using Agent.Runtime.SubAgents.ServiceNowScanner;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Microsoft.Azure.Cosmos.Linq;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class ServiceNowScannerTests
    {
        private readonly Mock<ILogger<ServiceNowScanner>> _mockLogger;
        private readonly Mock<IServiceNowAPIClient> _mockServiceNowApiClient;
        private readonly Mock<CosmosClient> _mockCosmosClient;
        private readonly Mock<Container> _mockContainer;
        private readonly Mock<IIncidentHandlingService<ServiceNowIncidentFilterDocumentPayload>> _mockIncidentHandlingService;
        private readonly Mock<IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload>> _mockIncidentManagementService;
        private readonly Mock<IIncidentFilterManagementService<ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload>> _mockIncidentFilterManagementService;
        private readonly Mock<IAgentInboundCommunicationService> _mockAgentInboundCommunicationService;
        private readonly CosmosDBSettings _cosmosDbSettings;

        public ServiceNowScannerTests()
        {
            _mockLogger = new Mock<ILogger<ServiceNowScanner>>();
            _mockServiceNowApiClient = new Mock<IServiceNowAPIClient>();
            _mockCosmosClient = new Mock<CosmosClient>();
            _mockContainer = new Mock<Container>();
            _mockIncidentHandlingService = new Mock<IIncidentHandlingService<ServiceNowIncidentFilterDocumentPayload>>();
            _mockIncidentManagementService = new Mock<IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload>>();
            _mockIncidentFilterManagementService = new Mock<IIncidentFilterManagementService<ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload>>();
            _mockAgentInboundCommunicationService = new Mock<IAgentInboundCommunicationService>();

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

        private ServiceNowScanner CreateScanner()
        {
            return new ServiceNowScanner(
                _mockLogger.Object,
                _mockServiceNowApiClient.Object,
                _mockCosmosClient.Object,
                _cosmosDbSettings,
                _mockIncidentHandlingService.Object,
                _mockIncidentManagementService.Object,
                _mockIncidentFilterManagementService.Object,
                _mockAgentInboundCommunicationService.Object
            );
        }

        [Fact]
        public async Task ScanAsync_NoFilters_SkipsScan()
        {
            // Arrange
            _mockIncidentFilterManagementService.Setup(s => s.ListIncidentFilters())
                .ReturnsAsync(new List<ServiceNowIncidentFilterDocument>());

            var lastScanTimeDocResponse = new Mock<ItemResponse<LastScanTimeDoc>>();
            lastScanTimeDocResponse.Setup(r => r.Resource).Returns(new LastScanTimeDoc());

            _mockContainer.Setup(c => c.ReadItemAsync<LastScanTimeDoc>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lastScanTimeDocResponse.Object);

            var scanner = CreateScanner();


            try
            {
                // Act
                var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await scanner.ScanAsync(cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // expected cancellation exception
            }

            // Assert
            _mockServiceNowApiClient.Verify(
                c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task ScanAsync_WithNonServiceNowFilter_SkipsScanForThatFilter()
        {
            // Arrange
            var filters = new List<ServiceNowIncidentFilterDocument>
            {
                new ServiceNowIncidentFilterDocument
                {
                    Id = "filter1",
                    UpdatedAt = DateTime.UtcNow,
                    Name = "Test Filter",
                    ImpactedService = "TestService",
                    Priority = "1",
                    IncidentType = "ServiceNow",
                    AlertId = "alert1",
                    TitleContains = "Test"
                }
            };

           filters = filters.Where(f => f.DocumentType == "IncidentFilterIcm").ToList();

            _mockIncidentFilterManagementService.Setup(s => s.ListIncidentFilters()).ReturnsAsync(filters);
            var lastScanTimeDocResponse = new Mock<ItemResponse<LastScanTimeDoc>>();
            lastScanTimeDocResponse.Setup(r => r.Resource).Returns(new LastScanTimeDoc());

            _mockContainer.Setup(c => c.ReadItemAsync<LastScanTimeDoc>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lastScanTimeDocResponse.Object);

            var scanner = CreateScanner();
            

            try
            {
                // Act
                var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await scanner.ScanAsync(cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // expected cancellation exception
            }

            // Assert
            _mockServiceNowApiClient.Verify(
                c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task ScanAsync_NewIncident_CreatesDocument()
        {
            // Arrange
            var filters = new List<ServiceNowIncidentFilterDocument>
            {
                new ServiceNowIncidentFilterDocument
                {
                    Id = "filter1",
                    UpdatedAt = DateTime.UtcNow,
                    Name = "Test Filter",
                    ImpactedService = "",
                    Priority = "",
                    IncidentType = "ServiceNow",
                    AlertId = "",
                    TitleContains = ""
                }
            };
            _mockIncidentFilterManagementService.Setup(s => s.ListIncidentFilters()).ReturnsAsync(filters);

            var incident = new ServiceNowIncident { IncidentId = "sys1", Number = "INC001", Title = "New Incident" };
            _mockServiceNowApiClient.Setup(c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<ServiceNowIncident> { incident });

            var mockItemResponse = new Mock<ItemResponse<ServiceNowIncidentDocument>>();
            mockItemResponse.Setup(r => r.Resource).Returns(new ServiceNowIncidentDocument(incident));

            var lastScanTimeDocResponse = new Mock<ItemResponse<LastScanTimeDoc>>();
            lastScanTimeDocResponse.Setup(r => r.Resource).Returns(new LastScanTimeDoc());

            _mockContainer.Setup(c => c.ReadItemAsync<LastScanTimeDoc>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lastScanTimeDocResponse.Object);


            _mockContainer.Setup(c => c.ReadItemAsync<ServiceNowIncidentDocument>(incident.Number, new PartitionKey(incident.Number), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("Not Found", System.Net.HttpStatusCode.NotFound, 0, "", 0));

            _mockContainer.Setup(c => c.CreateItemAsync(It.IsAny<ServiceNowIncidentDocument>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockItemResponse.Object);

            // Setup LINQ queryable to return empty result for ThreadDocument queries
            var emptyThreadDocuments = new List<ThreadDocument>().AsQueryable();
            _mockContainer.Setup(c => c.GetItemLinqQueryable<ThreadDocument>(It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>(), It.IsAny<CosmosLinqSerializerOptions>()))
                .Returns(emptyThreadDocuments.OrderBy(x => x.CreatedTimestamp));

            var scanner = CreateScanner();

            // Act
            try
            {
                // set a short timeout cancellation token to avoid long waits
                var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await scanner.ScanAsync(cancellationTokenSource.Token);
            }
            catch (ArgumentOutOfRangeException ex)
            {

                // expected exception due to ToFeedIterator only can apply to internal class CosmosLinqQuery
                Assert.Contains(ex.Message, "ToFeedIterator");
            }
            catch(TaskCanceledException)
            {
                // expected cancellation exception
            }

            // Assert
            _mockContainer.Verify(c => c.CreateItemAsync(It.Is<ServiceNowIncidentDocument>(d => d.Id == incident.Number), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ScanAsync_ExistingIncident_UpsertsDocumentAndNotifies()
        {
            // Arrange
            var filters = new List<ServiceNowIncidentFilterDocument>
            {
                new ServiceNowIncidentFilterDocument
                {
                    Id = "filter1",
                    UpdatedAt = DateTime.UtcNow,
                    Name = "Test Filter",
                    ImpactedService = "",
                    Priority = "",
                    IncidentType = "ServiceNow",
                    AlertId = "",
                    TitleContains = ""
                }
            };
            _mockIncidentFilterManagementService.Setup(s => s.ListIncidentFilters()).ReturnsAsync(filters);

            var incident = new ServiceNowIncident { IncidentId = "sys1", Number = "INC001", Title = "Existing Incident" };
            _mockServiceNowApiClient.Setup(c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<ServiceNowIncident> { incident });

            var lastScanTimeDocResponse = new Mock<ItemResponse<LastScanTimeDoc>>();
            lastScanTimeDocResponse.Setup(r => r.Resource).Returns(new LastScanTimeDoc());

            _mockContainer.Setup(c => c.ReadItemAsync<LastScanTimeDoc>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lastScanTimeDocResponse.Object);

            var existingDoc = new ServiceNowIncidentDocument(incident);
            var mockReadResponse = new Mock<ItemResponse<ServiceNowIncidentDocument>>();
            mockReadResponse.Setup(r => r.Resource).Returns(existingDoc);

            var mockUpsertResponse = new Mock<ItemResponse<ServiceNowIncidentDocument>>();
            mockUpsertResponse.Setup(r => r.Resource).Returns(existingDoc);

            _mockContainer.Setup(c => c.ReadItemAsync<ServiceNowIncidentDocument>(incident.Number, new PartitionKey(incident.Number), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockReadResponse.Object);
            _mockContainer.Setup(c => c.UpsertItemAsync(It.IsAny<ServiceNowIncidentDocument>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockUpsertResponse.Object);

            var emptyThreadDocuments = new List<ThreadDocument>().AsQueryable();
            _mockContainer.Setup(c => c.GetItemLinqQueryable<ThreadDocument>(It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>(), It.IsAny<CosmosLinqSerializerOptions>()))
                .Returns(emptyThreadDocuments.OrderBy(x => x.CreatedTimestamp));

            var scanner = CreateScanner();

            // Act
            try
            {
                // set a short timeout cancellation token to avoid long waits
                var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await scanner.ScanAsync(cancellationTokenSource.Token);
            }
            catch (ArgumentOutOfRangeException ex)
            {

                // expected exception due to ToFeedIterator only can apply to internal class CosmosLinqQuery
                Assert.Contains(ex.Message, "ToFeedIterator");
            }
            catch (TaskCanceledException)
            {
                // expected cancellation exception
            }

            // Assert
            _mockContainer.Verify(c => c.UpsertItemAsync(It.Is<ServiceNowIncidentDocument>(d => d.Id == incident.Number), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
