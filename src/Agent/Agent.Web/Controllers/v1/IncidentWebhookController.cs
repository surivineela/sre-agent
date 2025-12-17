// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Nodes;
using Agent.Runtime.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class IncidentWebhookController : ControllerBase
{
    private readonly ILogger<IncidentWebhookController> _logger;
    private readonly IIncidentHandlingServiceFactory _incidentHandlingServiceFactory;

    public IncidentWebhookController(
        IIncidentHandlingServiceFactory incidentHandlingServiceFactory,
        ILogger<IncidentWebhookController> logger)
    {
        _incidentHandlingServiceFactory = incidentHandlingServiceFactory;
        _logger = logger;
    }

    //#if DEBUG
    //    [HttpPost("azmonitor")]
    //#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation: webhook
    //    public async Task AzMonitorAlertsWebhook([FromBody] AlertItem alertItem)
    //#pragma warning restore CUSTOM004
    //    {
    //        if (alertItem == null)
    //        {
    //            _logger.LogInternalError("AzMonitorAlertsWebhook: AlertItem is null");
    //            throw new ArgumentNullException(nameof(alertItem), "AlertItem cannot be null");
    //        }

    //    _logger.LogInternalInformation(
    //        "AzMonitorAlertsWebhook: Invoked with AlertId: {AlertId}, Name: {Name}, Type: {Type}",
    //        alertItem.Id, alertItem.Name, alertItem.Type);

    //    try
    //    {
    //        await _azMonitorAlertScanner.ProcessAlertAsync(alertItem, CancellationToken.None);

    //        _logger.LogInternalInformation(
    //            "AzMonitorAlertsWebhook: Successfully processed alert with AlertId: {AlertId}",
    //            alertItem?.Id);
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogInternalError(
    //            ex,
    //            "AzMonitorAlertsWebhook: Error processing alert with AlertId: {AlertId}, Name: {Name}, Type: {Type}",
    //            alertItem?.Id, alertItem?.Name, alertItem?.Type);
    //        throw;
    //    }
    //}
    //#endif

    [HttpPost("processIncident")]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation: webhook
    public async Task<IActionResult> ProcessIncident([FromBody] JsonNode request)
#pragma warning restore CUSTOM004
    {
        _logger.LogInternalInformation("[IncidentWebhookController] ProcessIncident: Process with Request: {Request}", request);

        if (request == null)
        {
            _logger.LogInternalError("[IncidentWebhookController] ProcessIncident: Request is null");
            return BadRequest("Request cannot be null");
        }

        try
        {
            var response = await _incidentHandlingServiceFactory.HandleIncidentAsync(request);
            if (response == null)
            {
                _logger.LogInternalError("[IncidentWebhookController] ProcessIncident: Failed to handle incident for Request: {Request}", request);
                return StatusCode(500, "Failed to handle incident");
            }
            else
            {
                _logger.LogInternalInformation("[IncidentWebhookController] ProcessIncident: Successfully handled incident for Request: {Request}, StatusCode: {StatusCode}",
                request, response.StatusCode);
                return StatusCode(response.StatusCode, response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentWebhookController] ProcessIncident: Error processing incident webhook for Request: {Request}", request);
            return StatusCode(500, "Failed to processIncident");
        }
    }

    [HttpPost("processIncidents")]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation: webhook
    public async Task<IActionResult> ProcessIncidents([FromBody] List<JsonNode> request)
#pragma warning restore CUSTOM004
    {
        _logger.LogInternalInformation("[IncidentWebhookController] ProcessIncidents: Process with Request: {Request}", request);
        if (request == null)
        {
            _logger.LogInternalError("[IncidentWebhookController] ProcessIncidents: Request is null");
            return BadRequest("Request cannot be null");
        }
        try
        {
            var response = await _incidentHandlingServiceFactory.HandleIncidentsAsync(request);
            if (response == null)
            {
                _logger.LogInternalError("[IncidentWebhookController] ProcessIncidents: Failed to handle incidents for Request: {Request}", request);
                return StatusCode(500, "Failed to handle incidents");
            }
            else
            {
                _logger.LogInternalInformation("[IncidentWebhookController] ProcessIncidents: Successfully handled incidents for Request: {Request}, Response: {Response}",
                request, response);
                return Ok(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentWebhookController] ProcessIncidents: Error processing for Request: {Request}", request);
            return StatusCode(500, "Failed to processIncidents");
        }
    }
}
