// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Data.DataModels;
using Agent.Plugins;
using Agent.Runtime.Services;
using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Author = Agent.Core.Models.Api.v1.Author;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.SubAgents.AzMonitorAlertAgent;

public class AzMonitorAlertScanner
{
    private readonly ILogger<AzMonitorAlertScanner> _logger;
    private readonly IGraphDBPlugin _graphDBPlugin;
    private readonly IAgentInboundCommunicationService _inboundCommunicationService;
    private readonly IThreadRepository _repository;
    private readonly IChatClient _chatClient;
    private readonly IAzMonitorAlertService _azMonitorAlertService;
    private readonly Container _dbContainer;
    private readonly IGraphDatabaseClient _graphDbClient;


    public AzMonitorAlertScanner(
        IGraphDBPlugin graphDbPlugin,
        IAzMonitorAlertService azMonitorAlertService,
        IAgentInboundCommunicationService inboundCommunicationService,
        IThreadRepository repository,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IGraphDatabaseClient graphDatabaseClient,
        IChatClient chatClient, ILogger<AzMonitorAlertScanner> logger)
    {
        _graphDBPlugin = graphDbPlugin;
        _logger = logger;

        _azMonitorAlertService = azMonitorAlertService;
        _inboundCommunicationService = inboundCommunicationService;
        _repository = repository;
        _chatClient = chatClient;

        _dbContainer = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
        _graphDbClient = graphDatabaseClient;
    }

    public async Task PollNewAlertsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Polling for new Azure Monitor alerts from the last minute");

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
                _logger.LogInformation($"No subscriptions found in the Graph DB.");
                return;
            }

            _logger.LogInformation($"Scanning for Azure Monitor Alerts in the following subscriptions: {string.Join(", ", subscriptions)}");

            foreach (var subscription in subscriptions)
            {
                _logger.LogInformation($"Checking for alerts in subscription: {subscription}");
                var newAlerts = await _azMonitorAlertService.PollNewAlertsBySubscriptionId(subscription, 1);

                int alertCount = newAlerts.Count();
                _logger.LogInformation($"Found {alertCount} alerts in subscription {subscription}");

                foreach (var alert in newAlerts)
                {
                    _logger.LogInformation($"Processing new alert {alert.Id}...");
                    await ProcessAlertAsync(alert);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling Azure Monitor alerts");
        }
    }

    public async Task ProcessAlertAsync(AlertItem alert)
    {
        try
        {
            // save alert in the document db
            var docId = await SaveAlertToDocumentDb(alert);

            // save alert as a node in the graph db and create edge to resource
            await SaveAlertToGraphDb(alert);

            // Create incident thread
            await CreateIncidentThread(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing alert {alert.Id}: {ex.Message}");
        }
    }

    private async Task<string> SaveAlertToDocumentDb(AlertItem alert)
    {
        var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alert.Id, alert.Id);

        if (alertDocument is null)
        {
            _logger.LogInformation($"Creating new incident document for {alert.Id}.");

            var essentials = alert.Properties.Essentials;

            var alertId = alert.Id;
            var alertRule = essentials.AlertRule;
            var severity = essentials.Severity.ToString();
            var description = essentials.Description;
            var targetResource = essentials.TargetResource;
            var monitorCondition = essentials.MonitorCondition.ToString();

            var name = alertRule;
            string targetResourceType = essentials.TargetResourceType;
            string targetResourceId = targetResource;

            var resourceIdentifier = new ResourceIdentifier(targetResource);

            string subscriptionId = resourceIdentifier.SubscriptionId;
            string status = essentials.AlertState.ToString();
            DateTimeOffset createdAt = ParseDateTimeOffset(essentials.StartDateTime);

            var alertResourceId = new ResourceIdentifier(alertId);

            var newAlertDocument = new AzMonitorAlertDocument(
                Id: alertResourceId.Name,
                Name: name,
                Severity: severity,
                TargetResourceType: targetResourceType,
                TargetResourceId: targetResourceId,
                SubscriptionId: subscriptionId,
                Status: status,
                CreatedAt: createdAt
            )
            {
                Description = description,
                UpdatedAt = DateTime.UtcNow
            };

            // Save to database
            try
            {
                var response = await _dbContainer.UpsertItemAsync(
                    newAlertDocument,
                    new PartitionKey(newAlertDocument.PartitionKey)
                );

                _logger.LogInformation($"Alert document created successfully with id: {newAlertDocument.Id}");

                return newAlertDocument.Id;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, $"Error creating alert document in database: {ex.Message}");
                throw;
            }
        }
        else
        {
            _logger.LogInformation($"Alert document already exists with id: {alertDocument.Id}. No new incident created.");
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
            _logger.LogWarning($"Could not parse start time {value}, using current time instead");
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
                _logger.LogWarning($"Alert {alert.Id} has no target resource, skipping graph DB operations");
                return false;
            }

            var alertNode = new AzMonitorAlertNode
            {
                IncidentId = alert.Id,
                UpdateTs = DateTime.UtcNow.Ticks
            };

            _logger.LogInformation($"Adding/updating alert node in graph DB for {alert.Id}");
            var nodeResult = await _graphDbClient.AddOrUpdateNodeAsync(alertNode);

            if (!nodeResult)
            {
                _logger.LogWarning($"Failed to add/update alert node in graph DB for {alert.Id}");
                return false;
            }

            // Create edge between resource and alert
            var edge = new RelatedToIncidentEdge
            {
                SourceNodeId = targetResourceId.ToLowerInvariant(), // Resource is the source
                TargetNodeId = alertNode.GetNodeId(), // Alert is the target
                UpdateTs = DateTime.UtcNow.Ticks
            };

            _logger.LogInformation($"Adding/updating edge in graph DB between resource {targetResourceId} and alert {alert.Id}");
            var edgeResult = await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            if (!edgeResult)
            {
                _logger.LogWarning($"Failed to add/update edge in graph DB between resource {targetResourceId} and alert {alert.Id}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error saving alert {alert.Id} to graph database: {ex.Message}");
            return false;
        }
    }

    private async Task<Thread> CreateIncidentThread(AlertItem alert)
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

        var incidentMessage = $"🚨 **New Azure Monitor Alert Detected**\n\n" +
            $"**Alert ID:** {alertId}\n\n" +
            $"**Alert Rule:** {alertRule}\n\n" +
            $"**Description:** {description}\n\n";

        if (!string.IsNullOrEmpty(severity))
        {
            incidentMessage += $"**Severity:** {severity}\n\n";
        }

        incidentMessage += $"**Monitor Condition:** {monitorCondition}\n\n";
        incidentMessage += $"**Monitor Service:** {monitorService}\n\n";
        incidentMessage += $"**Fired At:** {startDateTime}\n\n";

        if (!string.IsNullOrEmpty(targetResource))
        {
            incidentMessage += $"**Target Resource:** {targetResource}\n\n";
        }

        if (string.IsNullOrEmpty(alert.Properties.Essentials.Description))
        {
            incidentMessage += "**Additional Context:**\n";
            incidentMessage += $"Description: {description}\n";
        }

        incidentMessage += $"Signal Type: {alert.Properties.Essentials.SignalType}\n";
        incidentMessage += $"Resource Group: {alert.Properties.Essentials.TargetResourceGroup}\n";
        incidentMessage += $"Resource Name: {alert.Properties.Essentials.TargetResourceName}\n";
        incidentMessage += $"Resource Type: {alert.Properties.Essentials.TargetResourceType} \n";


        (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
            title: $"Alert - {alertRule}",
            message: incidentMessage,
            agentTypeEnum: AgentTypeEnum.Meta,
            source: ThreadSource.Incident,
            incidentId: alertId
        );

        var agentMessage = $"**Acknowledging the alert**. I'm starting to investigate and see how I can help.";
        await _repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

        await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
            ThreadId: thread.Id,
            AgentContextId: agentContext.Id,
            MessageId: thread.StartMessage.Id,
            Message: messageBuilder.ToString(),
            UserId: "incident-system",
            DisplayName: monitorService.ToString() ?? "Azure Monitor",
            Timestamp: DateTime.UtcNow
        ));

        // acknowledge incident
        await _azMonitorAlertService.AcknowledgeAlert(alertId);

        return thread;
    }

    private async Task<T> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
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
}
