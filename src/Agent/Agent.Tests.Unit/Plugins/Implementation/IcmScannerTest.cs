using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Plugins.Interface;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents.Scanner;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.SREAgent.Incidents.IcM.Model;
using Moq;
using Incident = Microsoft.SREAgent.Incidents.IcM.Model.ICMIncident;

namespace Agent.Tests.Unit.Plugins.Implementation;
public class IcmScannerTest
{
    private readonly Mock<ILogger<IcmScanner>> _mockLogger;
    private readonly Mock<IICMAPIClient> _mockIcmApiClient;
    private readonly Mock<CosmosClient> _mockCosmosClient;
    private readonly Mock<IIncidentHandlingService<IcmIncidentFilterDocumentPayload>> _mockIncidentHandlingService;
    private readonly Mock<IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload>> _mockIncidentManagementService;
    private readonly Mock<IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload>> _mockIncidentFilterManagementService;
    private readonly Mock<IAgentInboundCommunicationService> _mockAgentInboundCommunicationService;
    private readonly Mock<IAgentOutboundCommunicationService> _mockAgentOutboundCommunicationService;
    private readonly Mock<IIncidentAnalysisService<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload, Incident>> _mockIncidentAnalysisService;
    private readonly Mock<IICMPlugin> _mockIcmPlugin;
    private readonly Mock<Container> _mockContainer;
    private readonly IncidentManagementSettings _incidentManagementSettings;
    private readonly CosmosDBSettings _cosmosDbSettings;

    public IcmScannerTest()
    {
        _mockLogger = new Mock<ILogger<IcmScanner>>();
        _mockIcmApiClient = new Mock<IICMAPIClient>();
        _mockCosmosClient = new Mock<CosmosClient>();
        _mockContainer = new Mock<Container>();
        _mockIncidentHandlingService = new Mock<IIncidentHandlingService<IcmIncidentFilterDocumentPayload>>();
        _mockIncidentManagementService = new Mock<IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload>>();
        _mockIncidentFilterManagementService = new Mock<IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload>>();
        _mockAgentInboundCommunicationService = new Mock<IAgentInboundCommunicationService>();
        _mockAgentOutboundCommunicationService = new Mock<IAgentOutboundCommunicationService>();
        _mockIncidentAnalysisService = new Mock<IIncidentAnalysisService<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload, Incident>>();
        _mockIcmPlugin = new Mock<IICMPlugin>();
        _incidentManagementSettings = new IncidentManagementSettings
        {
            Type = IncidentManagementType.Icm,
            ICMAPI = new ICMAPISettings
            {
                APIEndpoint = "https://icmapi.example.com",
                CertificateSubjectName = "CN=ICMAPI",
                IcmMSIResource = "api://icmapi-prod",
                UserToken = "user-token",
                OwningServiceId = "owning-service-id"
            }
        };
        _cosmosDbSettings = new CosmosDBSettings
        {
            Docs = new DocsSettings
            {
                Database = "TestDb"
            }
        };

        _mockCosmosClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockContainer.Object);
    }

    private IcmScanner CreateScanner()
    {
        return new IcmScanner(
            _mockLogger.Object,
            _mockIcmApiClient.Object,
            _mockCosmosClient.Object,
            _cosmosDbSettings,
            _mockIncidentHandlingService.Object,
            _mockIncidentManagementService.Object,
            _mockIncidentFilterManagementService.Object,
            _mockAgentInboundCommunicationService.Object,
            _incidentManagementSettings,
            _mockIncidentAnalysisService.Object
        );
    }

    private Incident GetIncident(string title, string id = "123456789")
    {
        return new Incident()
        {
            Id = long.Parse(id),
            Type = IncidentType.LiveSite.ToString(), // Replace with a valid IncidentType value
            HitCount = 1,
            CreatedBy = "user@example.com",
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow,
            State = "ACTIVE",
            OwningTeamName = "",
            Severity = 3,
            Title = title,
            Keywords = "test,incident",
            Summary = "This is a test incident",
            SubscriptionId = "",
            Tags = new[] { "" }
        };
    }

    private IcmIncidentFilterDocument GetIncidentFilter(string titleContains)
    {
        return new IcmIncidentFilterDocument
        {
            Id = "filter1",
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Filter",
            ImpactedService = "",
            Priority = "",
            IncidentType = "",
            AlertId = "",
            TitleContains = titleContains,
            IsEnabled = true,
            AgentMode = ""
        };
    }

    [Fact]
    public async Task ScanAsync_NoFilters_SkipsScan()
    {
        // Arrange
        var scanner = CreateScanner();
        _mockIncidentFilterManagementService.Setup(f => f.ListIncidentFilters(It.IsAny<bool>()))
            .ReturnsAsync(new List<IcmIncidentFilterDocument>());

        var lastScanTimeDocResponse = new Mock<ItemResponse<LastScanTimeDoc>>();
        lastScanTimeDocResponse.Setup(r => r.Resource).Returns(new LastScanTimeDoc());
        _mockContainer.Setup(c => c.ReadItemAsync<LastScanTimeDoc>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lastScanTimeDocResponse.Object);

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

        _mockIcmApiClient.Verify(
                c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()),
                Times.Never);
    }

    [Fact]
    public async Task ScanAsync_WithNonIcmFilter_SkipsScanForThatFilter()
    {
        var title = "Test Incident";
        var allFilters = new List<IcmIncidentFilterDocument> { GetIncidentFilter(title) };

        //Wrong filter type
        var filters = allFilters.Where(f => f.DocumentType == IncidentFilterDocumentUtilities.GetDocumentTypeName(IncidentManagementType.ServiceNow)).ToList();

        _mockIncidentFilterManagementService.Setup(s => s.ListIncidentFilters(It.IsAny<bool>())).ReturnsAsync(filters);

        var incident = GetIncident(title);
        _mockIcmApiClient.Setup(c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()))
            .ReturnsAsync(new List<Incident> { incident });

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
        _mockIcmApiClient.Verify(
            c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ScanAsync_NewIncident_CreatesDocument()
    {
        var title = "Test Incident";
        var filters = new List<IcmIncidentFilterDocument> { GetIncidentFilter(title) };

        _mockIncidentFilterManagementService.Setup(s => s.ListIncidentFilters(It.IsAny<bool>())).ReturnsAsync(filters);

        var incident = GetIncident(title);
        _mockIcmApiClient.Setup(c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()))
            .ReturnsAsync(new List<Incident> { incident });

        var lastScanTimeDocResponse = new Mock<ItemResponse<LastScanTimeDoc>>();
        lastScanTimeDocResponse.Setup(r => r.Resource).Returns(new LastScanTimeDoc());

        var mockItemResponse = new Mock<ItemResponse<IcmIncidentDocument>>();
        mockItemResponse.Setup(r => r.Resource).Returns(new IcmIncidentDocument(incident));

        _mockContainer.Setup(c => c.ReadItemAsync<LastScanTimeDoc>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lastScanTimeDocResponse.Object);

        _mockContainer.Setup(c => c.ReadItemAsync<IcmIncidentDocument>(incident.Id.ToString(), new PartitionKey(incident.Id.ToString()), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("Not Found", System.Net.HttpStatusCode.NotFound, 0, "", 0));

        _mockContainer.Setup(c => c.CreateItemAsync(It.IsAny<IcmIncidentDocument>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(mockItemResponse.Object);

        _mockIncidentAnalysisService.Setup(c => c.AnalyzeIncident(It.IsAny<IcmIncidentDocument>(), It.IsAny<Incident>(), It.IsAny<IcmIncidentFilterDocument?>())).ReturnsAsync(mockItemResponse.Object);

        var emptyThreadDocuments = new List<ThreadDocument>().AsQueryable();
        _mockContainer.Setup(c => c.GetItemLinqQueryable<ThreadDocument>(It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>(), It.IsAny<CosmosLinqSerializerOptions>()))
            .Returns(emptyThreadDocuments.OrderBy(x => x.CreatedTimestamp));

        var scanner = CreateScanner();

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
        _mockContainer.Verify(c => c.CreateItemAsync(It.Is<IcmIncidentDocument>(d => d.Id == incident.Id.ToString()), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ScanAsync_ExistingIncident_UpsertsDocumentAndNotifies()
    {
        string title = "Test Incident";
        string summary = "This is a test incident summary";
        var filters = new List<IcmIncidentFilterDocument> { GetIncidentFilter(title) };

        var oldIncident = GetIncident(title);

        var newIncident = GetIncident(title);
        newIncident.Summary = summary; // Update the summary to simulate a change

        _mockIncidentFilterManagementService.Setup(s => s.ListIncidentFilters(It.IsAny<bool>())).ReturnsAsync(filters);

        _mockIcmApiClient.Setup(c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()))
            .ReturnsAsync(new List<Incident> { newIncident });

        var lastScanTimeDocResponse = new Mock<ItemResponse<LastScanTimeDoc>>();
        lastScanTimeDocResponse.Setup(r => r.Resource).Returns(new LastScanTimeDoc());
        _mockContainer.Setup(c => c.ReadItemAsync<LastScanTimeDoc>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lastScanTimeDocResponse.Object);


        var existingDoc = new IcmIncidentDocument(oldIncident);
        var mockReadResponse = new Mock<ItemResponse<IcmIncidentDocument>>();
        mockReadResponse.Setup(r => r.Resource).Returns(existingDoc);

        var newDoc = new IcmIncidentDocument(newIncident);
        var mockUpsertResponse = new Mock<ItemResponse<IcmIncidentDocument>>();
        mockUpsertResponse.Setup(r => r.Resource).Returns(newDoc);


        _mockContainer.Setup(c => c.ReadItemAsync<IcmIncidentDocument>(oldIncident.Id.ToString(), new PartitionKey(oldIncident.Id.ToString()), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockReadResponse.Object);
        _mockContainer.Setup(c => c.UpsertItemAsync(It.IsAny<IcmIncidentDocument>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
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

        // Assert, check if the incident summary was upserted
        _mockContainer.Verify(c => c.UpsertItemAsync(It.Is<IcmIncidentDocument>(d => d.Id == newIncident.Id.ToString() && d.Summary == summary), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
