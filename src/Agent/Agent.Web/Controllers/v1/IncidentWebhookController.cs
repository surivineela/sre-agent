// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Agent.Data.DataModels;
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
    public async Task<IActionResult> IncidentWebhook([FromBody] JsonNode request)
#pragma warning restore CUSTOM004
    {
        _logger.LogInternalInformation(
            "IncidentWebhook: Invoked with Request: {Request}",
            request);

        if (request == null)
        {
            _logger.LogInternalError("IncidentWebhook: Request is null");
            throw new ArgumentNullException(nameof(request), "Request cannot be null");
        }

        try
        {
            return await ProcessIncidentAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "IncidentWebhook: Error processing incident webhook for Request: {Request}",
                request);

            return StatusCode(500, "Failed to process incident webhook");
        }
    }

    private async Task<IActionResult> ProcessIncidentAsync(JsonNode request)
    {
        _logger.LogInternalInformation(
            "ProcessIncidentAsync: Handling incident with Request: {Request}",
            request);

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), "Request cannot be null");
        }

        var response = await _incidentHandlingServiceFactory.HandleIncidentAsync(request);

        if (response == null)
        {
            _logger.LogInternalError(
                "ProcessIncidentAsync: Failed to handle incident for Request: {Request}",
                request);

            return StatusCode(500, "Failed to handle incident");
        }
        else
        {
            _logger.LogInternalInformation(
                "ProcessIncidentAsync: Successfully handled incident for Request: {Request}, StatusCode: {StatusCode}",
                request, response.StatusCode);

            return StatusCode(response.StatusCode, response.Response);
        }
    }

    #region Request Models

    public class IcMRequest : IncidentRequest<IcmIncidentFilterDocumentPayload>
    {
        public string? Title { get; set; } = string.Empty;

        public string? Description { get; set; } = string.Empty;

        public string? IncidentId { get; set; }

        public string? Severity { get; set; }

        public string? Source { get; set; }
    }

    public class PagerDutyRequest : IncidentRequest<PagerDutyIncidentFilterDocumentPayload>
    {
        [Required]
        public required string Title { get; set; }

        [Required]
        public required string Description { get; set; }

        public string? IncidentId { set; get; }

        public string? Severity { get; set; }

        public string? Source { get; set; }
    }

    public class ServiceNowRequest : IncidentRequest<ServiceNowIncidentFilterDocumentPayload>
    {
        public string? Title { get; set; }

        public string? Description { get; set; }

        [Required]
        public string? IncidentId { set; get; }

        public string? Severity { get; set; }

        public string? Source { get; set; } = "ServiceNow";
    }
}

#endregion
