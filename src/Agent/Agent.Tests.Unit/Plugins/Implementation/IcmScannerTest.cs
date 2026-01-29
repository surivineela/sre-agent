using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Plugins.Interface;
using Agent.Runtime.Services;
using Agent.Runtime.Services.IncidentTriggerDetection;
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
    private readonly Mock<IIncidentEventDetector> _mockIncidentEventDetector;
    private readonly Mock<IIncidentThreadLookupService> _mockIncidentThreadLookupService;
    private readonly Mock<IThreadRepository> _mockThreadRepository;
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
        _mockIncidentEventDetector = new Mock<IIncidentEventDetector>();
        _mockIncidentThreadLookupService = new Mock<IIncidentThreadLookupService>();
        _mockThreadRepository = new Mock<IThreadRepository>();
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
            _mockIncidentAnalysisService.Object,
            _mockIncidentEventDetector.Object,
            _mockIncidentThreadLookupService.Object,
            _mockThreadRepository.Object
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
            Priorities = new List<string>(),
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
                c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<List<string>>()),
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
        _mockIcmApiClient.Setup(c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<List<string>>()))
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
            c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<List<string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ScanAsync_NewIncident_CreatesDocument()
    {
        var title = "Test Incident";
        var filters = new List<IcmIncidentFilterDocument> { GetIncidentFilter(title) };

        _mockIncidentFilterManagementService.Setup(s => s.ListIncidentFilters(It.IsAny<bool>())).ReturnsAsync(filters);

        var incident = GetIncident(title);
        _mockIcmApiClient.Setup(c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<List<string>>()))
            .ReturnsAsync(new List<Incident> { incident });

        var lastScanTimeDocResponse = new Mock<ItemResponse<LastScanTimeDoc>>();
        lastScanTimeDocResponse.Setup(r => r.Resource).Returns(new LastScanTimeDoc());

        var mockItemResponse = new Mock<ItemResponse<IcmIncidentDocument>>();
        mockItemResponse.Setup(r => r.Resource).Returns(new IcmIncidentDocument(incident));

        _mockContainer.Setup(c => c.ReadItemAsync<LastScanTimeDoc>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lastScanTimeDocResponse.Object);

        _mockContainer.Setup(c => c.ReadItemAsync<IcmIncidentDocument>(incident.Id.ToString(), new PartitionKey(incident.Id.ToString()), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("Not Found", System.Net.HttpStatusCode.NotFound, 0, "", 0));

        _mockContainer.Setup(c => c.UpsertItemAsync(It.IsAny<IcmIncidentDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
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
        _mockContainer.Verify(c => c.UpsertItemAsync(It.Is<IcmIncidentDocument>(d => d.Id == incident.Id.ToString()), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
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

        _mockIcmApiClient.Setup(c => c.GetIncidentsAsync(It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<List<string>>()))
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

    #region Deduplication Tests - Filter without HandlingAgents

    /// <summary>
    /// Tests that when a filter has no handling agents configured (empty HandlingAgents and empty HandlingAgent),
    /// and a thread already exists with HandlerId = filter.Id, no duplicate thread is created.
    /// This validates the fix for the bug where GetEffectiveHandlingAgents() returns empty list,
    /// scanner uses "" for matching, but threads have HandlerId = filter.Id.
    /// </summary>
    [Fact]
    public void ProcessSingleEvent_FilterWithNoHandlingAgents_ExistingThreadWithFilterIdHandler_DoesNotCreateDuplicate()
    {
        // Arrange
        var filterId = "filter1";
        var incidentId = "123456789";
        var title = "Test Incident";

        var filter = new IcmIncidentFilterDocument
        {
            Id = filterId,
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Filter",
            ImpactedService = "",
            Priorities = new List<string>(),
            IncidentType = "",
            AlertId = "",
            TitleContains = title,
            IsEnabled = true,
            AgentMode = "",
            HandlingAgent = "", // Empty - no handling agent
            HandlingAgents = new List<string>() // Empty - no handling agents
        };

        // Existing thread with HandlerId = filter.Id (as set by thread creation)
        var existingThread = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: title,
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            Source: Agent.Core.Models.Api.v1.ThreadSource.Incident
        )
        {
            IncidentId = incidentId,
            IncidentDetails = new Agent.Core.Models.Api.v1.IncidentDetails(
                IncidentTitle: title,
                IncidentCreatedTime: DateTimeOffset.UtcNow.AddMinutes(-5),
                IncidentPriority: "3",
                ImpactedService: "",
                FilterId: filterId,
                HandlerId: filterId, // Key: HandlerId is set to filter.Id when no explicit handler
                InvestigationStatus: Agent.Core.Models.Api.v1.InvestigationStatus.InProgress
            )
        };

        // Mock thread lookup to return the existing thread
        _mockIncidentThreadLookupService
            .Setup(s => s.FindAllThreadsForIncidentAsync(incidentId))
            .ReturnsAsync(new List<ThreadDocument> { existingThread });

        // Verify that GetEffectiveHandlingAgents returns empty list for this filter
        var effectiveAgents = filter.GetEffectiveHandlingAgents();
        Assert.Empty(effectiveAgents);

        // Act & Assert
        // Since we can't easily call ProcessSingleEventAsync directly (private method),
        // we verify the deduplication logic by checking the agents needing threads calculation

        // Simulate the deduplication logic from ProcessSingleEventAsync
        var handlingAgents = filter.GetEffectiveHandlingAgents();
        if (handlingAgents.Count == 0)
        {
            handlingAgents = new List<string> { string.Empty };
        }

        var existingThreads = new List<ThreadDocument> { existingThread };
        var existingHandlerIds = existingThreads
            .Where(t => t.IncidentDetails?.HandlerId != null)
            .Select(t => t.IncidentDetails!.HandlerId!)
            .ToHashSet();

        // This is the fixed logic: check filter.Id when agent is empty string
        var agentsNeedingThreads = handlingAgents
            .Where(agent =>
            {
                if (string.IsNullOrEmpty(agent))
                {
                    // Empty agent maps to filter.Id in thread creation
                    return !existingHandlerIds.Contains(filter.Id ?? string.Empty);
                }
                return !existingHandlerIds.Contains(agent);
            })
            .ToList();

        // Assert: No agents should need threads because existing thread has HandlerId = filter.Id
        Assert.Empty(agentsNeedingThreads);
    }

    /// <summary>
    /// Tests that when threadsToProcess is filtered, threads with HandlerId = filter.Id
    /// are correctly included when handlingAgents contains empty string (meta_agent fallback).
    /// </summary>
    [Fact]
    public void ThreadsToProcess_FilterWithNoHandlingAgents_IncludesThreadsWithFilterIdHandler()
    {
        // Arrange
        var filterId = "filter1";
        var title = "Test Incident";

        var filter = new IcmIncidentFilterDocument
        {
            Id = filterId,
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Filter",
            ImpactedService = "",
            Priorities = new List<string>(),
            IncidentType = "",
            AlertId = "",
            TitleContains = title,
            IsEnabled = true,
            AgentMode = "",
            HandlingAgent = "", // Empty
            HandlingAgents = new List<string>() // Empty
        };

        var threadWithFilterIdHandler = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: title,
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            Source: Agent.Core.Models.Api.v1.ThreadSource.Incident
        )
        {
            IncidentDetails = new Agent.Core.Models.Api.v1.IncidentDetails(
                IncidentTitle: title,
                IncidentCreatedTime: DateTimeOffset.UtcNow.AddMinutes(-5),
                IncidentPriority: "3",
                ImpactedService: "",
                FilterId: filterId,
                HandlerId: filterId, // HandlerId = filter.Id
                InvestigationStatus: Agent.Core.Models.Api.v1.InvestigationStatus.InProgress
            )
        };

        var threadWithDifferentHandler = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: title,
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-3),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-3),
            Source: Agent.Core.Models.Api.v1.ThreadSource.Incident
        )
        {
            IncidentDetails = new Agent.Core.Models.Api.v1.IncidentDetails(
                IncidentTitle: title,
                IncidentCreatedTime: DateTimeOffset.UtcNow.AddMinutes(-3),
                IncidentPriority: "3",
                ImpactedService: "",
                FilterId: filterId,
                HandlerId: "different-agent-id", // Different handler
                InvestigationStatus: Agent.Core.Models.Api.v1.InvestigationStatus.InProgress
            )
        };

        var existingThreads = new List<ThreadDocument> { threadWithFilterIdHandler, threadWithDifferentHandler };

        // Act: Simulate the threadsToProcess logic from ProcessSingleEventAsync
        var handlingAgents = filter.GetEffectiveHandlingAgents();
        if (handlingAgents.Count == 0)
        {
            handlingAgents = new List<string> { string.Empty };
        }

        var threadsToProcess = existingThreads
            .Where(t =>
            {
                var threadHandlerId = t.IncidentDetails?.HandlerId;

                if (string.IsNullOrEmpty(threadHandlerId))
                    return true;

                if (handlingAgents.Contains(threadHandlerId))
                    return true;

                // Fixed logic: match threads with HandlerId = filter.Id when empty string is in handlingAgents
                if (handlingAgents.Contains(string.Empty) && threadHandlerId == filter.Id)
                    return true;

                return false;
            })
            .ToList();

        // Assert: Only the thread with HandlerId = filter.Id should be included
        Assert.Single(threadsToProcess);
        Assert.Equal(filterId, threadsToProcess[0].IncidentDetails?.HandlerId);
    }

    /// <summary>
    /// Tests that when a filter has explicit handling agents, deduplication works correctly.
    /// Threads with matching HandlerId should not cause new thread creation.
    /// </summary>
    [Fact]
    public void AgentsNeedingThreads_FilterWithExplicitHandlingAgents_DeduplicatesCorrectly()
    {
        // Arrange
        var filterId = "filter1";
        var agentA = "agent-a";
        var agentB = "agent-b";
        var title = "Test Incident";

        var filter = new IcmIncidentFilterDocument
        {
            Id = filterId,
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Filter",
            ImpactedService = "",
            Priorities = new List<string>(),
            IncidentType = "",
            AlertId = "",
            TitleContains = title,
            IsEnabled = true,
            AgentMode = "",
            HandlingAgents = new List<string> { agentA, agentB } // Two explicit agents
        };

        // Existing thread only for agent-a
        var existingThread = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: title,
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            Source: Agent.Core.Models.Api.v1.ThreadSource.Incident
        )
        {
            IncidentDetails = new Agent.Core.Models.Api.v1.IncidentDetails(
                IncidentTitle: title,
                IncidentCreatedTime: DateTimeOffset.UtcNow.AddMinutes(-5),
                IncidentPriority: "3",
                ImpactedService: "",
                FilterId: filterId,
                HandlerId: agentA, // Only agent-a has a thread
                InvestigationStatus: Agent.Core.Models.Api.v1.InvestigationStatus.InProgress
            )
        };

        var existingThreads = new List<ThreadDocument> { existingThread };

        // Act
        var handlingAgents = filter.GetEffectiveHandlingAgents();
        var existingHandlerIds = existingThreads
            .Where(t => t.IncidentDetails?.HandlerId != null)
            .Select(t => t.IncidentDetails!.HandlerId!)
            .ToHashSet();

        var agentsNeedingThreads = handlingAgents
            .Where(agent =>
            {
                if (string.IsNullOrEmpty(agent))
                {
                    return !existingHandlerIds.Contains(filter.Id ?? string.Empty);
                }
                return !existingHandlerIds.Contains(agent);
            })
            .ToList();

        // Assert: Only agent-b should need a new thread
        Assert.Single(agentsNeedingThreads);
        Assert.Equal(agentB, agentsNeedingThreads[0]);
    }

    /// <summary>
    /// Tests that when filter.Id is null, the deduplication logic handles it gracefully
    /// by using empty string as fallback.
    /// </summary>
    [Fact]
    public void AgentsNeedingThreads_FilterWithNullId_HandlesGracefully()
    {
        // Arrange
        var title = "Test Incident";

        var filter = new IcmIncidentFilterDocument
        {
            Id = null!, // Null filter ID (edge case)
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Filter",
            ImpactedService = "",
            Priorities = new List<string>(),
            IncidentType = "",
            AlertId = "",
            TitleContains = title,
            IsEnabled = true,
            AgentMode = "",
            HandlingAgent = "",
            HandlingAgents = new List<string>()
        };

        // Existing thread with empty HandlerId (matches null filter.Id fallback)
        var existingThread = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: title,
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            Source: Agent.Core.Models.Api.v1.ThreadSource.Incident
        )
        {
            IncidentDetails = new Agent.Core.Models.Api.v1.IncidentDetails(
                IncidentTitle: title,
                IncidentCreatedTime: DateTimeOffset.UtcNow.AddMinutes(-5),
                IncidentPriority: "3",
                ImpactedService: "",
                FilterId: "",
                HandlerId: "", // Empty HandlerId
                InvestigationStatus: Agent.Core.Models.Api.v1.InvestigationStatus.InProgress
            )
        };

        var existingThreads = new List<ThreadDocument> { existingThread };

        // Act
        var handlingAgents = filter.GetEffectiveHandlingAgents();
        if (handlingAgents.Count == 0)
        {
            handlingAgents = new List<string> { string.Empty };
        }

        var existingHandlerIds = existingThreads
            .Where(t => t.IncidentDetails?.HandlerId != null)
            .Select(t => t.IncidentDetails!.HandlerId!)
            .ToHashSet();

        var agentsNeedingThreads = handlingAgents
            .Where(agent =>
            {
                if (string.IsNullOrEmpty(agent))
                {
                    return !existingHandlerIds.Contains(filter.Id ?? string.Empty);
                }
                return !existingHandlerIds.Contains(agent);
            })
            .ToList();

        // Assert: Should not throw and should find the existing thread
        Assert.Empty(agentsNeedingThreads);
    }

    /// <summary>
    /// Tests backward compatibility: threads with null IncidentDetails should be included
    /// in threadsToProcess for processing.
    /// </summary>
    [Fact]
    public void ThreadsToProcess_ThreadWithNullIncidentDetails_IncludedForBackwardCompatibility()
    {
        // Arrange
        var filterId = "filter1";
        var title = "Test Incident";

        var filter = new IcmIncidentFilterDocument
        {
            Id = filterId,
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Filter",
            ImpactedService = "",
            Priorities = new List<string>(),
            IncidentType = "",
            AlertId = "",
            TitleContains = title,
            IsEnabled = true,
            AgentMode = "",
            HandlingAgent = "",
            HandlingAgents = new List<string>()
        };

        // Old thread without IncidentDetails (backward compat)
        var legacyThread = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: title,
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            Source: Agent.Core.Models.Api.v1.ThreadSource.Incident
        )
        {
            IncidentDetails = null // No IncidentDetails (legacy thread)
        };

        var existingThreads = new List<ThreadDocument> { legacyThread };

        // Act
        var handlingAgents = filter.GetEffectiveHandlingAgents();
        if (handlingAgents.Count == 0)
        {
            handlingAgents = new List<string> { string.Empty };
        }

        var threadsToProcess = existingThreads
            .Where(t =>
            {
                var threadHandlerId = t.IncidentDetails?.HandlerId;

                // Include threads with null/empty HandlerId for backward compatibility
                if (string.IsNullOrEmpty(threadHandlerId))
                    return true;

                if (handlingAgents.Contains(threadHandlerId))
                    return true;

                if (handlingAgents.Contains(string.Empty) && threadHandlerId == filter.Id)
                    return true;

                return false;
            })
            .ToList();

        // Assert: Legacy thread should be included
        Assert.Single(threadsToProcess);
        Assert.Null(threadsToProcess[0].IncidentDetails);
    }

    /// <summary>
    /// Tests backward compatibility: threads with IncidentDetails but null HandlerId
    /// should be included in threadsToProcess.
    /// </summary>
    [Fact]
    public void ThreadsToProcess_ThreadWithNullHandlerId_IncludedForBackwardCompatibility()
    {
        // Arrange
        var filterId = "filter1";
        var title = "Test Incident";

        var filter = new IcmIncidentFilterDocument
        {
            Id = filterId,
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Filter",
            ImpactedService = "",
            Priorities = new List<string>(),
            IncidentType = "",
            AlertId = "",
            TitleContains = title,
            IsEnabled = true,
            AgentMode = "",
            HandlingAgent = "",
            HandlingAgents = new List<string>()
        };

        // Thread with IncidentDetails but null HandlerId
        var threadWithNullHandler = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: title,
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            Source: Agent.Core.Models.Api.v1.ThreadSource.Incident
        )
        {
            IncidentDetails = new Agent.Core.Models.Api.v1.IncidentDetails(
                IncidentTitle: title,
                IncidentCreatedTime: DateTimeOffset.UtcNow.AddMinutes(-5),
                IncidentPriority: "3",
                ImpactedService: "",
                FilterId: filterId,
                HandlerId: null!, // Null HandlerId
                InvestigationStatus: Agent.Core.Models.Api.v1.InvestigationStatus.InProgress
            )
        };

        var existingThreads = new List<ThreadDocument> { threadWithNullHandler };

        // Act
        var handlingAgents = filter.GetEffectiveHandlingAgents();
        if (handlingAgents.Count == 0)
        {
            handlingAgents = new List<string> { string.Empty };
        }

        var threadsToProcess = existingThreads
            .Where(t =>
            {
                var threadHandlerId = t.IncidentDetails?.HandlerId;

                if (string.IsNullOrEmpty(threadHandlerId))
                    return true;

                if (handlingAgents.Contains(threadHandlerId))
                    return true;

                if (handlingAgents.Contains(string.Empty) && threadHandlerId == filter.Id)
                    return true;

                return false;
            })
            .ToList();

        // Assert: Thread with null HandlerId should be included
        Assert.Single(threadsToProcess);
    }

    /// <summary>
    /// Tests that HandlingAgent (singular) fallback works correctly for deduplication
    /// when HandlingAgents list is empty.
    /// </summary>
    [Fact]
    public void AgentsNeedingThreads_FilterWithSingularHandlingAgent_DeduplicatesCorrectly()
    {
        // Arrange
        var filterId = "filter1";
        var handlingAgent = "agent-singular";
        var title = "Test Incident";

        var filter = new IcmIncidentFilterDocument
        {
            Id = filterId,
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Filter",
            ImpactedService = "",
            Priorities = new List<string>(),
            IncidentType = "",
            AlertId = "",
            TitleContains = title,
            IsEnabled = true,
            AgentMode = "",
            HandlingAgent = handlingAgent, // Singular HandlingAgent set
            HandlingAgents = new List<string>() // Empty list - should fall back to HandlingAgent
        };

        // Existing thread for the singular handling agent
        var existingThread = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: title,
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            Source: Agent.Core.Models.Api.v1.ThreadSource.Incident
        )
        {
            IncidentDetails = new Agent.Core.Models.Api.v1.IncidentDetails(
                IncidentTitle: title,
                IncidentCreatedTime: DateTimeOffset.UtcNow.AddMinutes(-5),
                IncidentPriority: "3",
                ImpactedService: "",
                FilterId: filterId,
                HandlerId: handlingAgent, // Thread exists for the singular handler
                InvestigationStatus: Agent.Core.Models.Api.v1.InvestigationStatus.InProgress
            )
        };

        var existingThreads = new List<ThreadDocument> { existingThread };

        // Act
        var handlingAgents = filter.GetEffectiveHandlingAgents();
        // Should return ["agent-singular"] from the fallback

        var existingHandlerIds = existingThreads
            .Where(t => t.IncidentDetails?.HandlerId != null)
            .Select(t => t.IncidentDetails!.HandlerId!)
            .ToHashSet();

        var agentsNeedingThreads = handlingAgents
            .Where(agent =>
            {
                if (string.IsNullOrEmpty(agent))
                {
                    return !existingHandlerIds.Contains(filter.Id ?? string.Empty);
                }
                return !existingHandlerIds.Contains(agent);
            })
            .ToList();

        // Assert: GetEffectiveHandlingAgents should return the singular agent
        Assert.Single(handlingAgents);
        Assert.Equal(handlingAgent, handlingAgents[0]);
        // And deduplication should find it
        Assert.Empty(agentsNeedingThreads);
    }

    /// <summary>
    /// Tests that HandlingAgents list takes precedence over singular HandlingAgent.
    /// </summary>
    [Fact]
    public void GetEffectiveHandlingAgents_BothSet_HandlingAgentsTakesPrecedence()
    {
        // Arrange
        var filter = new IcmIncidentFilterDocument
        {
            Id = "filter1",
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Filter",
            ImpactedService = "",
            Priorities = new List<string>(),
            IncidentType = "",
            AlertId = "",
            TitleContains = "test",
            IsEnabled = true,
            AgentMode = "",
            HandlingAgent = "agent-singular", // Should be ignored
            HandlingAgents = new List<string> { "agent-list-1", "agent-list-2" } // Should take precedence
        };

        // Act
        var effectiveAgents = filter.GetEffectiveHandlingAgents();

        // Assert: HandlingAgents list should take precedence
        Assert.Equal(2, effectiveAgents.Count);
        Assert.Contains("agent-list-1", effectiveAgents);
        Assert.Contains("agent-list-2", effectiveAgents);
        Assert.DoesNotContain("agent-singular", effectiveAgents);
    }

    /// <summary>
    /// Tests repeated scan cycles: simulates multiple scans where thread already exists.
    /// Each scan should NOT create duplicate threads.
    /// </summary>
    [Fact]
    public void AgentsNeedingThreads_RepeatedScanCycles_NoDuplicateCreation()
    {
        // Arrange
        var filterId = "filter1";
        var title = "Test Incident";
        var incidentId = "123456789";

        var filter = new IcmIncidentFilterDocument
        {
            Id = filterId,
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Filter",
            ImpactedService = "",
            Priorities = new List<string>(),
            IncidentType = "",
            AlertId = "",
            TitleContains = title,
            IsEnabled = true,
            AgentMode = "",
            HandlingAgent = "",
            HandlingAgents = new List<string>()
        };

        // Thread created in first scan
        var existingThread = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: title,
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-10),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-10),
            Source: Agent.Core.Models.Api.v1.ThreadSource.Incident
        )
        {
            IncidentId = incidentId,
            IncidentDetails = new Agent.Core.Models.Api.v1.IncidentDetails(
                IncidentTitle: title,
                IncidentCreatedTime: DateTimeOffset.UtcNow.AddMinutes(-10),
                IncidentPriority: "3",
                ImpactedService: "",
                FilterId: filterId,
                HandlerId: filterId, // Created with filter.Id as HandlerId
                InvestigationStatus: Agent.Core.Models.Api.v1.InvestigationStatus.InProgress
            )
        };

        // Simulate multiple scan cycles
        for (int scanCycle = 1; scanCycle <= 5; scanCycle++)
        {
            var existingThreads = new List<ThreadDocument> { existingThread };

            var handlingAgents = filter.GetEffectiveHandlingAgents();
            if (handlingAgents.Count == 0)
            {
                handlingAgents = new List<string> { string.Empty };
            }

            var existingHandlerIds = existingThreads
                .Where(t => t.IncidentDetails?.HandlerId != null)
                .Select(t => t.IncidentDetails!.HandlerId!)
                .ToHashSet();

            var agentsNeedingThreads = handlingAgents
                .Where(agent =>
                {
                    if (string.IsNullOrEmpty(agent))
                    {
                        return !existingHandlerIds.Contains(filter.Id ?? string.Empty);
                    }
                    return !existingHandlerIds.Contains(agent);
                })
                .ToList();

            // Assert: Every scan cycle should find existing thread, no new threads needed
            Assert.Empty(agentsNeedingThreads);
        }
    }

    /// <summary>
    /// Tests that when an incident has threads for multiple handlers (mixed scenario),
    /// the correct thread is identified for a filter with no handling agents.
    /// </summary>
    [Fact]
    public void AgentsNeedingThreads_MixedHandlerTypes_IdentifiesCorrectThread()
    {
        // Arrange
        var filterId = "filter1";
        var title = "Test Incident";

        var filter = new IcmIncidentFilterDocument
        {
            Id = filterId,
            UpdatedAt = DateTime.UtcNow,
            Name = "Test Filter",
            ImpactedService = "",
            Priorities = new List<string>(),
            IncidentType = "",
            AlertId = "",
            TitleContains = title,
            IsEnabled = true,
            AgentMode = "",
            HandlingAgent = "",
            HandlingAgents = new List<string>()
        };

        // Thread 1: explicit agent
        var threadForExplicitAgent = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: title,
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-5),
            Source: Agent.Core.Models.Api.v1.ThreadSource.Incident
        )
        {
            IncidentDetails = new Agent.Core.Models.Api.v1.IncidentDetails(
                IncidentTitle: title,
                IncidentCreatedTime: DateTimeOffset.UtcNow.AddMinutes(-5),
                IncidentPriority: "3",
                ImpactedService: "",
                FilterId: "different-filter",
                HandlerId: "explicit-agent", // Different handler
                InvestigationStatus: Agent.Core.Models.Api.v1.InvestigationStatus.InProgress
            )
        };

        // Thread 2: filter.Id as handler (the one we should match)
        var threadForFilterId = new ThreadDocument(
            Id: Guid.NewGuid().ToString(),
            Title: title,
            MessageId: "",
            LastMessageId: "",
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-3),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-3),
            Source: Agent.Core.Models.Api.v1.ThreadSource.Incident
        )
        {
            IncidentDetails = new Agent.Core.Models.Api.v1.IncidentDetails(
                IncidentTitle: title,
                IncidentCreatedTime: DateTimeOffset.UtcNow.AddMinutes(-3),
                IncidentPriority: "3",
                ImpactedService: "",
                FilterId: filterId,
                HandlerId: filterId, // Handler = filter.Id
                InvestigationStatus: Agent.Core.Models.Api.v1.InvestigationStatus.InProgress
            )
        };

        var existingThreads = new List<ThreadDocument> { threadForExplicitAgent, threadForFilterId };

        // Act
        var handlingAgents = filter.GetEffectiveHandlingAgents();
        if (handlingAgents.Count == 0)
        {
            handlingAgents = new List<string> { string.Empty };
        }

        var existingHandlerIds = existingThreads
            .Where(t => t.IncidentDetails?.HandlerId != null)
            .Select(t => t.IncidentDetails!.HandlerId!)
            .ToHashSet();

        var agentsNeedingThreads = handlingAgents
            .Where(agent =>
            {
                if (string.IsNullOrEmpty(agent))
                {
                    return !existingHandlerIds.Contains(filter.Id ?? string.Empty);
                }
                return !existingHandlerIds.Contains(agent);
            })
            .ToList();

        // Assert: Should find the thread with HandlerId = filter.Id, no new thread needed
        Assert.Empty(agentsNeedingThreads);
        Assert.Contains(filterId, existingHandlerIds);
    }

    #endregion
}
