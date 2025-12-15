// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Web.ApiResources;
using Agent.Web.Authorization;
using Agent.Web.Models.ExtendedAgents;
using Agent.Web.Models.ExtendedAgents.Request;
using Agent.Web.Services;
using Agent.Web.Views.v2;
using Microsoft.AspNetCore.Mvc;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v2;

[ApiController]
[Route("api/v2/extendedAgent")]
public class ExtendedAgentApiController : ControllerBase
{
    private readonly ILogger<ExtendedAgentApiController> _logger;
    private readonly IExtendedAgentApiService _extendedAgentApiService;

    public ExtendedAgentApiController(
        ILogger<ExtendedAgentApiController> logger,
        IExtendedAgentApiService extendedAgentApiService)
    {
        _logger = logger;
        _extendedAgentApiService = extendedAgentApiService;
    }

    [HttpPut("agents/{agentName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<ExtendedAgentView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrUpdateAgentAsync(
        string agentName,
        [FromBody] ApiRequestEnvelope<ExtendedAgentView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != AgentDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (agentName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(agentName, request.Name));
        }

        var existingAgentResult = await _extendedAgentApiService.GetAgentAsync(agentName);

        var model = ExtendedAgentView.CreateModel(request, existingAgentResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdateAgentAsync(agentName, model, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(ExtendedAgentView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(ExtendedAgentView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpPatch("agents/{agentName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<ExtendedAgentView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PatchAgentAsync(
        string agentName,
        [FromBody] ApiRequestEnvelope<ExtendedAgentView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != AgentDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (agentName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(agentName, request.Name));
        }

        var baseModelResult = await _extendedAgentApiService.GetAgentAsync(agentName);
        if (baseModelResult.IsStatusCodeResult)
        {
            return baseModelResult.ActionResult;
        }

        var model = ExtendedAgentView.CreateModel(request, baseModel: baseModelResult.Response);

        var result = await _extendedAgentApiService.CreateOrUpdateAgentAsync(agentName, model, dryRun);
        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(ExtendedAgentView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(ExtendedAgentView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpDelete("agents/{agentName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentDeleteActionId)]
    [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteAgentAsync(
        string agentName,
        [FromQuery] bool dryRun = false)
    {
        var result = await _extendedAgentApiService.DeleteAgentAsync(agentName, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        return Accepted();
    }

    [HttpGet("agents")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListAgentsAsync(
        [FromQuery] int limit = 50,
        [FromQuery] string? search = null)
    {
        var result = await _extendedAgentApiService.GetAgentsAsync(limit, search);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            var responseEnvelope = new ApiCollectionEnvelope<ExtendedAgentView>
            {
                Value = [.. result.Response.Select(ExtendedAgentView.CreateApiResponseEnvelope)]
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("agents/{agentName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ApiRequestEnvelope<ExtendedAgentView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAgentAsync(string agentName)
    {
        var result = await _extendedAgentApiService.GetAgentAsync(agentName);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(ExtendedAgentView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    // Tool endpoints
    [HttpPut("tools/{toolName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<ToolView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrUpdateToolAsync(
        string toolName,
        [FromBody] ApiRequestEnvelope<ToolView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != ToolDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (toolName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(toolName, request.Name));
        }

        var existingToolResult = await _extendedAgentApiService.GetToolAsync(toolName);

        var model = ToolView.CreateModel(request, existingToolResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdateToolAsync(toolName, model, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(ToolView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(ToolView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpPatch("tools/{toolName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<ToolView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PatchToolAsync(
        string toolName,
        [FromBody] ApiRequestEnvelope<ToolView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != ToolDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (toolName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(toolName, request.Name));
        }

        var baseModelResult = await _extendedAgentApiService.GetToolAsync(toolName);
        if (baseModelResult.IsStatusCodeResult)
        {
            return baseModelResult.ActionResult;
        }

        var model = ToolView.CreateModel(request, baseModel: baseModelResult.Response);

        var result = await _extendedAgentApiService.CreateOrUpdateToolAsync(toolName, model, dryRun);
        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(ToolView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(ToolView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpDelete("tools/{toolName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentDeleteActionId)]
    [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteToolAsync(
        string toolName,
        [FromQuery] bool dryRun = false)
    {
        var result = await _extendedAgentApiService.DeleteToolAsync(toolName, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        return Accepted();
    }

    [HttpGet("tools")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListToolsAsync(
        [FromQuery] int limit = 50,
        [FromQuery] string? search = null)
    {
        var result = await _extendedAgentApiService.GetToolsAsync(limit, search);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            var responseEnvelope = new ApiCollectionEnvelope<ToolView>
            {
                Value = [.. result.Response.Select(ToolView.CreateApiResponseEnvelope)]
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("tools/{toolName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ApiRequestEnvelope<ToolView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetToolAsync(string toolName)
    {
        var result = await _extendedAgentApiService.GetToolAsync(toolName);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(ToolView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    // Connector endpoints
    [HttpPut("connectors/{connectorName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<ConnectorView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrUpdateConnectorAsync(
        string connectorName,
        [FromBody] ApiRequestEnvelope<ConnectorView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != ConnectorDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (connectorName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(connectorName, request.Name));
        }

        var existingConnectorResult = await _extendedAgentApiService.GetConnectorAsync(connectorName);

        var model = ConnectorView.CreateModel(request, existingConnectorResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdateConnectorAsync(connectorName, model, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(ConnectorView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(ConnectorView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpPatch("connectors/{connectorName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<ConnectorView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PatchConnectorAsync(
        string connectorName,
        [FromBody] ApiRequestEnvelope<ConnectorView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != ConnectorDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (connectorName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(connectorName, request.Name));
        }

        var baseModelResult = await _extendedAgentApiService.GetConnectorAsync(connectorName);
        if (baseModelResult.IsStatusCodeResult)
        {
            return baseModelResult.ActionResult;
        }

        var model = ConnectorView.CreateModel(request, baseModel: baseModelResult.Response);

        var result = await _extendedAgentApiService.CreateOrUpdateConnectorAsync(connectorName, model, dryRun);
        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(ConnectorView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(ConnectorView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpDelete("connectors/{connectorName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentDeleteActionId)]
    [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteConnectorAsync(
        string connectorName,
        [FromQuery] bool dryRun = false)
    {
        var result = await _extendedAgentApiService.DeleteConnectorAsync(connectorName, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        return Accepted();
    }

    [HttpGet("connectors")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListConnectorsAsync(
        [FromQuery] int limit = 50,
        [FromQuery] string? search = null)
    {
        var result = await _extendedAgentApiService.GetConnectorsAsync(limit, search);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            var responseEnvelope = new ApiCollectionEnvelope<ConnectorView>
            {
                Value = [.. result.Response.Select(ConnectorView.CreateApiResponseEnvelope)]
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("connectors/{connectorName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ApiRequestEnvelope<ConnectorView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetConnectorAsync(string connectorName)
    {
        var result = await _extendedAgentApiService.GetConnectorAsync(connectorName);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(ConnectorView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpGet("connectors/{connectorName}/status")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ConnectorStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetConnectorStatusAsync(string connectorName)
    {
        var result = await _extendedAgentApiService.GetConnectorStatusAsync(connectorName);
        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }
        if (result.IsSyncObjectResult)
        {
            return Ok(result.Response);
        }
        return UnexpectedError();
    }

    // Plugin endpoints
    [HttpPut("plugins/{pluginName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<PluginConfigView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrUpdatePluginConfigAsync(
        string pluginName,
        [FromBody] ApiRequestEnvelope<PluginConfigView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != PlugInConfigDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (pluginName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(pluginName, request.Name));
        }

        var existingPluginResult = await _extendedAgentApiService.GetPluginConfigAsync(pluginName);

        var model = PluginConfigView.CreateModel(request, existingPluginResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdatePluginConfigAsync(pluginName, model, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(PluginConfigView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(PluginConfigView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpPatch("plugins/{pluginName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<PluginConfigView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PatchPluginConfigAsync(
        string pluginName,
        [FromBody] ApiRequestEnvelope<PluginConfigView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != PlugInConfigDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        var baseModelResult = await _extendedAgentApiService.GetPluginConfigAsync(pluginName);
        if (baseModelResult.IsStatusCodeResult)
        {
            return baseModelResult.ActionResult;
        }

        if (pluginName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(pluginName, request.Name));
        }

        var model = PluginConfigView.CreateModel(request, baseModel: baseModelResult.Response);

        var result = await _extendedAgentApiService.CreateOrUpdatePluginConfigAsync(pluginName, model, dryRun);
        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(PluginConfigView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(PluginConfigView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpDelete("plugins/{pluginName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentDeleteActionId)]
    [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeletePluginConfigAsync(
        string pluginName,
        [FromQuery] bool dryRun = false)
    {
        var result = await _extendedAgentApiService.DeletePluginConfigAsync(pluginName, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        return Accepted();
    }

    [HttpGet("plugins")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListPluginConfigsAsync(
        [FromQuery] int limit = 50,
        [FromQuery] string? search = null)
    {
        var result = await _extendedAgentApiService.GetPluginConfigsAsync(limit, search);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            var responseEnvelope = new ApiCollectionEnvelope<PluginConfigView>
            {
                Value = [.. result.Response.Select(PluginConfigView.CreateApiResponseEnvelope)]
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("plugins/{pluginName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ApiRequestEnvelope<PluginConfigView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPluginConfigAsync(string pluginName)
    {
        var result = await _extendedAgentApiService.GetPluginConfigAsync(pluginName);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(PluginConfigView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    // CommonPrompt endpoints
    [HttpPut("commonprompts/{promptName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<CommonPromptView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrUpdateCommonPromptAsync(
        string promptName,
        [FromBody] ApiRequestEnvelope<CommonPromptView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != CommonPromptDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (promptName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(promptName, request.Name));
        }

        var existingPromptResult = await _extendedAgentApiService.GetCommonPromptAsync(promptName);

        var model = CommonPromptView.CreateModel(request, existingPromptResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdateCommonPromptAsync(promptName, model, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(CommonPromptView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(CommonPromptView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpPatch("commonprompts/{promptName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<CommonPromptView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PatchCommonPromptAsync(
        string promptName,
        [FromBody] ApiRequestEnvelope<CommonPromptView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != CommonPromptDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (promptName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(promptName, request.Name));
        }

        var baseModelResult = await _extendedAgentApiService.GetCommonPromptAsync(promptName);
        if (baseModelResult.IsStatusCodeResult)
        {
            return baseModelResult.ActionResult;
        }

        var model = CommonPromptView.CreateModel(request, baseModel: baseModelResult.Response);

        var result = await _extendedAgentApiService.CreateOrUpdateCommonPromptAsync(promptName, model, dryRun);
        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(CommonPromptView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(CommonPromptView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpGet("commonprompts")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListCommonPromptsAsync(
        [FromQuery] int limit = 50,
        [FromQuery] string? search = null)
    {
        var result = await _extendedAgentApiService.GetCommonPromptsAsync(limit, search);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            var responseEnvelope = new ApiCollectionEnvelope<CommonPromptView>
            {
                Value = [.. result.Response.Select(CommonPromptView.CreateApiResponseEnvelope)]
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("commonprompts/{promptName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ApiRequestEnvelope<CommonPromptView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCommonPromptAsync(string promptName)
    {
        var result = await _extendedAgentApiService.GetCommonPromptAsync(promptName);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(CommonPromptView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpDelete("commonprompts/{promptName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentDeleteActionId)]
    [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteCommonPromptAsync(
        string promptName,
        [FromQuery] bool dryRun = false)
    {
        var result = await _extendedAgentApiService.DeleteCommonPromptAsync(promptName, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        return Accepted();
    }

    // CommonToolList endpoints
    [HttpPut("commontoolslists/{listName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<CommonToolListView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrUpdateCommonToolListAsync(
        string listName,
        [FromBody] ApiRequestEnvelope<CommonToolListView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != CommonToolsListDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (listName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(listName, request.Name));
        }

        var existingListResult = await _extendedAgentApiService.GetCommonToolListAsync(listName);

        var model = CommonToolListView.CreateModel(request, existingListResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdateCommonToolListAsync(listName, model, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(CommonToolListView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(CommonToolListView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpPatch("commontoolslists/{listName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<CommonToolListView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PatchCommonToolListAsync(
        string listName,
        [FromBody] ApiRequestEnvelope<CommonToolListView> request,
        [FromQuery] bool dryRun = false)
    {
        if (request.Type != null && request.Type != CommonToolsListDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (listName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(listName, request.Name));
        }

        var baseModelResult = await _extendedAgentApiService.GetCommonToolListAsync(listName);
        if (baseModelResult.IsStatusCodeResult)
        {
            return baseModelResult.ActionResult;
        }

        var model = CommonToolListView.CreateModel(request, baseModel: baseModelResult.Response);

        var result = await _extendedAgentApiService.CreateOrUpdateCommonToolListAsync(listName, model, dryRun);
        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(CommonToolListView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(CommonToolListView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpGet("commontoolslists")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListCommonToolListsAsync(
        [FromQuery] int limit = 50,
        [FromQuery] string? search = null)
    {
        var result = await _extendedAgentApiService.GetCommonToolListsAsync(limit, search);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            var responseEnvelope = new ApiCollectionEnvelope<CommonToolListView>
            {
                Value = [.. result.Response.Select(CommonToolListView.CreateApiResponseEnvelope)]
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("commontoolslists/{listName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ApiRequestEnvelope<CommonToolListView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCommonToolListAsync(string listName)
    {
        var result = await _extendedAgentApiService.GetCommonToolListAsync(listName);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(CommonToolListView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpDelete("commontoolslists/{listName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentDeleteActionId)]
    [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteCommonToolListAsync(
        string listName,
        [FromQuery] bool dryRun = false)
    {
        var result = await _extendedAgentApiService.DeleteCommonToolListAsync(listName, dryRun);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        return Accepted();
    }

    // Skill endpoints
    [HttpPut("skills/{skillName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<SkillView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrUpdateSkillAsync(
        string skillName,
        [FromBody] ApiRequestEnvelope<SkillView> request)
    {
        if (request.Type != null && request.Type != SkillDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (skillName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(skillName, request.Name));
        }

        var existingSkillResult = await _extendedAgentApiService.GetSkillAsync(skillName);

        var model = SkillView.CreateModel(request, existingSkillResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdateSkillAsync(skillName, model);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(SkillView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(SkillView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpPatch("skills/{skillName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiRequestEnvelope<SkillView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PatchSkillAsync(
        string skillName,
        [FromBody] ApiRequestEnvelope<SkillView> request)
    {
        if (request.Type != null && request.Type != SkillDocumentModel.DocumentTypeName)
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (skillName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(skillName, request.Name));
        }

        var baseModelResult = await _extendedAgentApiService.GetSkillAsync(skillName);
        if (baseModelResult.IsStatusCodeResult)
        {
            return baseModelResult.ActionResult;
        }

        var model = SkillView.CreateModel(request, baseModel: baseModelResult.Response);

        var result = await _extendedAgentApiService.CreateOrUpdateSkillAsync(skillName, model);
        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsAsyncCreated)
        {
            return Accepted(SkillView.CreateApiResponseEnvelope(result.Response));
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(SkillView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpDelete("skills/{skillName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentDeleteActionId)]
    [ProducesResponseType(typeof(void), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSkillAsync(string skillName)
    {
        var result = await _extendedAgentApiService.DeleteSkillAsync(skillName);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        return Accepted();
    }

    [HttpGet("skills")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListSkillsAsync(
        [FromQuery] int limit = 50,
        [FromQuery] string? search = null)
    {
        var result = await _extendedAgentApiService.GetSkillsAsync(limit, search);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            var responseEnvelope = new ApiCollectionEnvelope<SkillView>
            {
                Value = [.. result.Response.Select(SkillView.CreateApiResponseEnvelope)]
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("skills/{skillName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ApiRequestEnvelope<SkillView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSkillAsync(string skillName)
    {
        var result = await _extendedAgentApiService.GetSkillAsync(skillName);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(SkillView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    [HttpPost("agents/{agentName}/convert-to-skill")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [ProducesResponseType(typeof(ApiRequestEnvelope<SkillView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEntity), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ConvertToSkillAsync(
        string agentName,
        [FromBody] ConvertToSkillRequest request)
    {
        var result = await _extendedAgentApiService.ConvertAgentToSkillAsync(agentName, request.TopLevelAgents);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        if (result.IsSyncObjectResult)
        {
            return Ok(SkillView.CreateApiResponseEnvelope(result.Response));
        }

        return UnexpectedError();
    }

    private IActionResult UnexpectedError()
    {
        var error = ErrorMap.InternalServerError.CreateErrorEntity();
        return StatusCode(StatusCodes.Status500InternalServerError, error);
    }
}
