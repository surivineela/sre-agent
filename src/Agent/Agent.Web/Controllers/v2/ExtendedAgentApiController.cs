// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Core.Helpers.ExtendedAgents;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Validation;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Plugins.Connector;
using Agent.Plugins.Kusto;
using Agent.Plugins.Tools;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models.ExtendedAgents;
using Agent.Runtime.Services;
using Agent.Web.Authorization;
using Agent.Web.Models.ExtendedAgents;
using Agent.Web.Models.ExtendedAgents.Request;
using Agent.Web.Models.ExtendedAgents.Response;
using Agent.Web.Services;
using Agent.Web.Views.v2;
using Agent.Web.ApiResources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ArmOperations = Agent.Core.Constants.ArmOperations;
using ModelContextProtocol.Protocol;

namespace Agent.Web.Controllers.v2;

[ApiController]
[Route("api/v2/extendedAgent")]
public class ExtendedAgentApiController : ControllerBase
{
    private readonly ILogger<ExtendedAgentApiController> _logger;
    private readonly IExtendedAgentApiService _extendedAgentApiService;

    public ExtendedAgentApiController(
        ILogger<ExtendedAgentApiController> logger,
        IExtendedAgentApiService extendedAgentApiService
    )
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
        [FromBody] ApiRequestEnvelope<ExtendedAgentView> request)
    {
        if (request.Type != null && request.Type != "ExtendedAgent")
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (agentName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(agentName, request.Name));
        }

        var existingAgentResult = await _extendedAgentApiService.GetAgentAsync(agentName);

        var model = ExtendedAgentView.CreateModel(request, existingAgentResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdateAgentAsync(agentName, model);

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
        [FromBody] ApiRequestEnvelope<ExtendedAgentView> request)
    {
        if (request.Type != null && request.Type != "ExtendedAgent")
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

        var result = await _extendedAgentApiService.CreateOrUpdateAgentAsync(agentName, model);
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
    public async Task<IActionResult> DeleteAgentAsync(string agentName)
    {
        var result = await _extendedAgentApiService.DeleteAgentAsync(agentName);

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
                Value = result.Response.Select(ExtendedAgentView.CreateApiResponseEnvelope).ToArray()
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("agents/{agentName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [Consumes("application/json")]
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
        [FromBody] ApiRequestEnvelope<ToolView> request)
    {
        if (request.Type != null && request.Type != "ExtendedAgentTool")
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (toolName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(toolName, request.Name));
        }

        var existingToolResult = await _extendedAgentApiService.GetToolAsync(toolName);

        var model = ToolView.CreateModel(request, existingToolResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdateToolAsync(toolName, model);

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
        [FromBody] ApiRequestEnvelope<ToolView> request)
    {
        if (request.Type != null && request.Type != "ExtendedAgentTool")
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

        var result = await _extendedAgentApiService.CreateOrUpdateToolAsync(toolName, model);
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
    public async Task<IActionResult> DeleteToolAsync(string toolName)
    {
        var result = await _extendedAgentApiService.DeleteToolAsync(toolName);

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
                Value = result.Response.Select(ToolView.CreateApiResponseEnvelope).ToArray()
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("tools/{toolName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [Consumes("application/json")]
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
        [FromBody] ApiRequestEnvelope<ConnectorView> request)
    {
        if (request.Type != null && request.Type != "ExtendedAgentConnector")
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (connectorName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(connectorName, request.Name));
        }

        var existingConnectorResult = await _extendedAgentApiService.GetConnectorAsync(connectorName);

        var model = ConnectorView.CreateModel(request, existingConnectorResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdateConnectorAsync(connectorName, model);

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
        [FromBody] ApiRequestEnvelope<ConnectorView> request)
    {
        if (request.Type != null && request.Type != "ExtendedAgentConnector")
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

        var result = await _extendedAgentApiService.CreateOrUpdateConnectorAsync(connectorName, model);
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
    public async Task<IActionResult> DeleteConnectorAsync(string connectorName)
    {
        var result = await _extendedAgentApiService.DeleteConnectorAsync(connectorName);

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
                Value = result.Response.Select(ConnectorView.CreateApiResponseEnvelope).ToArray()
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("connectors/{connectorName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [Consumes("application/json")]
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
        [FromBody] ApiRequestEnvelope<PluginConfigView> request)
    {
        if (request.Type != null && request.Type != "PluginConfig")
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (pluginName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(pluginName, request.Name));
        }

        var existingPluginResult = await _extendedAgentApiService.GetPluginConfigAsync(pluginName);

        var model = PluginConfigView.CreateModel(request, existingPluginResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdatePluginConfigAsync(pluginName, model);

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
        [FromBody] ApiRequestEnvelope<PluginConfigView> request)
    {
        if (request.Type != null && request.Type != "PluginConfig")
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

        var result = await _extendedAgentApiService.CreateOrUpdatePluginConfigAsync(pluginName, model);
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
    public async Task<IActionResult> DeletePluginConfigAsync(string pluginName)
    {
        var result = await _extendedAgentApiService.DeletePluginConfigAsync(pluginName);

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
                Value = result.Response.Select(PluginConfigView.CreateApiResponseEnvelope).ToArray()
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("plugins/{pluginName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [Consumes("application/json")]
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
        [FromBody] ApiRequestEnvelope<CommonPromptView> request)
    {
        if (request.Type != null && request.Type != "CommonPrompt")
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (promptName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(promptName, request.Name));
        }

        var existingPromptResult = await _extendedAgentApiService.GetCommonPromptAsync(promptName);

        var model = CommonPromptView.CreateModel(request, existingPromptResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdateCommonPromptAsync(promptName, model);

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
        [FromBody] ApiRequestEnvelope<CommonPromptView> request)
    {
        if (request.Type != null && request.Type != "CommonPrompt")
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

        var result = await _extendedAgentApiService.CreateOrUpdateCommonPromptAsync(promptName, model);
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
                Value = result.Response.Select(CommonPromptView.CreateApiResponseEnvelope).ToArray()
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("commonprompts/{promptName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [Consumes("application/json")]
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
    public async Task<IActionResult> DeleteCommonPromptAsync(string promptName)
    {
        var result = await _extendedAgentApiService.DeleteCommonPromptAsync(promptName);

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
        [FromBody] ApiRequestEnvelope<CommonToolListView> request)
    {
        if (request.Type != null && request.Type != "CommonToolsList")
        {
            return BadRequest(ErrorMap.InvalidObjectType.CreateErrorEntity(request.Type));
        }

        if (listName != request.Name)
        {
            return BadRequest(ErrorMap.ObjectNameMismatch.CreateErrorEntity(listName, request.Name));
        }

        var existingListResult = await _extendedAgentApiService.GetCommonToolListAsync(listName);

        var model = CommonToolListView.CreateModel(request, existingListResult.Response?.Metadata, null);

        var result = await _extendedAgentApiService.CreateOrUpdateCommonToolListAsync(listName, model);

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
        [FromBody] ApiRequestEnvelope<CommonToolListView> request)
    {
        if (request.Type != null && request.Type != "CommonToolsList")
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

        var result = await _extendedAgentApiService.CreateOrUpdateCommonToolListAsync(listName, model);
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
                Value = result.Response.Select(CommonToolListView.CreateApiResponseEnvelope).ToArray()
            };
            return Ok(responseEnvelope);
        }

        return UnexpectedError();
    }

    [HttpGet("commontoolslists/{listName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [Consumes("application/json")]
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
    public async Task<IActionResult> DeleteCommonToolListAsync(string listName)
    {
        var result = await _extendedAgentApiService.DeleteCommonToolListAsync(listName);

        if (result.IsStatusCodeResult)
        {
            return result.ActionResult;
        }

        return Accepted();
    }

    private IActionResult UnexpectedError()
    {
        var error = ErrorMap.InternalServerError.CreateErrorEntity();
        return StatusCode(StatusCodes.Status500InternalServerError, error);

    }
}
