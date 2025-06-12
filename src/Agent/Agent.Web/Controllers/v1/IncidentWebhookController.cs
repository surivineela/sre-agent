// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Data.Repositories;
using Agent.Graph.Interfaces;
using Agent.Core.Services;
using Agent.Logging;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents.AzMonitorAlertAgent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using Author = Agent.Core.Models.Api.v1.Author;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class IncidentWebhookController : ControllerBase
{
    private readonly IAgentInboundCommunicationService _inboundCommunicationService;
    private readonly IThreadRepository _repository;
    private readonly IChatClient _chatClient;
    private readonly IPagerDutyService _pagerDutyService;
    private readonly IIncidentHandlerManagementService _incidentHandlerManagementService;
    private readonly IIncidentFilterManagementService _incidentFilterManagementService;
    private readonly ILogger<IncidentWebhookController> _logger;
    private readonly AzMonitorAlertScanner _azMonitorAlertScanner;


    public IncidentWebhookController(
        IAgentInboundCommunicationService inboundCommunicationService,
        IThreadRepository repository,
        IChatClient chatClient,
        AzMonitorAlertScanner azMonitorAlertScanner,
        IPagerDutyService pagerDutyService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IIncidentFilterManagementService incidentFilterManagementService,
        ILogger<IncidentWebhookController> logger)
    {
        _inboundCommunicationService = inboundCommunicationService;
        _repository = repository;
        _chatClient = chatClient;
        _pagerDutyService = pagerDutyService;
        _incidentHandlerManagementService = incidentHandlerManagementService;
        _incidentFilterManagementService = incidentFilterManagementService;
        _azMonitorAlertScanner = azMonitorAlertScanner;
        _logger = logger;
    }

    /// <summary>
    /// Handles PagerDuty incident webhook notifications
    /// </summary>
    [HttpPost("pagerduty")]
    public async Task<IActionResult> PagerDutyWebhook([FromBody] PagerDutyRequest request)
    {
        try
        {
            return await PagerDutyProcessIncident(request);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error processing PagerDuty webhook");
            return StatusCode(500, "Failed to process PagerDuty webhook");
        }
    }

    private async Task<IActionResult> PagerDutyProcessIncident(PagerDutyRequest request)
    {
        var pagerDutyIncidentId = request.IncidentId;
        try
        {
            var incidentDetails = await _pagerDutyService.GetPagerDutyIncidentAsync(pagerDutyIncidentId);
            var filters = await _incidentFilterManagementService.ListIncidentFilters();
            var matchingFilters = filters
                .Where(filter =>
                filter.ImpactedService == incidentDetails.ImpactedService.Id
                && filter.Priority == incidentDetails.Priority.Summary
                && filter.IncidentType == incidentDetails.IncidentType.Name
                && (string.IsNullOrWhiteSpace(filter.TitleContains) || (!string.IsNullOrWhiteSpace(filter.TitleContains) && (incidentDetails.Title.Contains(filter.TitleContains, StringComparison.OrdinalIgnoreCase)))))
                .ToList();

            // Do not take any action if user has not configured incident filter for the incident
            if (matchingFilters == null || matchingFilters.Count == 0)
            {
                return NotFound($"No matching incident filters found for this incident.");
            }

            var matchingFilter = matchingFilters.FirstOrDefault();

            var incidentHandlers = await _incidentHandlerManagementService.ListIncidentHandlers();
            var matchingHandler = incidentHandlers.Where(x => x.IncidentFilterId == matchingFilter.Id).FirstOrDefault();

            if (matchingHandler == null)
            {
                var incidentRequest = new PagerDutyRequest
                {
                    Title = incidentDetails.Title ?? "PagerDuty Alert",
                    Description = incidentDetails.Description ?? "Alert notification from PagerDuty",
                    IncidentId = incidentDetails.IncidentId,
                    Severity = incidentDetails.Priority.Summary,
                    Source = "PagerDuty",
                    //AdditionalProperties = request.AdditionalProperties
                };

                var defaultThread = await CreateIncidentMetaAgentThread(incidentRequest);
                return Ok(new { threadId = defaultThread.Id, message = "PagerDuty incident received" });
            }

            // Fallback on the Meta Agent if no Handler has been configured
            var thread = await CreateIncidentHandlerAgentThread(incidentDetails, matchingHandler);
            return Ok(new { threadId = thread.Id, message = "PagerDuty incident received" });
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error processing PagerDuty webhook");
            return StatusCode(500, "Failed to process PagerDuty webhook");
        }
    }

    private async Task<Thread> CreateIncidentHandlerAgentThread(Graph.Interfaces.PagerDutyIncident incidentDetails, IncidentHandlerDocument incidentHandler)
    {
        var title = incidentDetails.Title ?? "PagerDuty Incident";
        var alertMessage = $"🚨 **New PagerDuty Incident Reported**\n\n" +
            $"**Title:** {title}\n\n" +
            $"**Description:** {incidentDetails.Body.Details}\n\n" +
            $"**Incident ID:** {incidentDetails.IncidentId}\n\n" +
            $"**Severity:** {incidentDetails.Priority?.Summary ?? "Unknown"}\n\n" +
            $"**Source:** PagerDuty\n\n";

        var customInstructionsForAlert = incidentHandler.IncidentProcessingGuide != null && incidentHandler.IncidentProcessingGuide.Count > 0 ?
            string.Join("\n", incidentHandler.IncidentProcessingGuide.Select(x => $"* {x}")) :
            "No custom instructions provided for this incident type.";

        alertMessage = 
            $"{alertMessage}\n\n" +
            $"**Custom Instructions for Incident Processing:**\n" +
            $"{customInstructionsForAlert}\n\n" +
            $"**Incident Handler:** {incidentHandler.Name}";

        (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
            title: $"Incident - {title}",
            message: alertMessage,
            agentTypeEnum: AgentTypeEnum.Incident,
            source: ThreadSource.Incident,
            incidentId: incidentDetails.IncidentId ?? string.Empty,
            AllowedTools: incidentHandler.Tools
        );

        var agentMessage = $"**Acknowledging the incident**. I'm starting to investigate and see how I can help.";
        await _repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

        await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
            ThreadId: thread.Id,
            AgentContextId: agentContext.Id,
            MessageId: thread.StartMessage.Id,
            Message: "Process the incident as per custom instructions provided",
            UserId: "incident-system",
            DisplayName: "Incident System",
            Timestamp: DateTime.UtcNow
        ));

        return thread;
    }

    private async Task<Thread> CreateIncidentMetaAgentThread(PagerDutyRequest request)
    {
        var messageBuilder = new StringBuilder();

        var incidentMessage = $"🚨 **New {(!string.IsNullOrEmpty(request.Source) ? request.Source : String.Empty)} Incident Reported**\n\n" +
            $"**Title:** {request.Title}\n\n" +
            $"**Description:** {request.Description}\n\n";

        if (!string.IsNullOrEmpty(request.IncidentId))
        {
            incidentMessage += $"**Incident ID:** {request.IncidentId}\n\n";
        }
        if (!string.IsNullOrEmpty(request.Severity))
        {
            incidentMessage += $"**Severity:** {request.Severity}\n\n";
        }
        if (!string.IsNullOrEmpty(request.Source))
        {
            incidentMessage += $"**Source:** {request.Source}\n\n";
        }
        if (request.AdditionalProperties?.Count > 0)
        {
            incidentMessage += "**Additional Details:**\n";
            foreach (var prop in request.AdditionalProperties)
            {
                incidentMessage += $"- {prop.Key}: {prop.Value}\n";
            }
            incidentMessage += "\n";
        }

        (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
            title: $"Incident Report - {request.Title}",
            message: incidentMessage,
            agentTypeEnum: AgentTypeEnum.Meta,
            source: ThreadSource.Incident,
            incidentId: request.IncidentId ?? string.Empty
        );

        var agentMessage = $"**Acknowledging the incident**. I'm starting to investigate and see how I can help.";
        await _repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

        await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
            ThreadId: thread.Id,
            AgentContextId: agentContext.Id,
            MessageId: thread.StartMessage.Id,
            Message: messageBuilder.ToString(),
            UserId: "incident-system",
            DisplayName: request.Source ?? "Incident System",
            Timestamp: DateTime.UtcNow
        ));

        return thread;
    }


#if DEBUG
    [HttpPost("azmonitor")]
    public async Task AzMonitorAlertsWebhook([FromBody] AlertItem alertItem)
    {
        await _azMonitorAlertScanner.ProcessAlertAsync(alertItem, CancellationToken.None);
    }
#endif

}

#region Request Models

/// <summary>
/// Normalize incident requests from different sources.
/// </summary>
public class IncidentRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public string? IncidentId { get; set; }
    public string? Severity { get; set; }
    public string? Source { get; set; }
    public Dictionary<string, string>? AdditionalProperties { get; set; }
}

public interface IIncidentAdapter
{
    IncidentRequest ToStandardFormat();
}

public class AzMonitorAlertRequest
{
    [JsonProperty("data")]
    public AlertData Data { get; set; }
}

public class AzMonitorAlertRequestAdapter : IIncidentAdapter
{
    private readonly AzMonitorAlertRequest _request;
    public AzMonitorAlertRequestAdapter(AzMonitorAlertRequest alertRequest)
    {
        _request = alertRequest;
    }

    public IncidentRequest ToStandardFormat()
    {
        var description = new StringBuilder();
        if (!string.IsNullOrEmpty(_request.Data?.Essentials?.Description))
        {
            description.AppendLine(_request.Data?.Essentials?.Description);
        }
        else if (_request.Data != null && _request.Data.Essentials != null)
        {
            description.AppendLine($"Alert Rule: {_request.Data.Essentials.AlertRule}");
            description.AppendLine($"Condition: {_request.Data.Essentials.MonitorCondition}");
            description.AppendLine($"Signal Type: {_request.Data.Essentials.SignalType}");
        }

        var additionalProps = new Dictionary<string, string>();
        if (_request.Data?.Essentials != null)
        {
            if (!string.IsNullOrEmpty(_request.Data.Essentials.FiredDateTime))
            {
                additionalProps.Add("Fired At", _request.Data.Essentials.FiredDateTime);
            }
        }

        return new IncidentRequest
        {
            Title = _request.Data?.Essentials?.AlertRule ?? "Azure Monitor Alert",
            Description = description.ToString(),
            IncidentId = _request.Data?.Essentials?.AlertId,
            Severity = _request.Data?.Essentials?.Severity,
            Source = "Azure Monitor",
            AdditionalProperties = additionalProps
        };
    }
}

public class AlertData
{
    [JsonProperty("essentials")]
    public Essentials Essentials { get; set; }
}

public class Essentials
{
    [JsonProperty("alertId")]
    public string AlertId { get; set; }

    [JsonProperty("alertRule")]
    public string AlertRule { get; set; }

    [JsonProperty("severity")]
    public string Severity { get; set; }

    [JsonProperty("signalType")]
    public string SignalType { get; set; }

    [JsonProperty("monitorCondition")]
    public string MonitorCondition { get; set; }

    [JsonProperty("monitoringService")]
    public string MonitoringService { get; set; }

    [JsonProperty("alertTargetIDs")]
    public List<string> AlertTargetIDs { get; set; }

    [JsonProperty("firedDateTime")]
    public string FiredDateTime { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

}

public class PagerDutyRequest
{
    [Required]
    public string Title { get; set; }

    [Required]
    public string Description { get; set; }

    public string? IncidentId { set; get; }

    public string? Severity { get; set; }

    public string? Source { get; set; }

    public Dictionary<string, string>? AdditionalProperties { get; set; }
}

public class PagerDutyRequestAdapter : IIncidentAdapter
{
    private readonly PagerDutyRequest _request;

    public PagerDutyRequestAdapter(PagerDutyRequest request)
    {
        _request = request;
    }
    public IncidentRequest ToStandardFormat()
    {
        return new IncidentRequest
        {
            Title = _request.Title ?? "PagerDuty Alert",
            Description = _request.Description ?? "Alert notification from PagerDuty",
            IncidentId = _request.IncidentId,
            Severity = _request.Severity,
            Source = "PagerDuty",
            AdditionalProperties = _request.AdditionalProperties
        };
    }
}

#endregion
