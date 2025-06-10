// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.Services;
using FirstPartyAgent.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class IncidentPlaygroundController : ControllerBase
{
    private IInstructionGenerationService _instructionGenerationService;
    private readonly IIncidentHandlerManagementService _incidentHandlerManagementService;
    private readonly IIncidentFilterManagementService _incidentFilterManagementService;
    private readonly IncidentManagementService<PagerDutyIncidentDocument> _incidentManagementService;
    private readonly ILogger<IncidentPlaygroundController> _logger;

    public IncidentPlaygroundController(
        IInstructionGenerationService instructionGenerationService,
        IncidentManagementService<PagerDutyIncidentDocument> incidentManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IIncidentFilterManagementService incidentFilterManagementService,
        ILogger<IncidentPlaygroundController> logger)
    {
        _incidentManagementService = incidentManagementService;
        _instructionGenerationService = instructionGenerationService;
        _incidentHandlerManagementService = incidentHandlerManagementService;
        _incidentFilterManagementService = incidentFilterManagementService;
        _logger = logger;
    }

    [HttpGet("checkConnectivity")]
    public async Task<IActionResult> CheckConnectivity()
    {
        try
        {
            // Simple connectivity check
            return Ok(await _incidentFilterManagementService.CheckConnectivity());
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error during connectivity check");
            return Ok(false);
        }
    }

    [HttpGet("filterFieldOptions")]
    public async Task<IActionResult> GetFilterFieldOptions()
    {
        try
        {
            var options = await _incidentFilterManagementService.ListIncidentFilterFieldOptions();
            return Ok(options);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error retrieving filter field options");
            return StatusCode(500, "Failed to retrieve filter field options");
        }
    }

    [HttpGet("handlers")]
    public async Task<IActionResult> ListIncidentHandlers()
    {
        var handlers = await _incidentHandlerManagementService.ListIncidentHandlers();
        return Ok(handlers);
    }

    [HttpPost("queryHandlers")]
    public async Task<IActionResult> QueryIncidentHandlers([FromBody] List<string> filteringKeywords)
    {
        var handlers = await _incidentHandlerManagementService.QueryIncidentHandlers(filteringKeywords);
        return Ok(handlers);
    }

    [HttpGet("handlers/{handlerId}")]
    public async Task<IActionResult> GetIncidentHandler(string handlerId)
    {
        var handler = await _incidentHandlerManagementService.GetIncidentHandler(handlerId);
        if (handler == null)
            return NotFound();
        return Ok(handler);
    }

    [HttpPut("handlers/{handlerId}")]
    public async Task<IActionResult> CreateIncidentHandler([FromBody] IncidentHandlerDocumentPayload document)
    {
        if (document == null || string.IsNullOrEmpty(document.Id))
        {
            return BadRequest("Invalid incident handler document");
        }
        var existingHandler = await _incidentHandlerManagementService.GetIncidentHandler(document.Id);
        if (existingHandler != null)
        {
            return Conflict("Incident handler with the same ID already exists. Use POST to update.");
        }
        var cosmosDocument = new IncidentHandlerDocument(
            document.Id,
            document.Name,
            document.Description,
            document.IncidentFilterId,
            document.IncidentProcessingGuide,
            document.Tools,
            document.Incidents,
            document.CustomInstructions,
            DateTime.UtcNow);

        var saved = await _incidentHandlerManagementService.SaveIncidentHandler(cosmosDocument);
        return Ok(saved);
    }

    [HttpPost("handlers/{handlerId}")]
    public async Task<IActionResult> SaveIncidentHandler([FromBody] IncidentHandlerDocumentPayload document)
    {
        if (document == null || string.IsNullOrEmpty(document.Id))
        {
            return BadRequest("Invalid incident handler document");
        }
        var existingHandler = await _incidentHandlerManagementService.GetIncidentHandler(document.Id);
        if (existingHandler == null)
        {
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

        var saved = await _incidentHandlerManagementService.SaveIncidentHandler(existingHandler);
        return Ok(saved);
    }

    [HttpDelete("handlers/{handlerId}")]
    public async Task<IActionResult> DeleteIncidentHandler(string handlerId)
    {
        var result = await _incidentHandlerManagementService.DeleteIncidentHandler(handlerId);
        if (!result)
            return NotFound();
        return Ok();
    }

    // List all incident filters
    [HttpGet("filters")]
    public async Task<IActionResult> ListIncidentFilters()
    {
        var filters = await _incidentFilterManagementService.ListIncidentFilters();
        return Ok(filters);
    }

    // Get a specific incident filter by ID
    [HttpGet("filters/{filterId}")]
    public async Task<IActionResult> GetIncidentFilter(string filterId)
    {
        var filter = await _incidentFilterManagementService.GetIncidentFilter(filterId);
        if (filter == null)
            return NotFound();
        return Ok(filter);
    }

    // Create a new incident filter (PUT)
    [HttpPut("filters/{filterId}")]
    public async Task<IActionResult> CreateIncidentFilter([FromBody] IncidentFilterDocumentPayload payload)
    {
        if (payload == null || string.IsNullOrEmpty(payload.Id))
        {
            return BadRequest("Invalid incident filter document");
        }
        var existingFilter = await _incidentFilterManagementService.GetIncidentFilter(payload.Id);
        if (existingFilter != null)
        {
            return Conflict("Incident filter with the same ID already exists. Use POST to update.");
        }
        var filterDoc = new IncidentFilterDocument(
            payload.Id,
            DateTime.UtcNow,
            payload.ImpactedService,
            payload.Priority,
            payload.IncidentType,
            payload.AlertId,
            payload.TitleContains,
            true
        );
        var saved = await _incidentFilterManagementService.SaveIncidentFilter(filterDoc);
        return Ok(saved);
    }

    // Update an existing incident filter (POST)
    [HttpPost("filters/{filterId}")]
    public async Task<IActionResult> SaveIncidentFilter([FromBody] IncidentFilterDocumentPayload payload)
    {
        if (payload == null || string.IsNullOrEmpty(payload.Id))
        {
            return BadRequest("Invalid incident filter document");
        }
        var existingFilter = await _incidentFilterManagementService.GetIncidentFilter(payload.Id);
        if (existingFilter == null)
        {
            return NotFound("Incident filter not found. Use PUT to create a new filter.");
        }

        existingFilter.ImpactedService = payload.ImpactedService;
        existingFilter.Priority = payload.Priority;
        existingFilter.IncidentType = payload.IncidentType;
        existingFilter.AlertId = payload.AlertId;
        existingFilter.TitleContains = payload.TitleContains;
        existingFilter.UpdatedAt = DateTime.UtcNow;

        var saved = await _incidentFilterManagementService.SaveIncidentFilter(existingFilter);
        return Ok(saved);
    }

    // Enable an existing incident filter (POST)
    [HttpPost("filters/{filterId}/enable")]
    public async Task<IActionResult> EnableIncidentFilter(string filterId)
    {
        if (filterId == null || string.IsNullOrEmpty(filterId))
        {
            return BadRequest("Invalid incident filter document");
        }
        var existingFilter = await _incidentFilterManagementService.GetIncidentFilter(filterId);
        if (existingFilter == null)
        {
            return NotFound($"Incident filter with id '{filterId}' not found.");
        }

        existingFilter.IsEnabled = true;
        existingFilter.UpdatedAt = DateTime.UtcNow;

        var saved = await _incidentFilterManagementService.SaveIncidentFilter(existingFilter);
        return Ok(saved);
    }

    // Enable an existing incident filter (POST)
    [HttpPost("filters/{filterId}/disable")]
    public async Task<IActionResult> DisableIncidentFilter(string filterId)
    {
        if (filterId == null || string.IsNullOrEmpty(filterId))
        {
            return BadRequest("Invalid incident filter document");
        }
        var existingFilter = await _incidentFilterManagementService.GetIncidentFilter(filterId);
        if (existingFilter == null)
        {
            return NotFound($"Incident filter with id '{filterId}' not found.");
        }

        existingFilter.IsEnabled = false;
        existingFilter.UpdatedAt = DateTime.UtcNow;

        var saved = await _incidentFilterManagementService.SaveIncidentFilter(existingFilter);
        return Ok(saved);
    }

    // Delete an incident filter
    [HttpDelete("filters/{filterId}")]
    public async Task<IActionResult> DeleteIncidentFilter(string filterId)
    {
        var result = await _incidentFilterManagementService.DeleteIncidentFilter(filterId);
        if (!result)
            return NotFound();
        return Ok();
    }

    [HttpPost("queryIncidents")]
    public async Task<IActionResult> QueryIncidents([FromBody] IncidentQueryRequest request)
    {
        try
        {
            if (request == null || request.Keywords == null)
            {
                return BadRequest("Invalid query request");
            }
            var incidents = await _incidentManagementService.QueryIncidents(request);
            return Ok(incidents);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error querying incidents");
            return StatusCode(500, "Failed to query incidents");
        }
    }

    [HttpGet("listTools")]
    public async Task<IActionResult> ListTools(string? searchString)
    {
        try
        {
            var tools = await _instructionGenerationService.FilterTools(searchString);
            return Ok(tools);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error listing tools");
            return StatusCode(500, "Failed to list tools");
        }
    }

    /// <summary>
    /// Handles Generate Instructions requests
    /// </summary>
    [HttpPost("generateInstructions")]
    public async Task<IActionResult> GenerateInstructions([FromBody] InstructionGenerationRequest instructionGenerationRequest)
    {
        try
        {
            var response = await _instructionGenerationService.GenerateInstructionsFromIncidents(instructionGenerationRequest);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error processing incident request");
            return StatusCode(500, "Failed to process incident request");
        }
    }
}
