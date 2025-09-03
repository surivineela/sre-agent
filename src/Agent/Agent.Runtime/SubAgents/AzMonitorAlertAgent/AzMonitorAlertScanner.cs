// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Data.DataModels;
using Agent.Plugins.Interface;
using Agent.Runtime.Interfaces;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Services;
using Azure.Core;
using Azure.ResourceManager.AlertsManagement.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Author = Agent.Core.Models.Api.v1.Author;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Message = Agent.Core.Models.Api.v1.Message;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.SubAgents.AzMonitorAlertAgent;

public class AzMonitorAlertScanner
{
    private readonly ILogger<AzMonitorAlertScanner> _logger;
    private readonly IGraphDBPlugin _graphDBPlugin;
    private readonly IAgentInboundCommunicationService _inboundCommunicationService;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IThreadRepository _repository;
    private readonly IChatClient _chatClient;
    private readonly IAzMonitorAlertService _azMonitorAlertService;
    private readonly IInvestigationOrchestrator _investigationOrchestrator;
    private readonly Container _dbContainer;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ILogQueryService _logQueryService;
    private readonly IAzMonitorAlertInvestigationService _azMonitorInvestigationService;
    private readonly IAgentsFactory _agentsFactory;
    private readonly IIncidentStatusMetricsService _incidentsStatusMetricsService;
    private readonly IncidentManagementSettings _incidentManagementSettings;


    public AzMonitorAlertScanner(
        IGraphDBPlugin graphDbPlugin,
        IAzMonitorAlertService azMonitorAlertService,
        IAgentInboundCommunicationService inboundCommunicationService,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IThreadRepository repository,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IGraphDatabaseClient graphDatabaseClient,
        ILogQueryService logQueryService,
        IAzMonitorAlertInvestigationService alertInvestigationService,
        IInvestigationOrchestrator investigationOrchestrator,
        IAgentsFactory agentsFactory,
        IIncidentStatusMetricsService incidentsStatusMetricsService,
        IChatClient chatClient,
        IncidentManagementSettings incidentManagementSettings,
        ILogger<AzMonitorAlertScanner> logger)
    {
        _graphDBPlugin = graphDbPlugin;
        _logger = logger;
        _incidentManagementSettings = incidentManagementSettings;

        _azMonitorAlertService = azMonitorAlertService;
        _inboundCommunicationService = inboundCommunicationService;
        _outboundCommunicationService = outboundCommunicationService;
        _repository = repository;
        _chatClient = chatClient;

        _dbContainer = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
        _graphDbClient = graphDatabaseClient;
        _logQueryService = logQueryService;
        _azMonitorInvestigationService = alertInvestigationService;
        _investigationOrchestrator = investigationOrchestrator;

        _agentsFactory = agentsFactory;
        _incidentsStatusMetricsService = incidentsStatusMetricsService;
    }

    /// <summary>
    /// Polls for new alerts in Log Analytics Workspace on a given cadence.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns></returns>
    public async Task PollNewAlertsAsync(CancellationToken ct = default)
    {
        _logger.LogInternalInformation("Polling for new Azure Monitor alerts from the last minute");

        try
        {
            var getSubscriptions = await _graphDBPlugin.ListSubscriptionsAsync();
            List<string> subscriptions = new();
            foreach (var subscription in getSubscriptions)
            {
                subscriptions.Add(subscription["id"]);
            }

            if (subscriptions.Count == 0)
            {
                _logger.LogInternalInformation($"No subscriptions found in the Graph DB.");
                return;
            }

            _logger.LogInternalInformation($"Scanning for Azure Monitor Alerts in the following subscriptions: {string.Join(", ", subscriptions)}");

            foreach (var subscription in subscriptions)
            {
                _logger.LogInternalInformation($"Checking for alerts in subscription: {subscription}");
                var newAlerts = await _azMonitorAlertService.PollNewAlertsBySubscriptionId(subscription, 5);

                int alertCount = newAlerts.Count();
                _logger.LogInternalInformation($"Found {alertCount} alerts in subscription {subscription}");

                foreach (var alert in newAlerts)
                {
                    _logger.LogInternalInformation($"Processing new alert {alert.Id}...");
                    await ProcessAlertAsync(alert, ct);
                }
            }
            // periodically refresh incident metrics
            var incidentMetrics = await _incidentsStatusMetricsService.GetIncidentStatusMetricsAsync(null, DateTime.Now);
            await _outboundCommunicationService.NotifyIncidentStatusMetrics(Guid.Empty, incidentMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error polling Azure Monitor alerts");
        }
    }

    public async Task ProcessAlertAsync(AlertItem alert, CancellationToken cancellationToken)
    {
        try
        {
            // check if there exists an active incident thread for this alert
            var existingActiveThread = await FindExistingActiveThreadForAlertRule(alert);

            await _azMonitorAlertService.AcknowledgeAlert(alert.Id);

            if (existingActiveThread != null)
            {
                if (RequiresTargetResourceInput(alert))
                {
                    string? alertDocumentId = existingActiveThread?.Status?.IncidentStatus?.IncidentId;
                    if (!string.IsNullOrEmpty(alertDocumentId))
                    {
                        var existingAlertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alertDocumentId, alertDocumentId);
                        if (existingAlertDocument != null)
                        {
                            if (existingAlertDocument.TargetResourceInputRequested)
                            {
                                // return if user input already requested.
                                return;
                            }
                        }
                    }
                }

                var investigationFinished = await IsInvestigationFinishedAsync(existingActiveThread!.Id);

                if (investigationFinished)
                {
                    string? alertDocumentId = existingActiveThread?.Status?.IncidentStatus?.IncidentId;
                    if (!string.IsNullOrEmpty(alertDocumentId))
                    {
                        var existingAlertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alertDocumentId, alertDocumentId);
                        if (existingAlertDocument != null)
                        {
                            // Check if we've reached the investigation attempt limit
                            var currentHitCount = existingAlertDocument.HitCount;
                            var maxRetryCount = _incidentManagementSettings.MaxAutomatedInvestigationAttempts;

                            // Update hitcount for every alert
                            var updatedAlertDocument = existingAlertDocument with
                            {
                                HitCount = currentHitCount + 1,
                                UpdatedAt = DateTime.UtcNow
                            };

                            await _dbContainer.UpsertItemAsync(
                                updatedAlertDocument,
                                new PartitionKey(updatedAlertDocument.PartitionKey)
                            );

                            if (currentHitCount > maxRetryCount)
                            {
                                // skip prompting user for input if already requested
                                if (existingAlertDocument.UserInputRequested) return;

                                // Retry limit reached - ask for user input
                                _logger.LogInternalInformation($"Found existing active thread {existingActiveThread?.Id} for alert {alert.Id}. Retry limit ({maxRetryCount}) reached. Requesting user input.");

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

                                await PromptUserForInputAsync(
                                    existingActiveThread.Id,
                                    existingAgentContext,
                                    userInputMessage
                                );

                                // Update UserInputRequested to avoid appending User Input Message again
                                updatedAlertDocument = updatedAlertDocument with
                                {
                                    UserInputRequested = true
                                };

                                await _dbContainer.UpsertItemAsync(
                                    updatedAlertDocument,
                                    new PartitionKey(updatedAlertDocument.PartitionKey)
                                );

                                // Don't increment the count when asking for user input - let them respond first
                                return;
                            }
                            else
                            {
                                // Under retry limit - append recurring alert message and increment count
                                _logger.LogInternalInformation($"Found existing active thread {existingActiveThread?.Id} with completed investigation for alert {alert.Id}. Appending recurring alert message. Count: {currentHitCount + 1}/{maxRetryCount}");

                                var message = $"Another alert **{alert.Id}** is firing with the same alert rule. Merging the investigation.";
                                if (existingActiveThread == null)
                                {
                                    _logger.LogInternalWarning("existingActiveThread is null when trying to get agent contexts.");
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

                                _logger.LogInternalInformation($"Updated recurring alert count to {currentHitCount + 1} for alert document {alertDocumentId}");
                            }
                        }
                        else
                        {
                            _logger.LogInternalWarning($"Could not find alert document {alertDocumentId} for existing thread {existingActiveThread?.Id}");
                        }
                    }
                    else
                    {
                        _logger.LogInternalWarning($"No alert document ID found for existing thread {existingActiveThread?.Id}");
                    }
                }
                else
                {
                    // Investigation is still in progress, just log and don't append message
                    _logger.LogInternalInformation($"Found existing active thread {existingActiveThread.Id} with ongoing investigation for alert {alert.Id}. Not appending message as investigation is still in progress.");
                }
                return;
            }

            // save alert in the document db
            var docId = await SaveAlertToDocumentDb(alert);

            // save alert as a node in the graph db and create edge to resource
            await SaveAlertToGraphDb(alert);

            // Create incident thread
            var (thread, agentContext) = await CreateIncidentThread(alert);

            var investigationResult = await _investigationOrchestrator.InvestigateAlertAsync(alert, thread);

            if (RequiresTargetResourceInput(alert))
            {
                _logger.LogInternalInformation($"Alert {alert.Id} targets a {alert.Properties?.Essentials?.TargetResourceType} resource. Requesting user to specify the affected resource before investigation.");

                var targetResourceInputMessage = $"This alert is targeting a **{alert.Properties?.Essentials?.TargetResourceType}** resource, which may impact the automated investigation.\n\n" +
                    $"**Action Required:** Please specify which specific resource or application is affected by this alert to ensure the investigation focuses on the correct target.\n\n" +
                    $"Please provide:\n" +
                    $"- The name or resource ID of the affected application/service\n";

                await PromptUserForInputAsync(thread.Id, agentContext, targetResourceInputMessage);

                // Update the alert document to mark that target resource input has been requested
                var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(docId, docId);
                if (alertDocument != null)
                {
                    if (alertDocument.TargetResourceInputRequested)
                    {
                        _logger.LogInternalInformation($"Target resource input has already been requested for alert {alert.Id}.");
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

                return;
            }

            var alertInfo = GetAlertInfoAsPrompt(alert);

            string subAgentPrompt = $"An Azure Monitor Alert was fired with the following details: {alertInfo}.\n Based on the alert details start the remediation flow with the appropriate subagent. Be as autonomous as possible without asking for permission to take actions.";

            await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                   ThreadId: thread.Id,
                   AgentContextId: agentContext.Id,
                   MessageId: thread.StartMessage?.Id ?? new Guid(),
                   Message: $"An automated investigation has been completed for this alert with the following hypotheses and findings: {investigationResult.Summary}\n\nPlease validate these hypotheses by checking the supporting evidence. If the hypotheses seem incomplete or insufficient, conduct additional targeted investigation focusing on metrics, logs, and recent changes. Your goal is to either confirm one of these hypotheses with high confidence or discover the actual root cause if it differs from what was identified by the automated analysis. Based on the initial findings, find an appropriate subagent to handle the remediation. CRITICAL: Be as autonomous as possible without asking for permission to take actions.",
                   UserId: "agent-default",
                   DisplayName: "Azure SRE Agent",
                   Timestamp: DateTime.UtcNow
               ));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error processing alert {alert.Id}: {ex.Message}");
        }
    }

    private async Task<string> SaveAlertToDocumentDb(AlertItem alert)
    {
        var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alert.Id, alert.Id);

        if (alertDocument is null)
        {
            _logger.LogInternalInformation($"Creating new incident document for {alert.Id}.");

            var essentials = alert.Properties.Essentials;

            var alertId = alert.Id;
            var alertRule = essentials.AlertRule;
            var severity = essentials.Severity.ToString();
            var description = essentials.Description;
            var targetResource = essentials.TargetResource;

            string targetResourceType = essentials.TargetResourceType;
            string targetResourceId = targetResource;

            var resourceIdentifier = new ResourceIdentifier(targetResource);

            string subscriptionId = resourceIdentifier.SubscriptionId ?? Guid.Empty.ToString();
            string status = ServiceAlertState.Acknowledged.ToString(); // at this point, the alert should be acknowledged
            DateTimeOffset createdAt = ParseDateTimeOffset(essentials.StartDateTime);

            var alertRuleName = new ResourceIdentifier(alertRule);

            var alertResourceId = new ResourceIdentifier(alertId);

            var newAlertDocument = new AzMonitorAlertDocument(
                Id: alertResourceId.Name, // only get the alert Id (guid)
                Name: alertRuleName.Name, // only get the Alert Name
                Severity: severity,
                TargetResourceType: targetResourceType,
                TargetResourceId: targetResourceId,
                SubscriptionId: subscriptionId,
                Status: status,
                CreatedAt: createdAt
            )
            {
                Description = description,
                UpdatedAt = DateTime.UtcNow,
                HitCount = 1,
                AlertRuleResourceId = alertRule
            };

            // Save to database
            try
            {
                var response = await _dbContainer.UpsertItemAsync(
                    newAlertDocument,
                    new PartitionKey(newAlertDocument.PartitionKey)
                );

                _logger.LogInternalInformation($"Alert document created successfully with id: {newAlertDocument.Id}");

                return newAlertDocument.Id;
            }
            catch (CosmosException ex)
            {
                _logger.LogInternalError(ex, $"Error creating alert document in database: {ex.Message}");
                throw;
            }
        }
        else
        {
            _logger.LogInternalInformation($"Alert document already exists with id: {alertDocument.Id}. No new incident created.");
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
            _logger.LogInternalWarning($"Could not parse start time {value}, using current time instead");
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
                _logger.LogInternalWarning($"Alert {alert.Id} has no target resource, skipping graph DB operations");
                return false;
            }

            var alertNode = new AzMonitorAlertNode
            {
                IncidentId = alert.Id,
                UpdateTs = DateTime.UtcNow.Ticks
            };

            _logger.LogInternalInformation($"Adding/updating alert node in graph DB for {alert.Id}");
            var nodeResult = await _graphDbClient.AddOrUpdateNodeAsync(alertNode);

            if (!nodeResult)
            {
                _logger.LogInternalWarning($"Failed to add/update alert node in graph DB for {alert.Id}");
                return false;
            }

            // Create edge between resource and alert
            var edge = new RelatedToIncidentEdge
            {
                SourceNodeId = targetResourceId.ToLowerInvariant(), // Resource is the source
                TargetNodeId = alertNode.GetNodeId(), // Alert is the target
                UpdateTs = DateTime.UtcNow.Ticks
            };

            _logger.LogInternalInformation($"Adding/updating edge in graph DB between resource {targetResourceId} and alert {alert.Id}");
            var edgeResult = await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            if (!edgeResult)
            {
                _logger.LogInternalWarning($"Failed to add/update edge in graph DB between resource {targetResourceId} and alert {alert.Id}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error saving alert {alert.Id} to graph database: {ex.Message}");
            return false;
        }
    }

    private async Task<(Thread, AgentContext)> CreateIncidentThread(AlertItem alert)
    {
        var messageBuilder = new StringBuilder();

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

        (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
            title: $"Incident Alert - [{severity}] [{targetResourceId?.Name ?? string.Empty}] {alertRuleName.Name}",
            message: alertDataBlock,
            agentTypeEnum: AgentTypeEnum.Meta,
            source: ThreadSource.Incident,
            incidentId: alertIdResource.Name, // Alert GUID (unique every time a new alert is fired)
            incidentSource: new IncidentSource(IncidentType.AzMonitor, alertRule)
        );

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
            _logger.LogInternalError($"Creating Incident thread failed with ex: {ex.Message}");
        }

        return (thread, agentContext);
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

    /// <summary>
    /// Checks if the investigation has finished by looking for final summary items in thread messages
    /// </summary>
    /// <param name="threadId">The thread ID to check</param>
    /// <returns>True if investigation is finished, false if still in progress</returns>
    private async Task<bool> IsInvestigationFinishedAsync(Guid threadId)
    {
        try
        {
            // Get all messages from the thread
            var messages = await _repository.GetMessagesAsync(threadId);

            foreach (var message in messages)
            {
                // Look for investigation summaries in the message text
                if (message.Text.Contains("<investigation-summaries>") && message.Text.Contains("</investigation-summaries>"))
                {
                    // Extract JSON content between the tags using simple string operations
                    int startIndex = message.Text.IndexOf("<investigation-summaries>") + "<investigation-summaries>".Length;
                    int endIndex = message.Text.IndexOf("</investigation-summaries>");

                    if (startIndex > 0 && endIndex > startIndex)
                    {
                        string jsonContent = message.Text.Substring(startIndex, endIndex - startIndex).Trim();

                        try
                        {
                            var investigationSummaries = JsonSerializer.Deserialize<InvestigationSummaries>(jsonContent);

                            if (investigationSummaries?.summaries != null)
                            {
                                // Check if there's any final summary item that's completed
                                var hasFinalCompleted = investigationSummaries.summaries
                                    .Any(s => s.isFinal && s.status == "completed");

                                if (hasFinalCompleted)
                                {
                                    return true;
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogInternalWarning(ex, "Failed to parse investigation summaries JSON in message {MessageId}", message.Id);
                        }
                    }
                }
            }

            return false; // No finished investigation found
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error checking investigation status for thread {ThreadId}", threadId);
            return false; // Assume not finished on error to be safe
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

    // TODO: Duplicate method. Move to a common helper class.
    private async Task<string> SummarizeWithLLM(string prompt, Guid threadGuid, AgentContext agentContext)
    {
        try
        {
            var message = new ChatMessage(ChatRole.System, prompt);

            var options = new ChatOptions
            {
                Tools = _agentsFactory.GetSubAgentsAITools(threadGuid, agentContext),
                Temperature = (float)0.1,
                ToolMode = ChatToolMode.Auto,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "text"
                }
            };

            var response = await _chatClient.GetResponseAsync(new List<ChatMessage> { message }, options);
            return response.Text;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error summarizing content with llm.");
            return $"Error summarizing with LLM: {ex.Message}";
        }
    }

    // Helper method to append an investigation summary to an existing message
    private async Task AppendInvestigationSummaryToMessage(
        Guid threadId,
        Guid messageId,
        string summaryTitle,
        string summaryContent,
        bool isCollapsed = true,
        string status = "completed",
        bool isFinal = false)
    {
        try
        {
            // Get the existing message
            var existingMessage = await _repository.GetMessageAsync(threadId, messageId);
            if (existingMessage == null)
            {
                _logger.LogInternalWarning("Unable to update message {MessageId} - message not found in thread {ThreadId}", messageId, threadId);
                return;
            }

            // Parse existing message to extract the current investigation summaries JSON
            string existingText = existingMessage.Text;
            string updatedText;

            // Regular expression to extract the JSON inside investigation-summaries tags
            var summariesRegex = new Regex(@"<investigation-summaries>([\s\S]*?)<\/investigation-summaries>", RegexOptions.IgnoreCase);
            var match = summariesRegex.Match(existingText);

            if (match.Success)
            {
                // Extract the existing JSON
                string existingJson = match.Groups[1].Value.Trim();

                try
                {
                    // Deserialize the existing JSON
                    var investigationSummaries = JsonSerializer.Deserialize<InvestigationSummaries>(existingJson);
                    if (investigationSummaries == null)
                    {
                        throw new Exception("Deserialized investigation summaries is null");
                    }

                    // Create the new summary item
                    var newSummary = new SummaryItem
                    {
                        title = summaryTitle,
                        summary = summaryContent,
                        isCollapsed = isCollapsed,
                        status = status,
                        isFinal = isFinal
                    };

                    // Add the new summary to the existing list
                    var updatedSummaries = investigationSummaries.summaries.ToList();
                    updatedSummaries.Add(newSummary);
                    investigationSummaries.summaries = updatedSummaries.ToArray();

                    // Serialize back to JSON
                    string updatedJson = JsonSerializer.Serialize(investigationSummaries);

                    // Replace the old JSON with the new one
                    updatedText = summariesRegex.Replace(existingText, $"<investigation-summaries>{updatedJson}</investigation-summaries>");
                }
                catch (JsonException ex)
                {
                    _logger.LogInternalError(ex, "Error parsing existing investigation summaries JSON");
                    // If JSON parsing fails, use the ChatMessageService as fallback
                    updatedText = ChatMessageService.AppendInvestigationSummary(
                        existingMessage.Text,
                        summaryTitle,
                        summaryContent,
                        isCollapsed,
                        status,
                        isFinal);
                }
            }
            else
            {
                // If no existing investigation-summaries, use the ChatMessageService
                updatedText = ChatMessageService.AppendInvestigationSummary(
                    existingMessage.Text,
                    summaryTitle,
                    summaryContent,
                    isCollapsed,
                    status,
                    isFinal);
            }

            // Create a new message with the updated text but keeping all other properties the same
            Message updatedMessage = existingMessage with { Text = updatedText };

            // Update the message in the repository
            await _repository.UpdateMessageAsync(threadId, updatedMessage);
            _logger.LogInternalInformation("Successfully appended investigation summary '{Title}' to message {MessageId}", summaryTitle, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error appending investigation summary to message {MessageId}", messageId);
        }
    }

    public async Task CloseInActiveAzMonitorIncidentThreads(int cuttoffTimeWindow = 10, CancellationToken ct = default)
    {
        _logger.LogInternalInformation("Checking for inactive AzMonitor incident threads to close");

        try
        {
            // Get all AzMonitor incident threads
            var azMonitorIncidentThreads = await _repository.GetThreadsBySourceAsync(
                source: ThreadSource.Incident,
                incidentType: IncidentType.AzMonitor,
                createdAfter: null);

            _logger.LogInternalInformation($"Found {azMonitorIncidentThreads.Count()} AzMonitor incident threads");

            var cutoffTime = DateTime.UtcNow.AddMinutes(-cuttoffTimeWindow);
            var threadsToClose = new List<Thread>();

            foreach (var thread in azMonitorIncidentThreads)
            {
                // Skip if thread status or incident status is null
                if (thread.Status?.IncidentStatus == null)
                {
                    continue;
                }

                // Skip if already closed
                if (thread.Status?.IncidentStatus?.Status == ServiceAlertState.Closed.ToString())
                {
                    continue;
                }

                // Check last message timestamp
                DateTime? lastMessageTime = thread.LastMessage?.TimeStamp;

                if (lastMessageTime == null)
                {
                    // If no last message, use the thread creation timestamp
                    lastMessageTime = thread.CreatedTimestamp;
                }

                // If last activity was more than X minutes ago, mark for closure
                if (lastMessageTime < cutoffTime)
                {
                    threadsToClose.Add(thread);
                    _logger.LogInternalInformation($"Thread {thread.Id} is inactive since {lastMessageTime}, will be closed");
                }
            }

            _logger.LogInternalInformation($"Found {threadsToClose.Count} inactive threads to close");

            // Close the inactive threads by updating their AzMonitorAlertDocument status
            foreach (var thread in threadsToClose)
            {
                try
                {
                    var incidentId = thread?.Status?.IncidentStatus?.IncidentId;
                    if (thread == null || string.IsNullOrEmpty(incidentId))
                    {
                        continue;
                    }

                    var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(incidentId, incidentId);

                    if (alertDocument != null)
                    {
                        var updatedAlertDocument = alertDocument with
                        {
                            Status = ServiceAlertState.Closed.ToString(),
                            UpdatedAt = DateTime.UtcNow
                        };

                        await _dbContainer.UpsertItemAsync(
                            updatedAlertDocument,
                            new PartitionKey(updatedAlertDocument.PartitionKey)
                        );

                        _logger.LogInternalInformation($"Successfully closed AzMonitor alert document {incidentId} for inactive thread {thread.Id}");

                        await _repository.AddMessageAsync(thread.Id, new Message(
                            Guid.NewGuid(),
                            DateTime.UtcNow,
                            new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                            $"🔒 **Incident Auto-Closed**\n\nThis incident thread has been automatically closed due to {cuttoffTimeWindow} minutes of inactivity."
                        ));

                        _logger.LogInternalInformation($"Added closure message to thread {thread.Id}");
                    }
                    else
                    {
                        _logger.LogInternalWarning($"Could not find AzMonitor alert document with ID {incidentId} for thread {thread.Id}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Error closing AzMonitor alert for thread {thread.Id}: {ex.Message}");
                }
            }

            _logger.LogInternalInformation($"Completed processing inactive AzMonitor incident threads. Closed {threadsToClose.Count} incidents.");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error checking for inactive AzMonitor incident threads");
        }
    }

    private async Task<Thread?> FindExistingActiveThreadForAlertRule(AlertItem alert)
    {
        try
        {
            var alertRuleResourceId = alert.Properties.Essentials.AlertRule;
            var targetResource = alert.Properties?.Essentials?.TargetResource;

            _logger.LogInternalInformation($"Looking for existing active thread for alert rule resource Id: {alertRuleResourceId}, target resource: {targetResource}");

            // Get threads from last 7 days only
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            var allIncidentThreads = await _repository.GetThreadsBySourceAsync(
                source: ThreadSource.Incident,
                incidentType: IncidentType.AzMonitor,
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
                        _logger.LogInternalInformation($"Skipping thread {thread.Id} - no alert document ID found in status");
                        continue;
                    }

                    // Get the AzMonitorAlertDocument for this thread
                    var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alertDocumentId, alertDocumentId);

                    if (alertDocument == null)
                    {
                        _logger.LogInternalInformation($"No alert document found for thread {thread.Id} with alert ID {alertDocumentId}");
                        continue;
                    }

                    // Skip if the alert document is already closed
                    if (alertDocument.Status == ServiceAlertState.Closed.ToString())
                    {
                        _logger.LogInternalInformation($"Alert document {alertDocumentId} is already closed, skipping thread {thread.Id}");
                        continue;
                    }

                    // Match based on alert rule id
                    if (string.IsNullOrEmpty(alertDocument.AlertRuleResourceId) && alertDocument.AlertRuleResourceId == alertRuleResourceId)
                    {
                        _logger.LogInternalInformation($"Found existing active thread {thread.Id} for alert rule {alertRuleResourceId} and target resource {targetResource}");
                        return thread;
                    }
                    else
                    {
                        _logger.LogInternalInformation($"Thread {thread.Id} alert document doesn't match - Document: (Name: {alertDocument.Name}, TargetResource: {alertDocument.TargetResourceId}) vs Incoming: (Name: {alertRuleResourceId}, TargetResource: {targetResource})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Error processing thread {thread.Id} while looking for existing alert rule: {ex.Message}");
                    continue;
                }
            }

            _logger.LogInternalInformation($"No existing active thread found for alert rule {alertRuleResourceId} and target resource {targetResource}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error finding existing thread for alert rule {alert.Properties.Essentials.AlertRule}: {ex.Message}");
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

    private class InvestigationSummaries
    {
        public required string containerTitle { get; set; }
        public required SummaryItem[] summaries { get; set; }
    }

    private class SummaryItem
    {
        public required string title { get; set; }
        public required string summary { get; set; }
        public bool isCollapsed { get; set; }
        public required string status { get; set; }
        public bool isFinal { get; set; }
    }
}
