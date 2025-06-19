// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data.DataModels;
using Agent.Logging;
using Agent.Runtime.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class IncidentPlaygroundController : ControllerBase
{
    private IInstructionGenerationService _instructionGenerationService;
    private readonly IIncidentHandlerManagementService _incidentHandlerManagementService;
    private readonly IIncidentFilterManagementService _incidentFilterManagementService;
    private readonly IIncidentManagementService<PagerDutyIncidentDocument> _pagerDutyincidentManagementService;
    private readonly IIncidentManagementService<IcmIncidentDocument> _icmIncidentManagementService;
    private readonly ILogger<IncidentPlaygroundController> _logger;
    private readonly IncidentManagementSettings _incidentManagementSettings;

    public IncidentPlaygroundController(
        IInstructionGenerationService instructionGenerationService,
        IIncidentManagementService<PagerDutyIncidentDocument> pagerDutyIncidentManagementService,
        IIncidentManagementService<IcmIncidentDocument> icmIncidentManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IIncidentFilterManagementService incidentFilterManagementService,
        IncidentManagementSettings incidentManagementSettings,
        ILogger<IncidentPlaygroundController> logger)
    {
        _pagerDutyincidentManagementService = pagerDutyIncidentManagementService;
        _icmIncidentManagementService = icmIncidentManagementService;
        _instructionGenerationService = instructionGenerationService;
        _incidentHandlerManagementService = incidentHandlerManagementService;
        _incidentFilterManagementService = incidentFilterManagementService;
        _incidentManagementSettings = incidentManagementSettings;
        _logger = logger;
    }

    [HttpGet("checkConnectivity")]
    public async Task<IActionResult> CheckConnectivity()
    {
        _logger.LogInternalInformation("CheckConnectivity: Invoked");
        try
        {
            _logger.LogInternalInformation("CheckConnectivity: Checking connectivity with IncidentFilterManagementService");
            var result = await _incidentFilterManagementService.CheckConnectivity();
            _logger.LogInternalInformation("CheckConnectivity: Connectivity check succeeded with result {Result}", result);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "CheckConnectivity: Error during connectivity check");
            return Ok(false);
        }
    }

    [HttpGet("filterFieldOptions")]
    public async Task<IActionResult> GetFilterFieldOptions()
    {
        _logger.LogInternalInformation("GetFilterFieldOptions: Invoked");
        try
        {
            _logger.LogInternalInformation("GetFilterFieldOptions: Listing incident filter field options");
            var options = await _incidentFilterManagementService.ListIncidentFilterFieldOptions();
            _logger.LogInternalInformation("GetFilterFieldOptions: Retrieved {Count} filter field options", options?.Count ?? 0);
            return Ok(options);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetFilterFieldOptions: Error retrieving filter field options");
            return StatusCode(500, "Failed to retrieve filter field options");
        }
    }

    [HttpGet("handlers")]
    public async Task<IActionResult> ListIncidentHandlers()
    {
        _logger.LogInternalInformation("ListIncidentHandlers: Invoked");
        var handlers = await _incidentHandlerManagementService.ListIncidentHandlers();
        _logger.LogInternalInformation("ListIncidentHandlers: Retrieved {Count} handlers", handlers?.Count ?? 0);
        return Ok(handlers);
    }

    [HttpPost("queryHandlers")]
    public async Task<IActionResult> QueryIncidentHandlers([FromBody] List<string> filteringKeywords)
    {
        _logger.LogInternalInformation("QueryIncidentHandlers: Invoked with FilteringKeywords: {FilteringKeywords}", filteringKeywords);
        var handlers = await _incidentHandlerManagementService.QueryIncidentHandlers(filteringKeywords);
        _logger.LogInternalInformation("QueryIncidentHandlers: Retrieved {Count} handlers", handlers?.Count ?? 0);
        return Ok(handlers);
    }

    [HttpGet("handlers/{handlerId}")]
    public async Task<IActionResult> GetIncidentHandler(string handlerId)
    {
        _logger.LogInternalInformation("GetIncidentHandler: Invoked for HandlerId: {HandlerId}", handlerId);
        var handler = await _incidentHandlerManagementService.GetIncidentHandler(handlerId);
        if (handler == null)
        {
            _logger.LogInternalWarning("GetIncidentHandler: Handler not found for HandlerId: {HandlerId}", handlerId);
            return NotFound();
        }
        _logger.LogInternalInformation("GetIncidentHandler: Handler found for HandlerId: {HandlerId}", handlerId);
        return Ok(handler);
    }

    [HttpPut("handlers/{handlerId}")]
    public async Task<IActionResult> CreateIncidentHandler([FromBody] IncidentHandlerDocumentPayload document)
    {
        _logger.LogInternalInformation("CreateIncidentHandler: Invoked for HandlerId: {HandlerId}", document?.Id);
        if (document == null || string.IsNullOrEmpty(document.Id))
        {
            _logger.LogInternalWarning("CreateIncidentHandler: Invalid incident handler document");
            return BadRequest("Invalid incident handler document");
        }
        var existingHandler = await _incidentHandlerManagementService.GetIncidentHandler(document.Id);
        if (existingHandler != null)
        {
            _logger.LogInternalWarning("CreateIncidentHandler: Handler already exists for HandlerId: {HandlerId}", document.Id);
            return Conflict("Incident handler with the same ID already exists. Use POST to update.");
        }
        var cosmosDocument = new IncidentHandlerDocument(
            document.Id,
            $"IncidentHandler{_incidentManagementSettings.Type.ToString()}",
            document.Name,
            document.Description,
            document.IncidentFilterId,
            document.IncidentProcessingGuide,
            document.Tools,
            document.Incidents,
            document.CustomInstructions,
            DateTime.UtcNow);

        _logger.LogInternalInformation("CreateIncidentHandler: Saving new handler for HandlerId: {HandlerId}", document.Id);
        var saved = await _incidentHandlerManagementService.SaveIncidentHandler(cosmosDocument);
        _logger.LogInternalInformation("CreateIncidentHandler: Handler created successfully for HandlerId: {HandlerId}", document.Id);
        return Ok(saved);
    }

    [HttpPost("handlers/{handlerId}")]
    public async Task<IActionResult> SaveIncidentHandler([FromBody] IncidentHandlerDocumentPayload document)
    {
        _logger.LogInternalInformation("SaveIncidentHandler: Invoked for HandlerId: {HandlerId}", document?.Id);
        if (document == null || string.IsNullOrEmpty(document.Id))
        {
            _logger.LogInternalWarning("SaveIncidentHandler: Invalid incident handler document");
            return BadRequest("Invalid incident handler document");
        }
        var existingHandler = await _incidentHandlerManagementService.GetIncidentHandler(document.Id);
        if (existingHandler == null)
        {
            _logger.LogInternalWarning("SaveIncidentHandler: Handler not found for HandlerId: {HandlerId}", document.Id);
            return NotFound("Incident handler not found. Use PUT to create a new handler.");
        }

        existingHandler.Name = document.Name;
        existingHandler.Description = document.Description;
        existingHandler.IncidentFilterId = document.IncidentFilterId;
        existingHandler.IncidentProcessingGuide = document.IncidentProcessingGuide;
        existingHandler.Tools = document.Tools;
        existingHandler.Incidents = document.Incidents;
        existingHandler.CustomInstructions = document.CustomInstructions;
        existingHandler.UpdatedAt = DateTime.UtcNow;

        _logger.LogInternalInformation("SaveIncidentHandler: Saving handler for HandlerId: {HandlerId}", document.Id);
        var saved = await _incidentHandlerManagementService.SaveIncidentHandler(existingHandler);
        _logger.LogInternalInformation("SaveIncidentHandler: Handler updated successfully for HandlerId: {HandlerId}", document.Id);
        return Ok(saved);
    }

    [HttpDelete("handlers/{handlerId}")]
    public async Task<IActionResult> DeleteIncidentHandler(string handlerId)
    {
        _logger.LogInternalInformation("DeleteIncidentHandler: Invoked for HandlerId: {HandlerId}", handlerId);
        var result = await _incidentHandlerManagementService.DeleteIncidentHandler(handlerId);
        if (!result)
        {
            _logger.LogInternalWarning("DeleteIncidentHandler: Handler not found for HandlerId: {HandlerId}", handlerId);
            return NotFound();
        }
        _logger.LogInternalInformation("DeleteIncidentHandler: Handler deleted for HandlerId: {HandlerId}", handlerId);
        return Ok();
    }

    // List all incident filters
    [HttpGet("filters")]
    public async Task<IActionResult> ListIncidentFilters()
    {
        _logger.LogInternalInformation("ListIncidentFilters: Invoked");
        var filters = await _incidentFilterManagementService.ListIncidentFilters();
        _logger.LogInternalInformation("ListIncidentFilters: Retrieved {Count} filters", filters?.Count ?? 0);
        return Ok(filters);
    }

    // Get a specific incident filter by ID
    [HttpGet("filters/{filterId}")]
    public async Task<IActionResult> GetIncidentFilter(string filterId)
    {
        _logger.LogInternalInformation("GetIncidentFilter: Invoked for FilterId: {FilterId}", filterId);
        var filter = await _incidentFilterManagementService.GetIncidentFilter(filterId);
        if (filter == null)
        {
            _logger.LogInternalWarning("GetIncidentFilter: Filter not found for FilterId: {FilterId}", filterId);
            return NotFound();
        }
        _logger.LogInternalInformation("GetIncidentFilter: Filter found for FilterId: {FilterId}", filterId);
        return Ok(filter);
    }

    // Create a new incident filter (PUT)
    [HttpPut("filters/{filterId}")]
    public async Task<IActionResult> CreateIncidentFilter([FromBody] IncidentFilterDocumentPayload payload)
    {
        _logger.LogInternalInformation("CreateIncidentFilter: Invoked for FilterId: {FilterId}", payload?.Id);
        if (payload == null || string.IsNullOrEmpty(payload.Id))
        {
            _logger.LogInternalWarning("CreateIncidentFilter: Invalid incident filter document");
            return BadRequest("Invalid incident filter document");
        }
        var existingFilter = await _incidentFilterManagementService.GetIncidentFilter(payload.Id);
        if (existingFilter != null)
        {
            _logger.LogInternalWarning("CreateIncidentFilter: Filter already exists for FilterId: {FilterId}", payload.Id);
            return Conflict("Incident filter with the same ID already exists. Use POST to update.");
        }
        var filterDoc = new IncidentFilterDocument(
            payload.Id,
            $"IncidentFilter{_incidentManagementSettings.Type.ToString()}",
            DateTime.UtcNow,
            payload.Name,
            payload.ImpactedService,
            payload.Priority,
            payload.IncidentType,
            payload.AlertId,
            payload.TitleContains,
            true
        );
        _logger.LogInternalInformation("CreateIncidentFilter: Saving new filter for FilterId: {FilterId}", payload.Id);
        var saved = await _incidentFilterManagementService.SaveIncidentFilter(filterDoc);
        _logger.LogInternalInformation("CreateIncidentFilter: Filter created successfully for FilterId: {FilterId}", payload.Id);
        return Ok(saved);
    }

    // Update an existing incident filter (POST)
    [HttpPost("filters/{filterId}")]
    public async Task<IActionResult> SaveIncidentFilter([FromBody] IncidentFilterDocumentPayload payload)
    {
        _logger.LogInternalInformation("SaveIncidentFilter: Invoked for FilterId: {FilterId}", payload?.Id);
        if (payload == null || string.IsNullOrEmpty(payload.Id))
        {
            _logger.LogInternalWarning("SaveIncidentFilter: Invalid incident filter document");
            return BadRequest("Invalid incident filter document");
        }
        var existingFilter = await _incidentFilterManagementService.GetIncidentFilter(payload.Id);
        if (existingFilter == null)
        {
            _logger.LogInternalWarning("SaveIncidentFilter: Filter not found for FilterId: {FilterId}", payload.Id);
            return NotFound("Incident filter not found. Use PUT to create a new filter.");
        }

        existingFilter.ImpactedService = payload.ImpactedService;
        existingFilter.Name = payload.Name;
        existingFilter.Priority = payload.Priority;
        existingFilter.IncidentType = payload.IncidentType;
        existingFilter.AlertId = payload.AlertId;
        existingFilter.TitleContains = payload.TitleContains;
        existingFilter.UpdatedAt = DateTime.UtcNow;

        _logger.LogInternalInformation("SaveIncidentFilter: Saving filter for FilterId: {FilterId}", payload.Id);
        var saved = await _incidentFilterManagementService.SaveIncidentFilter(existingFilter);
        _logger.LogInternalInformation("SaveIncidentFilter: Filter updated successfully for FilterId: {FilterId}", payload.Id);
        return Ok(saved);
    }

    // Enable an existing incident filter (POST)
    [HttpPost("filters/{filterId}/enable")]
    public async Task<IActionResult> EnableIncidentFilter(string filterId)
    {
        _logger.LogInternalInformation("EnableIncidentFilter: Invoked for FilterId: {FilterId}", filterId);
        if (filterId == null || string.IsNullOrEmpty(filterId))
        {
            _logger.LogInternalWarning("EnableIncidentFilter: Invalid filterId");
            return BadRequest("Invalid incident filter document");
        }
        var existingFilter = await _incidentFilterManagementService.GetIncidentFilter(filterId);
        if (existingFilter == null)
        {
            _logger.LogInternalWarning("EnableIncidentFilter: Filter not found for FilterId: {FilterId}", filterId);
            return NotFound($"Incident filter with id '{filterId}' not found.");
        }

        existingFilter.IsEnabled = true;
        existingFilter.UpdatedAt = DateTime.UtcNow;

        _logger.LogInternalInformation("EnableIncidentFilter: Enabling filter for FilterId: {FilterId}", filterId);
        var saved = await _incidentFilterManagementService.SaveIncidentFilter(existingFilter);
        _logger.LogInternalInformation("EnableIncidentFilter: Filter enabled for FilterId: {FilterId}", filterId);
        return Ok(saved);
    }

    // Enable an existing incident filter (POST)
    [HttpPost("filters/{filterId}/disable")]
    public async Task<IActionResult> DisableIncidentFilter(string filterId)
    {
        _logger.LogInternalInformation("DisableIncidentFilter: Invoked for FilterId: {FilterId}", filterId);
        if (filterId == null || string.IsNullOrEmpty(filterId))
        {
            _logger.LogInternalWarning("DisableIncidentFilter: Invalid filterId");
            return BadRequest("Invalid incident filter document");
        }
        var existingFilter = await _incidentFilterManagementService.GetIncidentFilter(filterId);
        if (existingFilter == null)
        {
            _logger.LogInternalWarning("DisableIncidentFilter: Filter not found for FilterId: {FilterId}", filterId);
            return NotFound($"Incident filter with id '{filterId}' not found.");
        }

        existingFilter.IsEnabled = false;
        existingFilter.UpdatedAt = DateTime.UtcNow;

        _logger.LogInternalInformation("DisableIncidentFilter: Disabling filter for FilterId: {FilterId}", filterId);
        var saved = await _incidentFilterManagementService.SaveIncidentFilter(existingFilter);
        _logger.LogInternalInformation("DisableIncidentFilter: Filter disabled for FilterId: {FilterId}", filterId);
        return Ok(saved);
    }

    // Delete an incident filter
    [HttpDelete("filters/{filterId}")]
    public async Task<IActionResult> DeleteIncidentFilter(string filterId)
    {
        _logger.LogInternalInformation("DeleteIncidentFilter: Invoked for FilterId: {FilterId}", filterId);
        var result = await _incidentFilterManagementService.DeleteIncidentFilter(filterId);
        if (!result)
        {
            _logger.LogInternalWarning("DeleteIncidentFilter: Filter not found for FilterId: {FilterId}", filterId);
            return NotFound();
        }
        _logger.LogInternalInformation("DeleteIncidentFilter: Filter deleted for FilterId: {FilterId}", filterId);
        return Ok();
    }

    [HttpPost("queryIncidents")]
    public async Task<IActionResult> QueryIncidents([FromBody] IncidentQueryRequest request)
    {
        _logger.LogInternalInformation("QueryIncidents: Invoked with Request: {Request}", Newtonsoft.Json.JsonConvert.SerializeObject(request));
        try
        {
            if (request == null || request.Keywords == null)
            {
                _logger.LogInternalWarning("QueryIncidents: Invalid query request");
                return BadRequest("Invalid query request");
            }
            if (_incidentManagementSettings.Type == IncidentManagementType.PagerDuty)
            {
                _logger.LogInternalInformation("QueryIncidents: Querying PagerDuty incidents");
                var incidents = await _pagerDutyincidentManagementService.QueryIncidents(request);
                _logger.LogInternalInformation("QueryIncidents: Retrieved {Count} PagerDuty incidents", incidents?.Items.Count ?? 0);
                return Ok(incidents);
            }
            else if (_incidentManagementSettings.Type == IncidentManagementType.Icm)
            {
                _logger.LogInternalInformation("QueryIncidents: Querying ICM incidents");
                var incidents = await _icmIncidentManagementService.QueryIncidents(request);
                _logger.LogInternalInformation("QueryIncidents: Retrieved {Count} ICM incidents", incidents?.Items.Count ?? 0);
                return Ok(incidents);
            }
            else
            {
                _logger.LogInternalWarning("QueryIncidents: Incident management type '{Type}' is not implemented", _incidentManagementSettings.Type);
                return StatusCode(500, $"Incident management type '{_incidentManagementSettings.Type}' is not implemented.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "QueryIncidents: Error querying incidents with Request: {Request}", Newtonsoft.Json.JsonConvert.SerializeObject(request));
            return StatusCode(500, "Failed to query incidents");
        }
    }

    [HttpGet("listTools")]
    public async Task<IActionResult> ListTools(string? searchString)
    {
        _logger.LogInternalInformation("ListTools: Invoked with SearchString: {SearchString}", searchString);
        try
        {
            var tools = await _instructionGenerationService.FilterTools(searchString);
            _logger.LogInternalInformation("ListTools: Retrieved {Count} tools", tools?.Count ?? 0);
            return Ok(tools);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ListTools: Error listing tools with SearchString: {SearchString}", searchString);
            return StatusCode(500, "Failed to list tools");
        }
    }

    /// <summary>
    /// Handles Generate Instructions requests
    /// </summary>
    [HttpPost("generateInstructions")]
    public async Task<IActionResult> GenerateInstructions([FromBody] InstructionGenerationRequest instructionGenerationRequest)
    {
        _logger.LogInternalInformation("GenerateInstructions: Invoked for AgentName: {AgentName}", instructionGenerationRequest?.AgentName);
        try
        {
            var response = await _instructionGenerationService.GenerateInstructionsFromIncidents(instructionGenerationRequest);
            _logger.LogInternalInformation("GenerateInstructions: Successfully generated instructions for AgentName: {AgentName}", instructionGenerationRequest?.AgentName);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GenerateInstructions: Error processing incident request for AgentName: {AgentName}", instructionGenerationRequest?.AgentName);
            return StatusCode(500, "Failed to process incident request");
        }
    }
}
