// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Agent.Core.Services;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents.AzMonitorAlertAgent;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

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
        if (request == null)
        {
            _logger.LogInternalError("PagerDutyWebhook: PagerDutyRequest is null");
            return StatusCode(500, "PagerDutyRequest is null");
        }

        _logger.LogInternalInformation(
            "PagerDutyWebhook: Invoked with IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
            request.IncidentId, request.Title, request.Source);

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
        if (request == null)
        {
            _logger.LogInternalError("PagerDutyProcessIncident: PagerDutyRequest is null");
            return StatusCode(500, "PagerDutyRequest is null");
        }

        _logger.LogInternalInformation(
            "PagerDutyProcessIncident: Handling incident with IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
            request.IncidentId, request.Title, request.Source);

        var response = await _incidentHandlingService.HandleIncidentAsync(
            new IncidentHandlingRequestModel()
            {
                IncidentId = request.IncidentId ?? string.Empty,
                Title = request.Title,
                Description = request.Description,
                Severity = request.Severity,
                Source = request.Source,
                AdditionalProperties = request.AdditionalProperties,
                IsTest = request.IsTest,
                IncidentFilter = request.IncidentFilter,
                IncidentHandler = request.IncidentHandler
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
    public async Task<IActionResult> IcmIncidentWebhook([FromBody] IcMRequest request)
    {
        if (request == null)
        {
            _logger.LogInternalError("IcmWebhook: IncidentRequest is null");
            return StatusCode(500, "IncidentRequest is null");
        }

        _logger.LogInternalInformation(
            "IcmWebhook: Invoked with IncidentId: {IncidentId}, Title: {Title}, Source: {Source}, IsTest: {IsTest}",
            request.IncidentId, request.Title, request.Source, request.IsTest);

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

    private async Task<IActionResult> IcmProcessIncident(IcMRequest request)
    {
        if (request == null)
        {
            _logger.LogInternalError("IcmProcessIncident: IncidentRequest is null");
            return StatusCode(500, "IncidentRequest is null");
        }

        _logger.LogInternalInformation(
            "IcmProcessIncident: Handling incident with IncidentId: {IncidentId}, Title: {Title}, Source: {Source}, IsTest: {IsTest}",
            request.IncidentId, request.Title, request.Source, request.IsTest);

        var response = await _incidentHandlingService.HandleIncidentAsync(
            new IncidentHandlingRequestModel()
            {
                IncidentId = request.IncidentId ?? string.Empty,
                Title = request.Title ?? string.Empty,
                Description = request.Description ?? string.Empty,
                Severity = request.Severity,
                Source = request.Source,
                AdditionalProperties = request.AdditionalProperties,
                IsTest = request.IsTest,
                IncidentHandler = request.IncidentHandler,
                IncidentFilter = request.IncidentFilter,
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
        if (alertItem == null)
        {
            _logger.LogInternalError("AzMonitorAlertsWebhook: AlertItem is null");
            throw new ArgumentNullException(nameof(alertItem), "AlertItem cannot be null");
        }

        _logger.LogInternalInformation(
            "AzMonitorAlertsWebhook: Invoked with AlertId: {AlertId}, Name: {Name}, Type: {Type}",
            alertItem.Id, alertItem.Name, alertItem.Type);

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

    /// <summary>
    /// Handles ServiceNow incident webhook notifications
    /// </summary>
    [HttpPost("servicenow")]
    public async Task<IActionResult> ServiceNowWebhook([FromBody] ServiceNowRequest request)
    {
        _logger.LogInternalInformation(
            "ServiceNowWebhook: Invoked with IncidentId: {IncidentId}, Title: {Title}, Source: {Source}, IsTest: {IsTest}",
            request?.IncidentId, request?.Title, request?.Source ?? "ServiceNow", request?.IsTest);

        if (request == null)
        {
            _logger.LogInternalError("ServiceNowWebhook: ServiceNowRequest is null");
            throw new ArgumentNullException(nameof(request), "ServiceNowRequest cannot be null");
        }

        try
        {
            return await ServiceNowProcessIncident(request);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "ServiceNowWebhook: Error processing ServiceNow webhook for IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
                request?.IncidentId, request?.Title, request?.Source ?? "ServiceNow");

            return StatusCode(500, "Failed to process ServiceNow webhook");
        }
    }

    private async Task<IActionResult> ServiceNowProcessIncident(ServiceNowRequest request)
    {
        _logger.LogInternalInformation(
            "ServiceNowProcessIncident: Handling incident with IncidentId: {IncidentId}, Title: {Title}, Source: {Source}, IsTest: {IsTest}",
            request?.IncidentId, request?.Title, request?.Source ?? "ServiceNow", request?.IsTest);

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), "ServiceNowRequest cannot be null");
        }

        var response = await _incidentHandlingService.HandleIncidentAsync(
            new IncidentHandlingRequestModel()
            {
                IncidentId = request.IncidentId ?? string.Empty,
                Title = request.Title ?? string.Empty,
                Description = request.Description ?? string.Empty,
                Severity = request.Severity,
                Source = "ServiceNow",
                AdditionalProperties = request.AdditionalProperties,
                IsTest = request.IsTest,
                IncidentFilter = request.IncidentFilter,
                IncidentHandler = request.IncidentHandler
            }
        );

        if (response == null)
        {
            _logger.LogInternalError(
                "ServiceNowProcessIncident: Failed to handle ServiceNow incident for IncidentId: {IncidentId}, Title: {Title}, Source: {Source}",
                request?.IncidentId, request?.Title, request?.Source ?? "ServiceNow");

            return StatusCode(500, "Failed to handle ServiceNow incident");
        }
        else
        {
            _logger.LogInternalInformation(
                "ServiceNowProcessIncident: Successfully handled ServiceNow incident for IncidentId: {IncidentId}, StatusCode: {StatusCode}",
                request?.IncidentId, response.StatusCode);

            return StatusCode(response.StatusCode, response.Response);
        }
    }
}

#region Request Models



public class IcMRequest : IncidentRequest
{
    public string? Title { get; set; } = string.Empty;

    public string? Description { get; set; } = string.Empty;

    public string? IncidentId { get; set; }
    public string? Severity { get; set; }
    public string? Source { get; set; }
}

public class PagerDutyRequest : IncidentRequest
{
    [Required]
    public required string Title { get; set; }

    [Required]
    public required string Description { get; set; }

    public string? IncidentId { set; get; }

    public string? Severity { get; set; }

    public string? Source { get; set; }
}

public class ServiceNowRequest : IncidentRequest
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    [Required]
    public string? IncidentId { set; get; }

    public string? Severity { get; set; }

    public string? Source { get; set; } = "ServiceNow";
}

public class AzMonitorAlertRequest
{
    [JsonProperty("data")]
    public required AlertData Data { get; set; }
}

public class AlertData
{
    [JsonProperty("essentials")]
    public required Essentials Essentials { get; set; }
}

public class Essentials
{
    [JsonProperty("alertId")]
    public required string AlertId { get; set; }

    [JsonProperty("alertRule")]
    public required string AlertRule { get; set; }

    [JsonProperty("severity")]
    public required string Severity { get; set; }

    [JsonProperty("signalType")]
    public required string SignalType { get; set; }

    [JsonProperty("monitorCondition")]
    public required string MonitorCondition { get; set; }

    [JsonProperty("monitoringService")]
    public required string MonitoringService { get; set; }

    [JsonProperty("alertTargetIDs")]
    public required List<string> AlertTargetIDs { get; set; }

    [JsonProperty("firedDateTime")]
    public required string FiredDateTime { get; set; }

    [JsonProperty("description")]
    public required string Description { get; set; }

}

#endregion
