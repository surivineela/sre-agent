// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Web.ApiResources;
using Agent.Web.Authorization;
using Agent.Web.Services;
using Agent.Web.Views.v2;
using Anthropic.Models.Messages;
using Microsoft.AspNetCore.Mvc;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v2;

[ApiController]
[Route("api/v2/IncidentFilter")]
public class IncidentFilterApiController : ControllerBase
{
    private readonly ILogger<IncidentFilterApiController> _logger;
    private readonly IIncidentFilterApiService _incidentFilterApiService;

    public IncidentFilterApiController(
        ILogger<IncidentFilterApiController> logger,
        IIncidentFilterApiService incidentFilterApiService)
    {
        _logger = logger;
        _incidentFilterApiService = incidentFilterApiService;
    }

    [HttpPut("filters/{filterName}")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<IncidentFilterView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrUpdateIncidentFilterAsync(
        string filterName,
        [FromBody] ApiRequestEnvelope<IncidentFilterView> request,
        [FromQuery] bool dryRun = false)
    {
        _logger.LogInternalInformation("CreateOrUpdateIncidentFilter requested for filter: {FilterName}, DryRun: {DryRun}", filterName, dryRun);

        if (filterName != request.Name)
        {
            _logger.LogInternalWarning("Filter name mismatch: URL parameter {UrlFilterName} does not match request body {RequestFilterName}", filterName, request.Name);
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(filterName, request.Name));
        }

        if (request.Type != "IncidentFilter")
        {
            _logger.LogInternalWarning("Invalid resource type: expected 'IncidentFilter', got '{RequestType}'", request.Type);
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity("IncidentFilter", request.Type));
        }

        var existingFilterResult = await _incidentFilterApiService.GetIncidentFilterAsync(filterName);

        // Create metadata from existing filter if it exists
        ResourceMetadata? metadata = null;
        if (existingFilterResult.IsSyncObjectResult)
        {
            metadata = new ResourceMetadata { CreatedAt = existingFilterResult.Response.CreatedAt };
        }

        var model = IncidentFilterView.CreateModel(request, metadata, null);

        var result = await _incidentFilterApiService.CreateOrUpdateIncidentFilterAsync(filterName, model, dryRun);

        if (result.IsStatusCodeResult)
        {
            _logger.LogInternalWarning("CreateOrUpdateIncidentFilter returned status code result for filter: {FilterName}", filterName);
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            _logger.LogInternalInformation("CreateOrUpdateIncidentFilter accepted (async) for filter: {FilterName}", filterName);
            return Accepted(IncidentFilterView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            _logger.LogInternalInformation("CreateOrUpdateIncidentFilter completed successfully for filter: {FilterName}", filterName);
            return Ok(IncidentFilterView.CreateApiResponseEnvelope(result.Response));
        }

        _logger.LogInternalError("CreateOrUpdateIncidentFilter returned unexpected result for filter: {FilterName}", filterName);
        return UnexpectedError();
    }

    [HttpPatch("filters/{filterName}")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<IncidentFilterView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PatchIncidentFilterAsync(
        string filterName,
        [FromBody] ApiRequestEnvelope<IncidentFilterView> request,
        [FromQuery] bool dryRun = false)
    {
        _logger.LogInternalInformation("PatchIncidentFilter requested for filter: {FilterName}, DryRun: {DryRun}", filterName, dryRun);

        if (filterName != request.Name)
        {
            _logger.LogInternalWarning("Filter name mismatch: URL parameter {UrlFilterName} does not match request body {RequestFilterName}", filterName, request.Name);
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(filterName, request.Name));
        }

        if (request.Type != "IncidentFilter")
        {
            _logger.LogInternalWarning("Invalid resource type: expected 'IncidentFilter', got '{RequestType}'", request.Type);
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity("IncidentFilter", request.Type));
        }

        var baseModelResult = await _incidentFilterApiService.GetIncidentFilterAsync(filterName);
        if (baseModelResult.IsStatusCodeResult)
        {
            _logger.LogInternalWarning("PatchIncidentFilter could not retrieve existing filter: {FilterName}", filterName);
            return baseModelResult.ActionResult;
        }

        // Use the existing IIncidentFilterDocument directly as the base model
        var baseDocument = baseModelResult.IsSyncObjectResult
            ? baseModelResult.Response
            : null;

        var model = IncidentFilterView.CreateModel(request, baseModel: baseDocument);

        var result = await _incidentFilterApiService.CreateOrUpdateIncidentFilterAsync(filterName, model, dryRun);
        if (result.IsStatusCodeResult)
        {
            _logger.LogInternalWarning("PatchIncidentFilter returned status code result for filter: {FilterName}", filterName);
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            _logger.LogInternalInformation("PatchIncidentFilter accepted (async) for filter: {FilterName}", filterName);
            return Accepted(IncidentFilterView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            _logger.LogInternalInformation("PatchIncidentFilter completed successfully for filter: {FilterName}", filterName);
            return Ok(IncidentFilterView.CreateApiResponseEnvelope(result.Response));
        }

        _logger.LogInternalError("PatchIncidentFilter returned unexpected result for filter: {FilterName}", filterName);
        return UnexpectedError();
    }

    [HttpDelete("filters/{filterName}")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementDeleteActionId)]
    [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteIncidentFilterAsync(
        string filterName,
        [FromQuery] bool dryRun = false)
    {
        _logger.LogInternalInformation("DeleteIncidentFilter requested for filter: {FilterName}, DryRun: {DryRun}", filterName, dryRun);

        var result = await _incidentFilterApiService.DeleteIncidentFilterAsync(filterName, dryRun);

        if (result.IsStatusCodeResult)
        {
            _logger.LogInternalWarning("DeleteIncidentFilter returned status code result for filter: {FilterName}", filterName);
            return result.ActionResult;
        }

        _logger.LogInternalInformation("DeleteIncidentFilter accepted for filter: {FilterName}", filterName);
        return Accepted();
    }

    /// <summary>
    /// This method is to preprocess incident filter before ListIncidentFilters or GetIncidentFilter
    /// So it can be used to do data migration or other preprocessing tasks
    /// For now, it is used to migrate Priority field to Priorities field
    /// </summary>
    /// <param name="document"></param>
    /// <returns></returns>
    protected virtual async Task<IIncidentFilterDocument> PreProcessIncidentFilterDocument(IIncidentFilterDocument document)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        if (document is IncidentFilterDocumentPayload payload && !string.IsNullOrWhiteSpace(payload.Priority))
        {
            _logger.LogInternalInformation("PreProcessIncidentDocument: Migrating Priority to Priorities for FilterId: {FilterId}", payload.Id);
            var priorities = payload.Priorities ?? new List<string>();
            priorities.Add(payload.Priority);
            priorities = priorities.Distinct().ToList();

            // Update the document directly since it's already an IIncidentFilterDocument
            IIncidentFilterDocument updatedDocument = document switch
            {
                IcmIncidentFilterDocument icm => icm with { Priorities = priorities, Priority = string.Empty },
                AzMonitorIncidentFilterDocument azMonitor => azMonitor with { Priorities = priorities, Priority = string.Empty },
                PagerDutyIncidentFilterDocument pagerDuty => pagerDuty with { Priorities = priorities, Priority = string.Empty },
                ServiceNowIncidentFilterDocument serviceNow => serviceNow with { Priorities = priorities, Priority = string.Empty },
                NullableIncidentFilterDocument nullable => nullable with { Priorities = priorities, Priority = string.Empty },
                _ => document
            };

            var result = await _incidentFilterApiService.CreateOrUpdateIncidentFilterAsync(updatedDocument.Id, updatedDocument, dryRun: false);
            if (result.IsSyncObjectResult)
            {
                return result.Response;
            }
        }
        return document;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [HttpGet("filters")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListIncidentFiltersAsync()
    {
        _logger.LogInternalInformation("ListIncidentFilters requested");

        var result = await _incidentFilterApiService.GetIncidentFiltersAsync();

        if (result.IsStatusCodeResult)
        {
            _logger.LogInternalWarning("ListIncidentFilters returned status code result");
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            var tasks = result.Response.Select(async doc => IncidentFilterView.CreateApiResponseEnvelope(await PreProcessIncidentFilterDocument(doc)));
            var viewList = (await Task.WhenAll(tasks)).ToList();
            var responseEnvelope = new ApiCollectionEnvelope<IncidentFilterView>
            {
                Value = [.. viewList]
            };
            _logger.LogInternalInformation("ListIncidentFilters completed successfully, returning {Count} filters", viewList.Count);
            return Ok(responseEnvelope);
        }

        _logger.LogInternalError("ListIncidentFilters returned unexpected result");
        return UnexpectedError();
    }

    [HttpGet("filters/{filterName}")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    [ProducesResponseType(typeof(ApiRequestEnvelope<IncidentFilterView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetIncidentFilterAsync(string filterName)
    {
        _logger.LogInternalInformation("GetIncidentFilter requested for filter: {FilterName}", filterName);

        var result = await _incidentFilterApiService.GetIncidentFilterAsync(filterName);

        if (result.IsStatusCodeResult)
        {
            _logger.LogInternalWarning("GetIncidentFilter returned status code result for filter: {FilterName}", filterName);
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            _logger.LogInternalInformation("GetIncidentFilter completed successfully for filter: {FilterName}", filterName);
            return Ok(IncidentFilterView.CreateApiResponseEnvelope(await PreProcessIncidentFilterDocument(result.Response)));
        }

        _logger.LogInternalError("GetIncidentFilter returned unexpected result for filter: {FilterName}", filterName);
        return UnexpectedError();
    }

    private IActionResult UnexpectedError()
    {
        var error = ErrorMap.InternalServerError.CreateErrorEntity();
        return StatusCode(StatusCodes.Status500InternalServerError, error);
    }
}
