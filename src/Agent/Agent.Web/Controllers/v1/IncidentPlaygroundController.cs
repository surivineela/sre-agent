// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents.IcmScanner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Agent.Web.Authorization;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class IncidentPlaygroundController : ControllerBase
{
    private IInstructionGenerationService _instructionGenerationService;
    private readonly IIncidentHandlerManagementService _incidentHandlerManagementService;
    private readonly IIncidentFilterManagementServiceFactory _incidentFilterManagementServiceFactory;
    private readonly IIncidentManagementServiceFactory _incidentManagementServiceFactory;
    private readonly ILogger<IncidentPlaygroundController> _logger;
    private readonly IncidentManagementSettings _incidentManagementSettings;
    private readonly Container _container;

    public IncidentPlaygroundController(
        IInstructionGenerationService instructionGenerationService,
        IIncidentFilterManagementServiceFactory incidentFilterManagementServiceFactory,
        IIncidentManagementServiceFactory incidentManagementServiceFactory,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IncidentManagementSettings incidentManagementSettings,
        ILogger<IncidentPlaygroundController> logger,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings)
    {
        _instructionGenerationService = instructionGenerationService;
        _incidentHandlerManagementService = incidentHandlerManagementService;
        _incidentFilterManagementServiceFactory = incidentFilterManagementServiceFactory;
        _incidentManagementServiceFactory = incidentManagementServiceFactory;
        _incidentManagementSettings = incidentManagementSettings;
        _logger = logger;
        _container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    }

    [HttpGet("checkConnectivity")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> CheckConnectivity()
    {
        _logger.LogInternalInformation("CheckConnectivity: Invoked");
        try
        {
            _logger.LogInternalInformation("CheckConnectivity: Checking connectivity with IncidentFilterManagementService");
            bool result = false;
            result = await _incidentFilterManagementServiceFactory.GetServiceDynamic().CheckConnectivity();
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
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> GetFilterFieldOptions()
    {
        _logger.LogInternalInformation("GetFilterFieldOptions: Invoked");
        try
        {
            _logger.LogInternalInformation("GetFilterFieldOptions: Listing incident filter field options");
            var options = await _incidentFilterManagementServiceFactory.GetServiceDynamic().ListIncidentFilterFieldOptions();
            //_logger.LogInternalInformation("GetFilterFieldOptions: Retrieved {Count} filter field options", options?.Count ?? 0);
            return Ok(options);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetFilterFieldOptions: Error retrieving filter field options");
            return StatusCode(500, "Failed to retrieve filter field options");
        }
    }

    [HttpGet("handlers")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> ListIncidentHandlers()
    {
        _logger.LogInternalInformation("ListIncidentHandlers: Invoked");
        var handlers = await _incidentHandlerManagementService.ListIncidentHandlers();
        _logger.LogInternalInformation("ListIncidentHandlers: Retrieved {Count} handlers", handlers?.Count ?? 0);
        return Ok(handlers);
    }

    [HttpPost("queryHandlers")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> QueryIncidentHandlers([FromBody] List<string> filteringKeywords)
    {
        _logger.LogInternalInformation("QueryIncidentHandlers: Invoked with FilteringKeywords: {FilteringKeywords}", filteringKeywords);
        var handlers = await _incidentHandlerManagementService.QueryIncidentHandlers(filteringKeywords);
        _logger.LogInternalInformation("QueryIncidentHandlers: Retrieved {Count} handlers", handlers?.Count ?? 0);
        return Ok(handlers);
    }

    [HttpGet("handlers/{handlerId}")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
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
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
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
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
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
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
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
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> ListIncidentFilters()
    {
        _logger.LogInternalInformation("ListIncidentFilters: Invoked");
        var filters = await _incidentFilterManagementServiceFactory.GetServiceDynamic().ListIncidentFilters();
        //_logger.LogInternalInformation("ListIncidentFilters: Retrieved {Count} filters", filters?.Count ?? 0);
        return Ok(filters);
    }

    // Get a specific incident filter by ID
    [HttpGet("filters/{filterId}")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> GetIncidentFilter(string filterId)
    {
        _logger.LogInternalInformation("GetIncidentFilter: Invoked for FilterId: {FilterId}", filterId);
        var filter = await _incidentFilterManagementServiceFactory.GetServiceDynamic().GetIncidentFilter(filterId);
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
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
    public async Task<IActionResult> CreateIncidentFilter([FromBody] JsonNode payload)
    {
        string id = payload?["Id"]?.ToString() ?? string.Empty;
        _logger.LogInternalInformation("CreateIncidentFilter: Invoked for FilterId: {FilterId}", id);
        if (payload is null || string.IsNullOrEmpty(id))
        {
            _logger.LogInternalWarning("CreateIncidentFilter: Invalid incident filter document");
            return BadRequest("Invalid incident filter document");
        }
        var existingFilter = await _incidentFilterManagementServiceFactory.GetServiceDynamic().GetIncidentFilter(id);
        if (existingFilter != null)
        {
            _logger.LogInternalWarning("CreateIncidentFilter: Filter already exists for FilterId: {FilterId}", id);
            return Conflict("Incident filter with the same ID already exists. Use POST to update.");
        }

        var filterDoc = new IncidentFilterDocumentPayload
        {
            Id = id,
            Name = payload["Name"]?.ToString() ?? string.Empty,
            ImpactedService = payload["ImpactedService"]?.ToString() ?? string.Empty,
            Priority = payload["Priority"]?.ToString() ?? string.Empty,
            IncidentType = payload["IncidentType"]?.ToString() ?? string.Empty,
            AlertId = payload["AlertId"]?.ToString() ?? string.Empty,
            TitleContains = payload["TitleContains"]?.ToString() ?? string.Empty,
            AgentMode = payload["AgentMode"]?.ToString() ?? string.Empty,
            OwningTeamId = payload["OwningTeamId"]?.ToString() ?? string.Empty,
            HandlingAgent = payload["HandlingAgent"]?.ToString() ?? string.Empty,
            MaxAutomatedInvestigationAttempts = payload?["MaxAutomatedInvestigationAttempts"]?.GetValue<int>() ?? 3,
            DeepInvestigationEnabled = payload?["DeepInvestigationEnabled"]?.GetValue<bool>() ?? false
        };

        string agentMode = payload?["AgentMode"]?.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(agentMode))
        {
            bool isValid = _incidentFilterManagementServiceFactory.GetServiceDynamic().ValidateAgentMode(agentMode);
            if (isValid)
            {
                filterDoc = filterDoc with { AgentMode = agentMode };
            }
            else
            {
                return BadRequest($"Cannot create filter with {agentMode}");
            }
        }

        var filterDocJNode = JsonSerializer.SerializeToNode(filterDoc);
        MergeJsonNode(payload, filterDocJNode, new List<string>() { "AgentMode" });


        _logger.LogInternalInformation("CreateIncidentFilter: Saving new filter for FilterId: {FilterId}", id);
        if (filterDocJNode is null)
        {
            _logger.LogInternalWarning("CreateIncidentFilter: Filter document serialization failed for FilterId: {FilterId}", id);
            return BadRequest("Failed to serialize incident filter document");
        }
        var saved = await _incidentFilterManagementServiceFactory.SaveIncidentFilter(filterDocJNode);
        _logger.LogInternalInformation("CreateIncidentFilter: Filter created successfully for FilterId: {FilterId}", id);
        return Ok(saved);
    }

    // Update an existing incident filter (POST)
    [HttpPost("filters/{filterId}")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
    public async Task<IActionResult> SaveIncidentFilter([FromBody] JsonNode payload)
    {
        string id = payload?["Id"]?.ToString() ?? string.Empty;
        _logger.LogInternalInformation("SaveIncidentFilter: Invoked for FilterId: {FilterId}", id);
        if (payload is null || string.IsNullOrEmpty(id))
        {
            _logger.LogInternalWarning("SaveIncidentFilter: Invalid incident filter document");
            return BadRequest("Invalid incident filter document");
        }

        IncidentFilterDocumentPayload existingFilter = await _incidentFilterManagementServiceFactory.GetServiceDynamic().GetIncidentFilter(id);
        if (existingFilter is null)
        {
            _logger.LogInternalWarning("SaveIncidentFilter: Filter not found for FilterId: {FilterId}", id);
            return NotFound("Incident filter not found. Use PUT to create a new filter.");
        }

        string agentMode = payload?["AgentMode"]?.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(agentMode))
        {
            bool validateRes = _incidentFilterManagementServiceFactory.GetServiceDynamic().ValidateAgentMode(agentMode);
            if (validateRes)
            {
                existingFilter = existingFilter with { AgentMode = agentMode };
            }
            else
            {
                return BadRequest($"Cannot update handler with {agentMode}");
            }
        }

        var existingDocJNode = JsonSerializer.SerializeToNode(existingFilter);
        MergeJsonNode(payload, existingDocJNode, new List<string>() { "AgentMode" });

        if (existingDocJNode is null)
        {
            _logger.LogInternalWarning("SaveIncidentFilter: Filter document MergeJsonNode failed for FilterId: {FilterId}", id);
            return BadRequest("Failed to serialize incident filter document");
        }

        _logger.LogInternalInformation("SaveIncidentFilter: Saving filter for FilterId: {FilterId}", id);
        var saved = await _incidentFilterManagementServiceFactory.SaveIncidentFilter(existingDocJNode);
        _logger.LogInternalInformation("SaveIncidentFilter: Filter updated successfully for FilterId: {FilterId}", id);
        return Ok(saved);
    }

    // Enable an existing incident filter (POST)
    [HttpPost("filters/{filterId}/enable")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
    public async Task<IActionResult> EnableIncidentFilter(string filterId)
    {
        _logger.LogInternalInformation("EnableIncidentFilter: Invoked for FilterId: {FilterId}", filterId);
        if (filterId == null || string.IsNullOrEmpty(filterId))
        {
            _logger.LogInternalWarning("EnableIncidentFilter: Invalid filterId");
            return BadRequest("Invalid incident filter document");
        }
        var existingFilter = await _incidentFilterManagementServiceFactory.GetServiceDynamic().GetIncidentFilter(filterId);
        if (existingFilter is null)
        {
            _logger.LogInternalWarning("EnableIncidentFilter: Filter not found for FilterId: {FilterId}", filterId);
            return NotFound($"Incident filter with id '{filterId}' not found.");
        }

        existingFilter.IsEnabled = true;
        existingFilter.UpdatedAt = DateTime.UtcNow;

        _logger.LogInternalInformation("EnableIncidentFilter: Enabling filter for FilterId: {FilterId}", filterId);
        var saved = await _incidentFilterManagementServiceFactory.GetServiceDynamic().SaveIncidentFilter(existingFilter);
        _logger.LogInternalInformation("EnableIncidentFilter: Filter enabled for FilterId: {FilterId}", filterId);
        return Ok(saved);
    }

    // Disable an existing incident filter (POST)
    [HttpPost("filters/{filterId}/disable")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
    public async Task<IActionResult> DisableIncidentFilter(string filterId)
    {
        _logger.LogInternalInformation("DisableIncidentFilter: Invoked for FilterId: {FilterId}", filterId);
        if (filterId == null || string.IsNullOrEmpty(filterId))
        {
            _logger.LogInternalWarning("DisableIncidentFilter: Invalid filterId");
            return BadRequest("Invalid incident filter document");
        }
        var existingFilter = await _incidentFilterManagementServiceFactory.GetServiceDynamic().GetIncidentFilter(filterId);
        if (existingFilter is null)
        {
            _logger.LogInternalWarning("DisableIncidentFilter: Filter not found for FilterId: {FilterId}", filterId);
            return NotFound($"Incident filter with id '{filterId}' not found.");
        }

        existingFilter.IsEnabled = false;
        existingFilter.UpdatedAt = DateTime.UtcNow;

        _logger.LogInternalInformation("DisableIncidentFilter: Disabling filter for FilterId: {FilterId}", filterId);
        var saved = await _incidentFilterManagementServiceFactory.GetServiceDynamic().SaveIncidentFilter(existingFilter);
        _logger.LogInternalInformation("DisableIncidentFilter: Filter disabled for FilterId: {FilterId}", filterId);
        return Ok(saved);
    }

    // Delete an incident filter
    [HttpDelete("filters/{filterId}")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
    public async Task<IActionResult> DeleteIncidentFilter(string filterId)
    {
        _logger.LogInternalInformation("DeleteIncidentFilter: Invoked for FilterId: {FilterId}", filterId);
        var result = await _incidentFilterManagementServiceFactory.GetServiceDynamic().DeleteIncidentFilter(filterId);
        if (!result)
        {
            _logger.LogInternalWarning("DeleteIncidentFilter: Filter not found for FilterId: {FilterId}", filterId);
            return NotFound();
        }
        _logger.LogInternalInformation("DeleteIncidentFilter: Filter deleted for FilterId: {FilterId}", filterId);
        return Ok();
    }

    [HttpPost("queryIncidents")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> QueryIncidents([FromBody] JsonNode request)
    {
        _logger.LogInternalInformation("QueryIncidents: Invoked with Request: {Request}", JsonSerializer.Serialize(request));
        try
        {
            if (request is null || (request["Keywords"] is null && request["Filter"] is null))
            {
                _logger.LogInternalWarning("QueryIncidents: Invalid query request");
                return BadRequest("Invalid query request");
            }
            var incidents = await _incidentManagementServiceFactory.QueryIncidents(request);
            return Ok(incidents);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "QueryIncidents: Error querying incidents with Request: {Request}", JsonSerializer.Serialize(request));
            return StatusCode(500, "Failed to query incidents");
        }
    }

    [HttpGet("listTools")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
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
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> GenerateInstructions([FromBody] InstructionGenerationRequest instructionGenerationRequest)
    {
        _logger.LogInternalInformation("GenerateInstructions: Invoked for AgentName: {AgentName}", instructionGenerationRequest.AgentName);
        try
        {
            var response = await _instructionGenerationService.GenerateInstructionsFromIncidents(instructionGenerationRequest);
            _logger.LogInternalInformation("GenerateInstructions: Successfully generated instructions for AgentName: {AgentName}", instructionGenerationRequest.AgentName);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GenerateInstructions: Error processing incident request for AgentName: {AgentName}", instructionGenerationRequest.AgentName);
            return StatusCode(500, "Failed to process incident request");
        }
    }

    // DEPRECATED: This method is scheduled for removal in a future release.
    // Do not use in production environments.
    [HttpPost("restLastScanTimeIcm")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
    public async Task<IActionResult> ResetLastScanTime()
    {
        var doc = new LastScanTimeDoc()
        {
            LastScanTime = DateTime.UtcNow.AddDays(-30)
        };
        try
        {
            await _container.UpsertItemAsync(doc);
            _logger.LogInternalInformation($"[IncidentPlaygroundController] Last scan time reset to: {doc.LastScanTime}");
            return Ok("Reset to last 30 days");
        }
        catch (Exception)
        {
            return StatusCode(500, "Failed to reset LastScanTimeIcm");
        }
    }

    [HttpGet("getIncident/{incidentId}")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> GetIncident(string incidentId)
    {
        _logger.LogInternalInformation("GetIncident: Invoked for IncidentId: {IncidentId}", incidentId);
        if (string.IsNullOrEmpty(incidentId))
        {
            _logger.LogInternalWarning("GetIncident: Invalid incidentId");
            return BadRequest("Invalid incidentId");
        }
        try
        {
            var incident = await _incidentManagementServiceFactory.GetServiceDynamic().GetIncidentDetails(incidentId);
            return Ok(incident);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Failed to getIncident {incidentId}", ex);
            return StatusCode(500, "Failed to getIncident");
        }
    }

    // ---------- Test Scanner Queue (local only) ----------
    public sealed record EnqueueTestScannerRequest(string IncidentId, string? OwningTeamId, bool ForceTeamSpecific = false);

    /// <summary>
    /// Enqueue a test incident for IcmScanner local testing.
    /// Active only when SREAGENT_ENABLE_ICMSCANNER_TEST_QUEUE is enabled.
    /// </summary>
    [HttpPost("testScanner/enqueue")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
    public IActionResult EnqueueTestScannerIncident([FromBody] EnqueueTestScannerRequest req)
    {
        if (!IcmScannerTestQueueHelper.IsEnabled())
        {
            return NotFound("Test queue disabled. Set SREAGENT_ENABLE_ICMSCANNER_TEST_QUEUE=true to enable.");
        }
        if (req is null || string.IsNullOrWhiteSpace(req.IncidentId))
        {
            return BadRequest("IncidentId is required.");
        }

        var ok = IcmScannerTestQueueHelper.Enqueue(req.IncidentId, req.OwningTeamId, req.ForceTeamSpecific);
        if (!ok)
        {
            return BadRequest("Failed to enqueue (possibly disabled or invalid input).");
        }

        _logger.LogInternalInformation("[IncidentPlaygroundController] Enqueued test incident {IncidentId} forceTeamSpecific={Force} owningTeamId={Team}", req.IncidentId, req.ForceTeamSpecific, req.OwningTeamId);
        return Ok(new { enqueued = true, queueLength = IcmScannerTestQueueHelper.Count });
    }

    private void MergeJsonNode(JsonNode? source, JsonNode? target, IEnumerable<string>? excludeProperties = null)
    {
        if (source is null || target is null)
        {
            return;
        }
        foreach (var property in source.AsObject())
        {
            if (excludeProperties is not null && excludeProperties.Any(ep => ep.Equals(property.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            if (target[property.Key] is not null)
            {
                target[property.Key] = property.Value?.DeepClone();
            }
        }
    }
}
