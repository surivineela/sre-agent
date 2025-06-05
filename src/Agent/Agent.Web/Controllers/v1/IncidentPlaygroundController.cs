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
    private readonly IncidentManagementService<PagerDutyIncidentDocument> _incidentManagementService;
    private readonly ILogger<IncidentPlaygroundController> _logger;

    public IncidentPlaygroundController(
        IInstructionGenerationService instructionGenerationService,
        IncidentManagementService<PagerDutyIncidentDocument> incidentManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        ILogger<IncidentPlaygroundController> logger)
    {
        _incidentManagementService = incidentManagementService;
        _instructionGenerationService = instructionGenerationService;
        _incidentHandlerManagementService = incidentHandlerManagementService;
        _logger = logger;
    }

    [HttpGet("handlers")]
    public async Task<IActionResult> ListIncidentHandlers()
    {
        var handlers = await _incidentHandlerManagementService.ListIncidentHandlers(new List<string>());
        return Ok(handlers);
    }

    [HttpPost("filterHandlers")]
    public async Task<IActionResult> FilterIncidentHandlers([FromBody] List<string> filteringKeywords)
    {
        var handlers = await _incidentHandlerManagementService.ListIncidentHandlers(filteringKeywords);
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
            document.TitleKeywords,
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
        existingHandler.TitleKeywords = document.TitleKeywords;
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

    [HttpPost("queryIncidents")]
    public async Task<IActionResult> QueryIncidents([FromBody] IncidentQueryRequest request)
    {
        try
        {
            if (request == null || request.Keywords == null)
            {
                return BadRequest("Invalid query request");
            }
            var incidents = await _incidentManagementService.QueryIncidents(request.Keywords);
            return Ok(incidents);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error querying incidents");
            return StatusCode(500, "Failed to query incidents");
        }
    }

    [HttpGet("listTools")]
    public async Task<IActionResult> ListTools(string searchString)
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
