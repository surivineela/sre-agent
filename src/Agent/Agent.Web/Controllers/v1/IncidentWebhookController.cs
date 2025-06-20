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
    private readonly ILogger<IncidentWebhookController> _logger;
    private readonly AzMonitorAlertScanner _azMonitorAlertScanner;
    private readonly IIncidentHandlingService _incidentHandlingService;

    public IncidentWebhookController(
        AzMonitorAlertScanner azMonitorAlertScanner,
        IIncidentHandlingService incidentHandlingService,
        ILogger<IncidentWebhookController> logger)
    {
        _incidentHandlingService = incidentHandlingService;
        _azMonitorAlertScanner = azMonitorAlertScanner;
        _logger = logger;
    }

    /// <summary>
    /// Handles PagerDuty incident webhook notifications
    /// </summary>
    [HttpPost("pagerduty")]
    public async Task<IActionResult> PagerDutyWebhook([FromBody] PagerDutyRequest request)
    {
        _logger.LogInternalInformation(
            "PagerDutyWebhook: Invoked with IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
            request?.IncidentId, request?.Title, request?.Source);

        try
        {
            return await PagerDutyProcessIncident(request);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "PagerDutyWebhook: Error processing PagerDuty webhook for IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
                request?.IncidentId, request?.Title, request?.Source);

            return StatusCode(500, "Failed to process PagerDuty webhook");
        }
    }

    private async Task<IActionResult> PagerDutyProcessIncident(PagerDutyRequest request)
    {
        _logger.LogInternalInformation(
            "PagerDutyProcessIncident: Handling incident with IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
            request?.IncidentId, request?.Title, request?.Source);

        var response = await _incidentHandlingService.HandleIncidentAsync(
            new IncidentHandlingRequestModel()
            {
                IncidentId = request.IncidentId,
                Title = request.Title,
                Description = request.Description,
                Severity = request.Severity,
                Source = request.Source,
                AdditionalProperties = request.AdditionalProperties,
            }
        );

        if (response == null)
        {
            _logger.LogInternalError(
                "PagerDutyProcessIncident: Failed to handle PagerDuty incident for IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
                request?.IncidentId, request?.Title, request?.Source);

            return StatusCode(500, "Failed to handle PagerDuty incident");
        }
        else
        {
            _logger.LogInternalInformation(
                "PagerDutyProcessIncident: Successfully handled PagerDuty incident for IncidentId: {IncidentId}, StatusCode: {StatusCode}",
                request?.IncidentId, response.StatusCode);

            return StatusCode(response.StatusCode, response.Response);
        }
    }

    /// <summary>
    /// Handles Icm incident webhook notifications
    /// </summary>
    [HttpPost("icm")]
    public async Task<IActionResult> IcmIncidentWebhook([FromBody] IncidentRequest request)
    {
        _logger.LogInternalInformation(
            "IcmWebhook: Invoked with IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
            request?.IncidentId, request?.Title, request?.Source);

        try
        {
            return await IcmProcessIncident(request);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "IcmWebhook: Error processing Icm webhook for IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
                request?.IncidentId, request?.Title, request?.Source);

            return StatusCode(500, "Failed to process Icm webhook");
        }
    }

    private async Task<IActionResult> IcmProcessIncident(IncidentRequest request)
    {
        _logger.LogInternalInformation(
            "IcmProcessIncident: Handling incident with IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
            request?.IncidentId, request?.Title, request?.Source);

        var response = await _incidentHandlingService.HandleIncidentAsync(
            new IncidentHandlingRequestModel()
            {
                IncidentId = request.IncidentId,
                Title = request.Title,
                Description = request.Description,
                Severity = request.Severity,
                Source = request.Source,
                AdditionalProperties = request.AdditionalProperties,
            }
        );

        if (response == null)
        {
            _logger.LogInternalError(
                "IcmProcessIncident: Failed to handle Icm incident for IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
                request?.IncidentId, request?.Title, request?.Source);

            return StatusCode(500, "Failed to handle Icm incident");
        }
        else
        {
            _logger.LogInternalInformation(
                "IcmProcessIncident: Successfully handled Icm incident for IncidentId: {IncidentId}, StatusCode: {StatusCode}",
                request?.IncidentId, response.StatusCode);

            return StatusCode(response.StatusCode, response.Response);
        }
    }

#if DEBUG
    [HttpPost("azmonitor")]
    public async Task AzMonitorAlertsWebhook([FromBody] AlertItem alertItem)
    {
        _logger.LogInternalInformation(
            "AzMonitorAlertsWebhook: Invoked with AlertId: {AlertId}, Name: {Name}, Type: {Type}",
            alertItem?.Id, alertItem?.Name, alertItem?.Type);

        try
        {
            await _azMonitorAlertScanner.ProcessAlertAsync(alertItem, CancellationToken.None);

            _logger.LogInternalInformation(
                "AzMonitorAlertsWebhook: Successfully processed alert with AlertId: {AlertId}",
                alertItem?.Id);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "AzMonitorAlertsWebhook: Error processing alert with AlertId: {AlertId}, Name: {Name}, Type: {Type}",
                alertItem?.Id, alertItem?.Name, alertItem?.Type);
            throw;
        }
    }
#endif

}

#region Request Models

/// <summary>
/// Normalize incident requests from different sources.
/// </summary>
public class IncidentRequest
{
    public string? Title { get; set; } = string.Empty;

    public string? Description { get; set; } = string.Empty;

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
