// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Data.DataModels;
using Agent.Data.DataModels.IncidentModel;
using Agent.Data.Interface.IncidentAPI;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.Services;
using Azure.Core;
using Azure.ResourceManager.AlertsManagement.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Author = Agent.Core.Models.Api.v1.Author;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using IncidentDetails = Agent.Core.Models.Api.v1.IncidentDetails;
using Message = Agent.Core.Models.Api.v1.Message;
using Thread = Agent.Core.Models.Api.v1.Thread;

public class AzMonitorIncidentHandlingService : IncidentHandlingServiceBase<AzMonitorAlertDocument, AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload>
{

    private readonly IIncidentManagementService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload> _incidentManagementService;
    private readonly IAzMonitorAlertService _azMonitorAlertService;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly Container _dbContainer;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly IIncidentStatusMetricsService _incidentsStatusMetricsService;
    private readonly IThreadRepository _repository;
    private readonly IAgentInboundCommunicationService _inboundCommunicationService;
    private readonly ExperimentalSettings _experimentalSettings;
    private readonly IIncidentAnalysisService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload, AlertItem> _incidentAnalysisService;

    private IncidentManagementType IncidentType => IncidentManagementType.AzMonitor;

    public AzMonitorIncidentHandlingService(
        IIncidentFilterManagementService<AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentManagementService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload> incidentManagementService,
        IAzMonitorAlertService azMonitorAlertService,
        ILogger<AzMonitorIncidentHandlingService> logger,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        IAgentOutboundCommunicationService outboundCommunicationService,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IGraphDatabaseClient graphDbClient,
        IIncidentStatusMetricsService incidentsStatusMetricsService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IIncidentStatusMetricsService incidentStatusMetricsService,
        IAgentOutboundCommunicationService agentOutboundCommunicationService,
        IIncidentAnalysisService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload, AlertItem> incidentAnalysisService,
        Tracer tracer,
        IAgentFactory<AgentContext> agentFactory,
        ExperimentalSettings experimentalSettings
    ) : base(incidentFilterManagementService, incidentHandlerManagementService, agentFactory, logger)
    {
        _incidentManagementService = incidentManagementService;
        _azMonitorAlertService = azMonitorAlertService;
        _outboundCommunicationService = outboundCommunicationService;
        _dbContainer = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
        _graphDbClient = graphDbClient;
        _incidentsStatusMetricsService = incidentsStatusMetricsService;
        _repository = repository;
        _inboundCommunicationService = inboundCommunicationService;
        _experimentalSettings = experimentalSettings;
        _incidentAnalysisService = incidentAnalysisService;
    }

    protected override async Task<AzMonitorAlertDocument> GetIncidentAsync(string incidentId)
    {
        _logger.LogInternalInformation("[AzMonitorIncidentHandlingService] GetIncidentAsync: Invoked for IncidentId: {IncidentId}", incidentId);
        try
        {
            var incidentGUID = new ResourceIdentifier(incidentId).Name ?? incidentId;
            var incidentDoc = await _incidentManagementService.GetIncidentDetails(incidentGUID);
            if (incidentDoc is null)
            {
                _logger.LogInternalWarning("[AzMonitorIncidentHandlingService] GetIncidentAsync: No incident data found for IncidentId: {IncidentId}, fetching latest", incidentId);
                var lastestIncident = await _azMonitorAlertService.GetIncidentAsync(incidentId);
                return AzMonitorAlertDocument.FromIncident(lastestIncident);
            }
            _logger.LogInternalInformation("[AzMonitorIncidentHandlingService] GetIncidentAsync: Returning incident data for IncidentId: {IncidentId}", incidentId);
            return incidentDoc;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[AzMonitorIncidentHandlingService] GetIncidentAsync: Error occurred for IncidentId: {IncidentId}", incidentId);
            throw;
        }
    }

    protected async override Task<IncidentHandlingResponseModel> HandleIncidentInternalAsync(AzMonitorAlertDocument incidentDetails, AzMonitorIncidentFilterDocumentPayload filterPayload, IncidentHandlerDocumentPayload? handler, IncidentHandlingRequestModelBase request)
    {
        var isTest = request.IsTest;
        var incidentId = request.IncidentId;

        try
        {
            var incident = AzMonitorAlertDocument.ToIncidentItem(incidentDetails);

            if (isTest)
            {
                _logger.LogInternalInformation("[AzMonitorIncidentHandlingService] HandleIncidentAsync: Processing test incident for IncidentId: {IncidentId}", incidentId);

                var (thread, agentContext) = await CreateHandlerAwareIncidentThread(incident, handler, filterPayload);

                await InitiateIncidentInvestigationAsync(incident, thread, agentContext, handler);

                try
                {
                    var data = ToIncidentActivitySnapshot(filterPayload, incidentDetails, request, handler);
                    _incidentAnalysisService.Ingest(data);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError($"[AzMonitorIncidentHandlingService] HandleIncidentAsync: Error logging incident handling data to Incident Analysis Service; {ex.Message}");
                }

                _logger.LogInternalInformation("[AzMonitorIncidentHandlingService] HandleIncidentAsync: Created test thread with ThreadId: {ThreadId} for IncidentId: {IncidentId}", thread.Id, incidentId);

                return new IncidentHandlingResponseModel()
                {
                    StatusCode = 200,
                    Response = new { threadId = thread.Id, message = "Test incident received" }
                };
            }
            else
            {
                _logger.LogInternalInformation("[AzMonitorIncidentHandlingService] HandleIncidentAsync: Processing production incident for IncidentId: {IncidentId}", incidentId);

                var maxAttempts = filterPayload.MaxAutomatedInvestigationAttempts;

                var threadId = await ProcessAlertAsync(incident, maxAttempts, handler, filterPayload);

                if (threadId.HasValue)
                {
                    _logger.LogInternalInformation("[AzMonitorIncidentHandlingService] HandleIncidentAsync: Processed incident with ThreadId: {ThreadId} for IncidentId: {IncidentId}", threadId.Value, incidentId);


                    try
                    {
                        var data = ToIncidentActivitySnapshot(filterPayload, incidentDetails, request, handler);
                        _incidentAnalysisService.Ingest(data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError($"[AzMonitorIncidentHandlingService] HandleIncidentAsync: Error logging incident handling data to Incident Analysis Service; {ex.Message}");
                    }

                    return new IncidentHandlingResponseModel()
                    {
                        StatusCode = 200,
                        Response = new { threadId = threadId.Value, message = "Incident received" }
                    };
                }
                else
                {
                    _logger.LogInternalWarning("[AzMonitorIncidentHandlingService] HandleIncidentAsync: ProcessAlertAsync returned null threadId for IncidentId: {IncidentId}", incidentId);

                    return new IncidentHandlingResponseModel()
                    {
                        StatusCode = 500,
                        Response = "Failed to get thread ID for incident"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[AzMonitorIncidentHandlingService] HandleIncidentAsync: Error processing IncidentId: {IncidentId}", incidentId);

            return new IncidentHandlingResponseModel()
            {
                StatusCode = 500,
                Response = "Failed to process Incident"
            };
        }
    }

    #region ProcessIncident
    public async Task<Guid?> ProcessAlertAsync(AlertItem alert, int maxAutomatedInvestigationAttempts, IncidentHandlerDocumentPayload? handlerDoc, AzMonitorIncidentFilterDocumentPayload? filterPayload = null)
    {
        try
        {
            // Acknowledge the alert first
            await _azMonitorAlertService.AcknowledgeAlert(alert.Id);

            // Check for existing thread and handle deduplication
            var (ShouldReturn, ExistingThread) = await HandleExistingThreadAsync(alert, maxAutomatedInvestigationAttempts);
            if (ShouldReturn)
            {
                if (ExistingThread != null)
                {
                    return ExistingThread.Id;
                }
                else
                {
                    _logger.LogInternalWarning($"[AzMonitorIncidentHandlingService] HandleExistingThreadAsync returned ShouldReturn=true but ExistingThread is null for alert {alert.Id}");
                    // Continue with normal processing to create a new thread
                }
            }

            // Process new alert (save to databases)
            await ProcessNewAlertAsync(alert);

            // Handle thread creation based on resource requirements
            var threadId = await CreateAndInitializeThreadAsync(alert, handlerDoc, filterPayload);
            return threadId;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[AzMonitorIncidentHandlingService]Error processing alert {alert.Id}: {ex.Message}");
            return null;
        }
    }

    #region ProcessAlert Helper Methods

    private async Task<(bool ShouldReturn, Thread? ExistingThread)> HandleExistingThreadAsync(
        AlertItem alert,
        int maxAutomatedInvestigationAttempts)
    {
        var existingActiveThread = await FindExistingActiveThreadForAlertRule(alert);

        if (existingActiveThread == null)
        {
            return (false, null);
        }

        var alertDocumentId = existingActiveThread?.Status?.IncidentStatus?.IncidentId;
        if (string.IsNullOrEmpty(alertDocumentId))
        {
            _logger.LogInternalWarning($"[AzMonitorIncidentHandlingService] No alert document ID found for existing thread {existingActiveThread?.Id}");
            return (false, null);
        }

        // If target resource is application insights or log analytics workspace, prompt user for additional input.
        if (await ShouldSkipDueToTargetResourceInputAsync(alert, alertDocumentId))
        {
            return (true, existingActiveThread);
        }

        // Handle retry investigation logic and user input prompting
        var shouldReturn = await HandleRetryLogicAsync(
            alert, alertDocumentId, maxAutomatedInvestigationAttempts, existingActiveThread!);

        return (shouldReturn, existingActiveThread);
    }

    private async Task<bool> ShouldSkipDueToTargetResourceInputAsync(AlertItem alert, string alertDocumentId)
    {
        if (!RequiresTargetResourceInput(alert))
        {
            return false;
        }

        var existingAlertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alertDocumentId, alertDocumentId);
        if (existingAlertDocument != null && existingAlertDocument.TargetResourceInputRequested)
        {
            return true; // Skip if user input already requested
        }

        return false;
    }

    private async Task<bool> HandleRetryLogicAsync(
        AlertItem alert,
        string alertDocumentId,
        int maxRetryCount,
        Thread existingActiveThread)
    {
        var existingAlertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alertDocumentId, alertDocumentId);
        if (existingAlertDocument == null)
        {
            _logger.LogInternalWarning($"[AzMonitorIncidentHandlingService] Could not find alert document {alertDocumentId} for existing thread {existingActiveThread?.Id}");
            return false;
        }

        var currentHitCount = existingAlertDocument.HitCount;

        // Always update hit count
        await UpdateAlertHitCountAsync(existingAlertDocument);

        if (currentHitCount > maxRetryCount)
        {
            return await HandleRetryLimitExceededAsync(alert, existingAlertDocument, existingActiveThread);
        }
        else
        {
            await HandleRecurringAlertAsync(alert, existingActiveThread, currentHitCount, maxRetryCount);
            return true;
        }
    }

    private async Task UpdateAlertHitCountAsync(AzMonitorAlertDocument alertDocument)
    {
        var updatedDocument = alertDocument with
        {
            HitCount = alertDocument.HitCount + 1,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContainer.UpsertItemAsync(updatedDocument, new PartitionKey(updatedDocument.PartitionKey));
    }

    private async Task<bool> HandleRetryLimitExceededAsync(
        AlertItem alert,
        AzMonitorAlertDocument alertDocument,
        Thread existingActiveThread)
    {
        if (alertDocument.UserInputRequested)
        {
            return true; // Skip if user input already requested
        }

        _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Found existing active thread {existingActiveThread?.Id} for alert {alert.Id}. Retry limit reached. Requesting user input.");

        await RequestUserInputForRetryLimitAsync(alert, existingActiveThread!);
        await MarkUserInputRequestedAsync(alertDocument);
        return true;
    }

    private async Task RequestUserInputForRetryLimitAsync(AlertItem alert, Thread existingActiveThread)
    {
        var userInputMessage = $"The automated investigation has been completed multiple times but was unable to identify a definitive root cause.\n\n" +
            $"**Action Required:** Please provide additional context or manual investigation steps that might help resolve this recurring issue. Consider:\n" +
            $"- Recent changes not captured in logs\n" +
            $"- External dependencies or third-party services\n" +
            $"- Known issues with the affected system\n" +
            $"- Any manual remediation steps that have worked before\n" +
            $"- Configuration changes or deployments\n" +
            $"- Network connectivity issues\n" +
            $"- Resource scaling or capacity problems\n\n" +
            $"Please share any relevant information that could help identify the root cause.";

        var agentContexts = await _repository.GetAgentContextsForThreadAsync(existingActiveThread!.Id);
        var existingAgentContext = agentContexts.First();

        await PromptUserForInputAsync(existingActiveThread.Id, existingAgentContext, userInputMessage);
    }

    private async Task MarkUserInputRequestedAsync(AzMonitorAlertDocument alertDocument)
    {
        var updatedDocument = alertDocument with
        {
            UserInputRequested = true,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContainer.UpsertItemAsync(updatedDocument, new PartitionKey(updatedDocument.PartitionKey));
    }

    private async Task HandleRecurringAlertAsync(
        AlertItem alert,
        Thread existingActiveThread,
        int currentHitCount,
        int maxRetryCount)
    {
        _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Found existing active thread {existingActiveThread?.Id} with completed investigation for alert {alert.Id}. Appending recurring alert message. Count: {currentHitCount + 1}/{maxRetryCount}");

        var message = $"Another alert **{alert.Id}** is firing with the same alert rule. Merging the investigation.";

        if (existingActiveThread == null)
        {
            _logger.LogInternalWarning("[AzMonitorIncidentHandlingService] existingActiveThread is null when trying to get agent contexts.");
            return;
        }

        var agentContexts = await _repository.GetAgentContextsForThreadAsync(existingActiveThread.Id);
        var existingAgentContext = agentContexts.First();
        var incidentMetrics = await _incidentsStatusMetricsService.GetIncidentStatusMetricsAsync(null, DateTime.Now);

        await _outboundCommunicationService.NotifyIncidentStatusMetrics(existingActiveThread.Id, incidentMetrics);
        await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
            ThreadId: existingActiveThread.Id,
            AgentContextId: existingAgentContext.Id,
            MessageId: Guid.NewGuid(),
            Message: message,
            UserId: "agent-default",
            DisplayName: "Azure SRE Agent",
            Timestamp: DateTime.UtcNow
        ));

        _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Updated recurring alert count to {currentHitCount + 1} for alert document {existingActiveThread.Status?.IncidentStatus?.IncidentId}");
    }

    private async Task ProcessNewAlertAsync(AlertItem alert)
    {
        var saveToDocumentTask = SaveAlertToDocumentDb(alert);
        var saveToGraphTask = SaveAlertToGraphDb(alert);

        await Task.WhenAll(saveToDocumentTask, saveToGraphTask);
    }

    private async Task<Guid> CreateAndInitializeThreadAsync(
        AlertItem alert,
        IncidentHandlerDocumentPayload? handlerDoc,
        AzMonitorIncidentFilterDocumentPayload? filterPayload)
    {
        // Create incident thread with handler awareness
        var (thread, agentContext) = await CreateHandlerAwareIncidentThread(alert, handlerDoc, filterPayload);

        if (RequiresTargetResourceInput(alert))
        {
            await HandleTargetResourceInputRequestAsync(alert, thread, agentContext);
            return thread.Id;
        }

        await InitiateIncidentInvestigationAsync(alert, thread, agentContext, handlerDoc);
        return thread.Id;
    }

    private async Task HandleTargetResourceInputRequestAsync(
        AlertItem alert,
        Thread thread,
        AgentContext agentContext)
    {
        _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Alert {alert.Id} targets a {alert.Properties?.Essentials?.TargetResourceType} resource. Requesting user to specify the affected resource before investigation.");

        var targetResourceInputMessage = BuildTargetResourceInputMessage(alert);
        await PromptUserForInputAsync(thread.Id, agentContext, targetResourceInputMessage);
        await MarkTargetResourceInputRequestedAsync(alert.Id);
    }

    private string BuildTargetResourceInputMessage(AlertItem alert)
    {
        return $"This alert is targeting a **{alert.Properties?.Essentials?.TargetResourceType}** resource, which may impact the automated investigation.\n\n" +
            $"**Action Required:** Please specify which specific resource or application is affected by this alert to ensure the investigation focuses on the correct target.\n\n" +
            $"Please provide:\n" +
            $"- The name or resource ID of the affected application/service\n";
    }

    private async Task MarkTargetResourceInputRequestedAsync(string alertId)
    {
        var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alertId, alertId);
        if (alertDocument != null)
        {
            if (alertDocument.TargetResourceInputRequested)
            {
                _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Target resource input has already been requested for alert {alertId}.");
                return;
            }

            var updatedAlertDocument = alertDocument with
            {
                TargetResourceInputRequested = true,
                UpdatedAt = DateTime.UtcNow
            };

            await _dbContainer.UpsertItemAsync(
                updatedAlertDocument,
                new PartitionKey(updatedAlertDocument.PartitionKey)
            );
        }
    }

    private async Task InitiateIncidentInvestigationAsync(
        AlertItem alert,
        Thread thread,
        AgentContext agentContext,
        IncidentHandlerDocumentPayload? handlerDoc)
    {
        var alertInfo = GetAlertInfoAsPrompt(alert);

        string subAgentPrompt = handlerDoc != null
            ? $"An Azure Monitor Alert was fired with the following details: {alertInfo}.\n Process the incident as per the custom instructions provided in the thread."
            : $"An Azure Monitor Alert was fired with the following details: {alertInfo}.\n Based on the alert details start the remediation flow with the appropriate subagent. Be as autonomous as possible without asking for permission to take actions.";

        await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
            ThreadId: thread.Id,
            AgentContextId: agentContext.Id,
            MessageId: thread.StartMessage?.Id ?? new Guid(),
            Message: subAgentPrompt,
            UserId: "agent-default",
            DisplayName: "Azure SRE Agent",
            Timestamp: DateTime.UtcNow
        ));
    }

    #endregion

    private async Task<string> SaveAlertToDocumentDb(AlertItem alert)
    {
        var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alert.Id, alert.Id);

        if (alertDocument is null)
        {
            _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Creating new incident document for {alert.Id}.");

            var newAlertDocument = AzMonitorAlertDocument.FromIncident(alert);

            // Save to database
            try
            {
                var response = await _dbContainer.UpsertItemAsync(
                    newAlertDocument,
                    new PartitionKey(newAlertDocument.PartitionKey)
                );

                _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Alert document created successfully with id: {newAlertDocument.Id}");

                return newAlertDocument.Id;
            }
            catch (CosmosException ex)
            {
                _logger.LogInternalError(ex, $"[AzMonitorIncidentHandlingService] Error creating alert document in database: {ex.Message}");
                throw;
            }
        }
        else
        {
            _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Alert document already exists with id: {alertDocument.Id}. No new incident created.");
        }

        return alertDocument.Id;
    }

    private DateTimeOffset ParseDateTimeOffset(string? value)
    {
        DateTimeOffset createdAt;
        if (!string.IsNullOrEmpty(value) && DateTimeOffset.TryParse(value, out var parsedDate))
        {
            createdAt = parsedDate;
        }
        else
        {
            createdAt = DateTimeOffset.UtcNow;
            _logger.LogInternalWarning($"[AzMonitorIncidentHandlingService] Could not parse start time {value}, using current time instead");
        }

        return createdAt;
    }

    private async Task<bool> SaveAlertToGraphDb(AlertItem alert)
    {
        try
        {
            var essentials = alert.Properties.Essentials;
            var targetResourceId = essentials.TargetResource;

            if (string.IsNullOrEmpty(targetResourceId))
            {
                _logger.LogInternalWarning($"[AzMonitorIncidentHandlingService] Alert {alert.Id} has no target resource, skipping graph DB operations");
                return false;
            }

            var alertNode = new AzMonitorAlertNode
            {
                IncidentId = alert.Id,
                UpdateTs = DateTime.UtcNow.Ticks
            };

            _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Adding/updating alert node in graph DB for {alert.Id}");
            var nodeResult = await _graphDbClient.AddOrUpdateNodeAsync(alertNode);

            if (!nodeResult)
            {
                _logger.LogInternalWarning($"[AzMonitorIncidentHandlingService] Failed to add/update alert node in graph DB for {alert.Id}");
                return false;
            }

            // Create edge between resource and alert
            var edge = new RelatedToIncidentEdge
            {
                SourceNodeId = targetResourceId.ToLowerInvariant(), // Resource is the source
                TargetNodeId = alertNode.GetNodeId(), // Alert is the target
                UpdateTs = DateTime.UtcNow.Ticks
            };

            _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Adding/updating edge in graph DB between resource {targetResourceId} and alert {alert.Id}");
            var edgeResult = await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            if (!edgeResult)
            {
                _logger.LogInternalWarning($"[AzMonitorIncidentHandlingService] Failed to add/update edge in graph DB between resource {targetResourceId} and alert {alert.Id}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[AzMonitorIncidentHandlingService] Error saving alert {alert.Id} to graph database: {ex.Message}");
            return false;
        }
    }

    private async Task<(Thread, AgentContext)> CreateIncidentThread(AlertItem alert)
    {
        var essentials = alert.Properties.Essentials;

        var alertId = alert.Id;
        var alertRule = essentials.AlertRule;
        var severity = essentials.Severity.ToString();
        var description = essentials.Description;
        var targetResource = essentials.TargetResource;
        var monitorCondition = essentials.MonitorCondition.ToString();
        var monitorService = essentials.MonitorService;
        var startDateTime = ParseDateTimeOffset(essentials.StartDateTime);

        var alertRuleName = new ResourceIdentifier(alertRule);
        var targetResourceId = new ResourceIdentifier(targetResource);
        var subscription = targetResourceId?.SubscriptionId?.ToString();
        var resourceGroup = targetResourceId?.ResourceGroupName?.ToString();
        var targetResourceName = essentials.TargetResourceName;

        var alertIdResource = new ResourceIdentifier(alertId);
        var alertRuleResource = new ResourceIdentifier(alertRule);

        var encodedAlertId = Uri.EscapeDataString(alertId);
        var portalUrl = $"https://ms.portal.azure.com/#view/Microsoft_Azure_Monitoring_Alerts/AlertDetails.ReactView/alertId~/{encodedAlertId}/invokedFrom/CopyLinkFeature";

        var alertData = new
        {
            alertId = alertIdResource.Name,
            alertRule = alertRuleResource.Name,
            description,
            monitoredResource = targetResourceName,
            severity,
            monitorCondition,
            monitorService,
            firedAt = startDateTime.ToString(),
            subscription,
            resourceGroup,
            portalUrl
        };

        var serializedAlertData = JsonSerializer.Serialize(alertData);
        var alertDataBlock = $"```incident-alert\n{serializedAlertData}\n```\n";

        var title = $"[{severity}] [{targetResourceId?.Name ?? string.Empty}] {alertRuleName.Name}";
        (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
            title: $"Azure Monitor Alert - {title}",
            message: alertDataBlock,
            agentTypeEnum: AgentTypeEnum.Meta,
            source: ThreadSource.Incident,
            incidentId: alertIdResource.Name, // Alert GUID (unique every time a new alert is fired)
            incidentSource: new IncidentSource(Agent.Core.Models.Api.v1.IncidentType.AzMonitor, alertRule),
            incidentDetails: new IncidentDetails(
                title,
                startDateTime,
                severity,
                string.Empty,
                string.Empty,
                string.Empty,
                InvestigationStatus.InProgress)
        );

        // Emit agent action telemetry for thread creation with incident source AzMonitor
        try
        {
            var param = JsonSerializer.Serialize(new { IncidentSource = IncidentType.ToString() });
            _logger.LogAgentAction(
                action: AgentActionEvents.CreateThread,
                parameter: param,
                status: AgentActionStatus.Success,
                duration: 0,
                threadId: thread.Id.ToString(),
                subAgentName: "",
                threadSource: thread.Source.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "[AzMonitorIncidentHandlingService] CreateIncidentThread: Failed to emit LogAgentAction for CreateThread");
        }

        try
        {
            var agentMessage = $"Alert acknowledged ✅\n\nInitiating investigation to assess the situation and identify potential causes 🛠️";

            await _repository.AddMessageAsync(thread.Id, new Message(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                    agentMessage
                ));

            return (thread, agentContext);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[AzMonitorIncidentHandlingService] Creating Incident thread failed with ex: {ex.Message}");
        }

        return (thread, agentContext);
    }

    private async Task<(Thread, AgentContext)> CreateHandlerAwareIncidentThread(
        AlertItem alert,
        IncidentHandlerDocumentPayload? handlerDoc,
        AzMonitorIncidentFilterDocumentPayload? filterPayload)
    {
        if (handlerDoc != null)
        {
            return await CreateIncidentThreadWithHandler(alert, handlerDoc, filterPayload);
        }
        else
        {
            // Fallback to existing behavior when no handler
            return await CreateIncidentThread(alert);
        }
    }

    private async Task<(Thread, AgentContext)> CreateIncidentThreadWithHandler(
        AlertItem alert,
        IncidentHandlerDocumentPayload handlerDoc,
        AzMonitorIncidentFilterDocumentPayload? filterPayload)
    {
        var essentials = alert.Properties.Essentials;
        var alertId = alert.Id;
        var alertRule = essentials.AlertRule;
        var severity = essentials.Severity.ToString();
        var description = essentials.Description;
        var targetResource = essentials.TargetResource;
        var startDateTime = ParseDateTimeOffset(essentials.StartDateTime);

        var alertRuleName = new ResourceIdentifier(alertRule);
        var targetResourceId = new ResourceIdentifier(targetResource);
        var targetResourceName = essentials.TargetResourceName;
        var alertIdResource = new ResourceIdentifier(alertId);

        // Build base Azure Monitor alert message
        var title = $"[{severity}] [{targetResourceId?.Name ?? string.Empty}] {alertRuleName.Name}";
        var alertMessage = $"🚨 **New Azure Monitor Alert Reported**\n\n" +
            $"**Title:** {title}\n\n" +
            $"**Alert ID:** {alertId}\n\n" +
            $"**Severity:** {severity}\n\n" +
            $"**Description:** {description}\n\n" +
            $"**Source:** Azure Monitor\n\n";

        // Add existing Azure Monitor JSON block
        var alertData = new
        {
            alertId = alertIdResource.Name,
            alertRule = alertRuleName.Name,
            description,
            monitoredResource = targetResourceName,
            severity,
            monitorCondition = essentials.MonitorCondition.ToString(),
            monitorService = essentials.MonitorService,
            firedAt = startDateTime.ToString(),
            subscription = targetResourceId?.SubscriptionId?.ToString(),
            resourceGroup = targetResourceId?.ResourceGroupName?.ToString()
        };

        var serializedAlertData = JsonSerializer.Serialize(alertData);
        var alertDataBlock = $"```incident-alert\n{serializedAlertData}\n```\n";
        alertMessage += alertDataBlock;

        // Add handler-specific instructions
        var customInstructionsForAlert = handlerDoc.IncidentProcessingGuide?.Count > 0 ?
            string.Join("\n", handlerDoc.IncidentProcessingGuide.Select(x => $"* {x}")) :
            "No custom instructions provided for this incident type.";

        alertMessage += $"\n\n**Custom Instructions for Incident Processing:**\n" +
            $"{customInstructionsForAlert}\n\n" +
            $"**Incident Handler:** {handlerDoc.Name}";

        // Create dynamic YAML agent from incident handler
        var dynamicAgentName = $"azmonitor_handler_{handlerDoc.Id}";

        // Build system prompt from incident processing guide
        var systemPrompt = BuildSystemPromptFromHandler(handlerDoc, alert);

        // Create YAML agent descriptor
        var yamlDescriptor = new YamlAgentDescriptor
        {
            Name = dynamicAgentName,
            Instructions = systemPrompt,
            Tools = handlerDoc.Tools ?? new List<string>(),
            Handoffs = new List<string>(),
            AllowParallelToolCalls = false,
            Temperature = 0.7f
        };

        // Register the agent dynamically
        await RegisterDynamicYamlAgent(yamlDescriptor);

        // Create thread with handler settings
        (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
            title: $"Azure Monitor Alert - {title}",
            message: alertMessage,
            agentTypeEnum: _experimentalSettings.UseYamlForIncidentHandling ? AgentTypeEnum.Meta : AgentTypeEnum.Incident,
            source: ThreadSource.Incident,
            incidentId: alertIdResource.Name,
            incidentSource: new IncidentSource(Agent.Core.Models.Api.v1.IncidentType.AzMonitor, alertRule),
            AllowedTools: handlerDoc.Tools,
            overrideAgentMode: filterPayload?.AgentMode ?? "",
            incidentDetails: new IncidentDetails(
                title,
                startDateTime,
                severity,
                string.Empty,
                handlerDoc.IncidentFilterId,
                handlerDoc.Id,
                InvestigationStatus.InProgress)
        );

        // Update agent context for YAML mode
        if (_experimentalSettings.UseYamlForIncidentHandling)
        {
            agentContext = agentContext with { CurrentAgent = dynamicAgentName };
            await _repository.UpdateAgentContextAsync(agentContext);
        }

        // Emit agent action telemetry for thread creation
        try
        {
            var param = JsonSerializer.Serialize(new { IncidentSource = IncidentType.ToString(), HandlerId = handlerDoc.Id });
            _logger.LogAgentAction(
                action: AgentActionEvents.CreateThread,
                parameter: param,
                status: AgentActionStatus.Success,
                duration: 0,
                threadId: thread.Id.ToString(),
                subAgentName: "",
                threadSource: thread.Source.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "[AzMonitorIncidentHandlingService] CreateIncidentThreadWithHandler: Failed to emit LogAgentAction for CreateThread");
        }

        // Add acknowledgment message
        var agentMessage = $"**Acknowledging the incident**. I'm starting to investigate and see how I can help.";
        await _repository.AddMessageAsync(thread.Id, new Message(
            Guid.NewGuid(),
            DateTime.UtcNow,
            new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
            agentMessage
        ));

        return (thread, agentContext);
    }

    private string BuildSystemPromptFromHandler(IncidentHandlerDocumentPayload handler, AlertItem alert)
    {
        var promptBuilder = new StringBuilder();

        // Base instruction
        promptBuilder.AppendLine($"You are an Azure Monitor incident handler agent for {handler.Name}.");
        promptBuilder.AppendLine($"You are handling Azure Monitor alert: {alert.Id}");
        promptBuilder.AppendLine();

        // Add instructions for status updates using the tool
        promptBuilder.AppendLine("IMPORTANT: Use the 'NotifyUser' tool to provide status updates as you work through the incident. Do not provide repetitive updates. Only send updates about new steps that are being taken.");
        promptBuilder.AppendLine("Send status updates for major steps like:");
        promptBuilder.AppendLine("- Starting investigation");
        promptBuilder.AppendLine("- Analyzing metrics or logs");
        promptBuilder.AppendLine("- Identifying root cause");
        promptBuilder.AppendLine("- Applying remediation");
        promptBuilder.AppendLine();

        // Add processing guide as instructions
        if (handler.IncidentProcessingGuide?.Count > 0)
        {
            promptBuilder.AppendLine("Follow these incident processing guidelines:");
            foreach (var guideline in handler.IncidentProcessingGuide)
            {
                promptBuilder.AppendLine($"- {guideline}");
            }
            promptBuilder.AppendLine();
        }

        // Add custom instructions
        if (!string.IsNullOrEmpty(handler.CustomInstructions))
        {
            promptBuilder.AppendLine("Additional Instructions:");
            promptBuilder.AppendLine(handler.CustomInstructions);
        }

        return promptBuilder.ToString();
    }

    private async Task<T?> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            ItemResponse<T> response = await _dbContainer.ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    private string GetAlertInfoAsPrompt(AlertItem alert)
    {
        if (alert == null)
        {
            return "Alert information unavailable";
        }

        var essentials = alert.Properties?.Essentials;

        // Is Unknown the best fallback?
        return $@"Azure Monitor Alert Details:
                ID: {alert.Id ?? "Unknown"}
                Name: {alert.Name ?? "Unknown"}
                Rule: {essentials?.AlertRule ?? "Unknown"}
                Severity: {essentials?.Severity ?? "Unknown"}
                Condition: {essentials?.MonitorCondition ?? "Unknown"}
                Description: {essentials?.Description ?? "Unknown"}
                Resource: {essentials?.TargetResourceName ?? essentials?.TargetResource ?? "Unknown"}
                Type: {essentials?.TargetResourceType ?? "Unknown"}
                Time: {essentials?.StartDateTime ?? "Unknown"}";
    }

    private async Task<Thread?> FindExistingActiveThreadForAlertRule(AlertItem alert)
    {
        try
        {
            var alertRuleResourceId = alert.Properties.Essentials.AlertRule;
            var targetResource = alert.Properties?.Essentials?.TargetResource;

            _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Looking for existing active thread for alert rule resource Id: {alertRuleResourceId}, target resource: {targetResource}");

            // Get threads from last 7 days only
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            var allIncidentThreads = await _repository.GetThreadsBySourceAsync(
                source: ThreadSource.Incident,
                incidentType: Agent.Core.Models.Api.v1.IncidentType.AzMonitor,
                createdAfter: sevenDaysAgo);

            foreach (var thread in allIncidentThreads)
            {
                try
                {
                    // Get the alert document ID (GUID) from thread status
                    string? alertDocumentId = thread?.Status?.IncidentStatus?.IncidentId;
                    if (thread is null || alertDocumentId == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(alertDocumentId))
                    {
                        _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Skipping thread {thread.Id} - no alert document ID found in status");
                        continue;
                    }

                    // Get the AzMonitorAlertDocument for this thread
                    var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alertDocumentId, alertDocumentId);

                    if (alertDocument == null)
                    {
                        _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] No alert document found for thread {thread.Id} with alert ID {alertDocumentId}");
                        continue;
                    }

                    // Skip if the alert document is already closed
                    if (alertDocument.Status == ServiceAlertState.Closed.ToString())
                    {
                        _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Alert document {alertDocumentId} is already closed, skipping thread {thread.Id}");
                        continue;
                    }

                    // Match based on alert rule id
                    if (!string.IsNullOrEmpty(alertDocument.AlertRuleResourceId) && alertDocument.AlertRuleResourceId == alertRuleResourceId)
                    {
                        _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Found existing active thread {thread.Id} for alert rule {alertRuleResourceId} and target resource {targetResource}");
                        return thread;
                    }
                    else
                    {
                        _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] Thread {thread.Id} alert document doesn't match - Document: (Name: {alertDocument.Title}, TargetResource: {alertDocument.TargetResourceId}) vs Incoming: (Name: {alertRuleResourceId}, TargetResource: {targetResource})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"[AzMonitorIncidentHandlingService] Error processing thread {thread.Id} while looking for existing alert rule: {ex.Message}");
                    continue;
                }
            }

            _logger.LogInternalInformation($"[AzMonitorIncidentHandlingService] No existing active thread found for alert rule {alertRuleResourceId} and target resource {targetResource}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[AzMonitorIncidentHandlingService] Error finding existing thread for alert rule {alert.Properties.Essentials.AlertRule}: {ex.Message}");
        }

        return null;
    }

    private async Task PromptUserForInputAsync(Guid threadId, AgentContext agentContext, string message)
    {
        var chatMessage = new ChatMessage(ChatRole.Assistant, message);
        var messageId = Guid.NewGuid();

        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            agentContext,
            chatMessage,
            messageId);

        // Signal that processing is complete and agent is waiting for user input
        await _outboundCommunicationService.SignalProcessingComplete(threadId, messageId);
    }

    /// <summary>
    /// Checks if the alert requires user input to specify the correct target resource
    /// based on the target resource type (workspace/component alerts need clarification)
    /// </summary>
    /// <param name="alert">The alert to check</param>
    /// <returns>True if user input is required for target resource specification, false otherwise</returns>
    private static bool RequiresTargetResourceInput(AlertItem alert)
    {
        var resourceType = alert.Properties?.Essentials?.TargetResourceType?.ToLowerInvariant();
        return resourceType == "microsoft.operationalinsights/workspaces" ||
               resourceType == "microsoft.insights/components";
    }

    private double? GetTimeTilMitigation(AzMonitorAlertDocument incidentDoc)
    {
        DateTime? mitigatedAt = incidentDoc.ResolvedAt;
        if (mitigatedAt == null)
        {
            return null;
        }

        double totalMinutes = ((DateTime)mitigatedAt).Subtract(incidentDoc.CreatedAt).TotalMinutes;
        return Math.Round(totalMinutes, 2, MidpointRounding.AwayFromZero);
    }

    protected override AzMonitorIncidentFilterDocument GetDefaultIncidentFilter(IncidentHandlingRequestModel<AzMonitorIncidentFilterDocumentPayload> request)
    {
        string filterId = $"IncidentFilter_AzMonitor";
        return new AzMonitorIncidentFilterDocument()
        {
            Id = request?.IncidentFilter?.Id ?? filterId,
            Name = request?.IncidentFilter?.Name ?? filterId,
            AlertId = request?.IncidentFilter?.AlertId ?? filterId,
            AgentMode = request?.IncidentFilter?.AgentMode ?? "",
            ImpactedService = request?.IncidentFilter?.ImpactedService ?? "",
            Priority = request?.IncidentFilter?.Priority ?? "",
            IncidentType = request?.IncidentFilter?.IncidentType ?? "",
            TitleContains = request?.IncidentFilter?.TitleContains ?? "",
            TargetResourceType = request?.IncidentFilter?.TargetResourceType ?? string.Empty,
            TargetResource = request?.IncidentFilter?.TargetResource ?? string.Empty,
            MaxAutomatedInvestigationAttempts = 3,
            CreatedAt = request?.IncidentFilter?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = request?.IncidentFilter?.UpdatedAt ?? DateTime.UtcNow
        };
    }

    protected IncidentAIData ToIncidentActivitySnapshot(AzMonitorIncidentFilterDocumentPayload filter, AzMonitorAlertDocument incidentDetails, IncidentHandlingRequestModelBase request, IncidentHandlerDocumentPayload? handler)
    {
        IncidentAIData snapShot = new IncidentAIData
        {
            HandlerId = filter?.Id ?? filter?.Name ?? "no-handler",
            IncidentId = incidentDetails.Id,
            IncidentTitle = incidentDetails.Title,
            IncidentCreatedAt = incidentDetails.CreatedAt,
            IncidentUpdatedAt = incidentDetails.LastModifiedTime,
            HandlerCreatedAt = filter?.CreatedAt ?? DateTime.UtcNow,
            HandlerUpdatedAt = filter?.UpdatedAt ?? DateTime.UtcNow,
            IncidentHandledAt = DateTime.UtcNow,
            MitigatedAt = incidentDetails.ResolvedAt, // Az Monitor is special case due to recurring alerts, so incident can possibly already be resolved
            Status = incidentDetails.Status.ToString(),
            Priority = incidentDetails.Priority,
            IsMitigatedByAgent = false,
            IsAssistedByAgent = incidentDetails.IsAssistedByAgent,
            RootCause = incidentDetails.AIRootCause,
            RootCauseDescription = incidentDetails.RootCauseDescription,
            Summary = incidentDetails.GeneralSummary,
            ImpactedService = incidentDetails.ImpactedServiceName,
            RunMode = !string.IsNullOrWhiteSpace(filter?.AgentMode) ? filter.AgentMode! : "review",
            IsHandlerCustom = !string.IsNullOrWhiteSpace(handler?.CustomInstructions) ? true : false,
            IncidentPlatform = IncidentType.ToString(),
            TimeTilMitigation = GetTimeTilMitigation(incidentDetails)
        };

        return snapShot;
    }

    #endregion
}
