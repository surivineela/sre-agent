// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Runtime.Services;
using Azure.ResourceManager.AlertsManagement;
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


    public AzMonitorAlertScanner(
        IGraphDBPlugin graphDbPlugin,
        IAzMonitorAlertService azMonitorAlertService,
        IAgentInboundCommunicationService inboundCommunicationService,
        IThreadRepository repository,
        IChatClient chatClient, ILogger<AzMonitorAlertScanner> logger)
    {
        _graphDBPlugin = graphDbPlugin;
        _logger = logger;

        _azMonitorAlertService = azMonitorAlertService;
        _inboundCommunicationService = inboundCommunicationService;
        _repository = repository;
        _chatClient = chatClient;
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
                var newAlerts = await _azMonitorAlertService.PollNewAlertsBySubscriptionId(subscription, 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling Azure Monitor alerts");
        }
    }

    private async Task<Thread> CreateIncidentThread(ServiceAlertResource alert)
    {
        var messageBuilder = new StringBuilder();

        var essentials = alert.Data.Properties.Essentials;

        var alertId = alert.Data.Id.Name;
        var alertRule = essentials.AlertRule;
        var severity = essentials.Severity.ToString();
        var description = essentials.Description;
        var targetResource = essentials.TargetResource;
        var monitorCondition = essentials.MonitorCondition.ToString();
        var monitorService = essentials.MonitorService;
        var startDateTime = essentials.StartOn?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "Unknown";

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

        if (alert.Data.Properties.Context != null)
        {
            incidentMessage += "**Additional Context:**\n";
            var contextJson = System.Text.Json.JsonSerializer.Serialize(
                alert.Data.Properties.Context,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            incidentMessage += $"```json\n{contextJson}\n```\n\n";
        }

        (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
            title: $"Alert - {alertRule}",
            message: incidentMessage,
            agentTypeEnum: AgentTypeEnum.Meta,
            source: ThreadSource.Incident
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
}

