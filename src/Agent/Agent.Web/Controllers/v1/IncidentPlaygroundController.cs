// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Core.Configuration;
using Agent.Core.Services;
using Agent.Core.Validation;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents.IcmScanner;
using Agent.Runtime.SubAgents.Scanner;
using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
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
    private readonly ISmartFilterService _smartFilterService;
    private readonly ILogger<IncidentPlaygroundController> _logger;
    private readonly IncidentManagementSettings _incidentManagementSettings;
    private readonly Container _container;
    private readonly IConnectorResolver _connectorResolver;
    private readonly IICMAPIClient _icmAPIClient;

    public IncidentPlaygroundController(
        IInstructionGenerationService instructionGenerationService,
        IIncidentFilterManagementServiceFactory incidentFilterManagementServiceFactory,
        IIncidentManagementServiceFactory incidentManagementServiceFactory,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        ISmartFilterService smartFilterService,
        IncidentManagementSettings incidentManagementSettings,
        ILogger<IncidentPlaygroundController> logger,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IConnectorResolver connectorResolver,
        IICMAPIClient icmAPIClient)
    {
        _instructionGenerationService = instructionGenerationService;
        _incidentHandlerManagementService = incidentHandlerManagementService;
        _incidentFilterManagementServiceFactory = incidentFilterManagementServiceFactory;
        _incidentManagementServiceFactory = incidentManagementServiceFactory;
        _smartFilterService = smartFilterService;
        _incidentManagementSettings = incidentManagementSettings;
        _logger = logger;
        _container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
        _connectorResolver = connectorResolver;
        _icmAPIClient = icmAPIClient;
    }

    /// <summary>
    /// Get Agent Space managed identity for incident platform
    /// </summary>
    /// <param name="platform">Required platform identifier (e.g., "ICM")</param>
    /// <returns>Agent Space managed identity ID if configured, otherwise null</returns>
    [HttpGet("agentSpaceIdentity")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public IActionResult GetAgentSpaceIdentity([FromQuery] string platform)
    {
        _logger.LogInternalInformation("GetAgentSpaceIdentity: Invoked with platform {Platform}", platform);
        if (string.IsNullOrWhiteSpace(platform))
        {
            _logger.LogInternalWarning("GetAgentSpaceIdentity: Platform parameter is required");
            return BadRequest("Platform parameter is required");
        }

        try
        {
            var identity = _connectorResolver.GetAgentSpaceDataConnectorIdentity();
            if (string.IsNullOrEmpty(identity))
            {
                _logger.LogInternalInformation("GetAgentSpaceIdentity: No Agent Space identity found for platform {Platform}", platform);
                return Ok(null);
            }

            _logger.LogInternalInformation("GetAgentSpaceIdentity: Found Agent Space identity: {Identity}", identity);
            return Ok(identity);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetAgentSpaceIdentity: Error getting Agent Space identity for platform {Platform}", platform);
            return Ok(null);
        }
    }

    [HttpGet("checkConnectivity")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> CheckConnectivity()
    {
        _logger.LogInternalInformation("CheckConnectivity: Invoked");
        try
        {
            string incidentType = _incidentManagementSettings.Type?.ToString() ?? string.Empty;
            _logger.LogInternalInformation($"CheckConnectivity: Checking connectivity with incident type: {incidentType}");
            bool success = await _incidentFilterManagementServiceFactory.GetServiceDynamic().CheckConnectivity();
            _logger.LogInternalInformation("CheckConnectivity: Connectivity check {Result}", success ? "succeeded" : "failed");
            return Ok(success);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "CheckConnectivity: Error during connectivity check");
            return Ok(false);
        }
    }

    [HttpGet("checkConnectivityDetailed")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> CheckConnectivityDetailed()
    {
        _logger.LogInternalInformation("CheckConnectivityDetailed: Invoked");
        try
        {
            string incidentType = _incidentManagementSettings.Type?.ToString() ?? string.Empty;
            _logger.LogInternalInformation($"CheckConnectivityDetailed: Checking connectivity with incident type: {incidentType}");
            var result = await _incidentFilterManagementServiceFactory.GetServiceDynamic().GetConnectivityStatus();
            bool success = result.Success;
            string? errorMessage = result.ErrorMessage;
            _logger.LogInternalInformation("CheckConnectivityDetailed: Connectivity check completed with result {Result}", success);
            return Ok(new { Success = success, ErrorMessage = errorMessage });
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "CheckConnectivityDetailed: Error during connectivity check");
            return Ok(new { Success = false, ErrorMessage = ex.Message });
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

        var validationResult = ValidateIncidentHandler(document);
        if (!validationResult.IsValid)
        {
            _logger.LogInternalInformation("CreateIncidentHandler: Validation failed for HandlerId: {HandlerId} with errors: {Errors}",
                document.Id, string.Join(", ", validationResult.Errors));
            return BadRequest($"Validation failed: {validationResult}");
        }

        var existingHandler = await _incidentHandlerManagementService.GetIncidentHandler(document.Id);
        if (existingHandler != null)
        {
            _logger.LogInternalWarning("CreateIncidentHandler: Handler already exists for HandlerId: {HandlerId}", document.Id);
            return Conflict("Incident handler with the same ID already exists. Use POST to update.");
        }
        var cosmosDocument = new IncidentHandlerDocument()
        {
            Id = document.Id,
            DocumentType = IncidentHandlerDocument.GetDocumentType(_incidentManagementSettings.Type),
            Name = document.Name,
            Description = document.Description,
            IncidentFilterId = document.IncidentFilterId,
            IncidentProcessingGuide = document.IncidentProcessingGuide,
            Tools = document.Tools,
            Incidents = document.Incidents,
            CustomInstructions = document.CustomInstructions,
            UpdatedAt = DateTime.UtcNow
        };

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

        var validationResult = ValidateIncidentHandler(document);
        if (!validationResult.IsValid)
        {
            _logger.LogInternalInformation("CreateIncidentHandler: Validation failed for HandlerId: {HandlerId} with errors: {Errors}",
                document.Id, string.Join(", ", validationResult.Errors));
            return BadRequest($"Validation failed: {validationResult}");
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
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementDeleteActionId)]
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
            bool isValid = AgentModes.IsModeValid(agentMode);
            if (isValid)
            {
                filterDoc = filterDoc with { AgentMode = agentMode };
            }
            else
            {
                return BadRequest($"Cannot create filter with {agentMode}");
            }
        }

        var filterDocJNode = JsonSerializer.SerializeToNode(filterDoc, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
            bool validateRes = AgentModes.IsModeValid(agentMode);
            if (validateRes)
            {
                existingFilter = existingFilter with { AgentMode = agentMode };
            }
            else
            {
                return BadRequest($"Cannot update handler with {agentMode}");
            }
        }

        var existingDocJNode = JsonSerializer.SerializeToNode(existingFilter, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementDeleteActionId)]
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

    /// <summary>
    /// Get smart filter recommendations for a specific owning team
    /// </summary>
    /// <param name="owningTeamId">The owning team ID to analyze incidents for</param>
    /// <param name="incidentType">Optional incident type to filter by (defaults to null)</param>
    /// <returns>List of recommended handlers based on incident patterns</returns>
    [HttpGet("filters/aiSuggestions/{owningTeamId}")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementWriteActionId)]
    public async Task<IActionResult> GetFilterRecommendations(string owningTeamId, [FromQuery] string? incidentType = null)
    {
        _logger.LogInternalInformation("GetFilterRecommendations: Invoked for OwningTeamId: {OwningTeamId}, IncidentType: {IncidentType}", owningTeamId, incidentType ?? "LiveSite");

        if (_incidentManagementSettings.Type != IncidentManagementType.Icm)
        {
            _logger.LogInternalWarning("GetFilterRecommendations: Only executes for ICM.");
            return BadRequest($"This feature is only implemented for ICM, not {_incidentManagementSettings.Type.ToString()}");
        }

        if (string.IsNullOrWhiteSpace(owningTeamId))
        {
            _logger.LogInternalWarning("GetFilterRecommendations: OwningTeamId parameter is required");
            return BadRequest("OwningTeamId parameter is required");
        }

        try
        {
            var recommendations = await _smartFilterService.GetFilterRecommendations(owningTeamId, incidentType);
            _logger.LogInternalInformation("GetFilterRecommendations: Retrieved {Count} filter recommendations for OwningTeamId: {OwningTeamId}",
                recommendations?.Count ?? 0, owningTeamId);
            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetFilterRecommendations: Error occurred while fetching filter recommendations for OwningTeamId: {OwningTeamId}", owningTeamId);
            return StatusCode(500, $"An error occurred while fetching filter recommendations: {ex.Message}");
        }
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
        var lastScanTimeKey = LastScanTimeDoc.GetLastScanTimeKey(_incidentManagementSettings.Type);
        var lastScanTimeDoc = new LastScanTimeDoc
        {
            Id = lastScanTimeKey,
            DocumentType = lastScanTimeKey,
            PartitionKey = lastScanTimeKey,
            LastScanTime = DateTime.UtcNow.AddDays(-30)
        };
        try
        {
            await _container.UpsertItemAsync(lastScanTimeDoc);
            _logger.LogInternalInformation($"[IncidentPlaygroundController] Last scan time reset to: {lastScanTimeDoc.LastScanTime} for {_incidentManagementSettings.Type}");
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
            var incident = await _incidentManagementServiceFactory.GetServiceDynamic().GetIncidentAsync(incidentId);
            if (incident is null)
            {
                _logger.LogInternalWarning("GetIncident: Incident not found for IncidentId: {IncidentId}", incidentId);
                return NotFound();
            }
            return Ok(incident);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Failed to getIncident {incidentId}", ex);
            return StatusCode(500, "Failed to getIncident");
        }
    }

    [HttpGet("incidentPlatformType")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public IActionResult GetIncidentPlatformType()
    {
        try
        {
            return Ok(new { IncidentPlatformType = _incidentManagementSettings.Type.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Failed to getIncidentPlatformType", ex);
            return StatusCode(500, "Failed to getIncidentPlatformType");
        }
    }

    [HttpPost("icm/searchTeams")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> SearchTeams([FromBody] IcmSearchTeamsRequestPayload searchPayload)
    {
        if (searchPayload is null || string.IsNullOrWhiteSpace(searchPayload.SearchString))
        {
            _logger.LogInternalWarning("SearchTeams: Invalid search payload");
            return BadRequest("Invalid search payload");
        }

        try
        {
            var teams = await _icmAPIClient.SearchTeamsAsync(
                limit: (uint)searchPayload.Top,
                offset: (uint)searchPayload.Skip,
                teamNameContains: searchPayload.SearchString,
                assignableOnly: searchPayload.AssignableOnly,
                withOnCallRotationsOnly: searchPayload.WithOnCallRotationsOnly
            );
            _logger.LogInternalInformation("SearchTeams: Retrieved {Count} teams", teams?.Count ?? 0);
            return Ok(teams);
        }
        catch (Exception ex)
        {
            string incidentPlatformType = _incidentManagementSettings.Type.ToString() ?? string.Empty;
            _logger.LogInternalError(ex, "SearchTeams Error searching teams with payload: {SearchPayload}, IncidentPlatformType: {IncidentPlatformType}", JsonSerializer.Serialize(searchPayload), incidentPlatformType);
            return StatusCode(500, "Failed to search teams");
        }
    }

    [HttpGet("icm/teams/{teamId}")]
    [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
    public async Task<IActionResult> GetTeamById(string teamId)
    {
        if (string.IsNullOrWhiteSpace(teamId))
        {
            _logger.LogInternalWarning("GetTeamById: Invalid teamId");
            return BadRequest("Invalid teamId");
        }
        try
        {
            var team = await _icmAPIClient.GetTeamAsync(teamId);
            if (team is null)
            {
                _logger.LogInternalWarning("GetTeamById: Team not found for TeamId: {TeamId}", teamId);
                return NotFound();
            }
            _logger.LogInternalInformation("GetTeamById: Team found for TeamId: {TeamId}", teamId);
            return Ok(team);
        }
        catch (Exception ex)
        {
            string incidentPlatformType = _incidentManagementSettings.Type.ToString() ?? string.Empty;
            _logger.LogInternalError(ex, "GetTeamById Error getting team with TeamId: {TeamId}, IncidentPlatformType: {IncidentPlatformType}", teamId, incidentPlatformType);
            return StatusCode(500, "Failed to get team by ID");
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
            if (property.Value is not null)
            {
                target[property.Key] = property.Value.DeepClone();
            }
        }
    }

    private ApiValidationResult ValidateIncidentHandler(IncidentHandlerDocumentPayload payload)
    {
        var result = new ApiValidationResult();

        if (payload.Tools?.Count > 30)
        {
            result.AddError("Incident handler cannot have more than 30 tools.");
        }

        return result;
    }
}

public class IcmSearchTeamsRequestPayload
{
    public bool AssignableOnly { get; set; }
    public string SearchString { get; set; } = string.Empty;
    public int Skip { get; set; }
    public bool WithOnCallRotationsOnly { get; set; }
    public int Top { get; set; }
}
