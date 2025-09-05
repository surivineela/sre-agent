// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Agent.Core.Validation;
using Agent.Core.Helpers.ExtendedAgents;
using Agent.Framework;
using Agent.Framework.Reasoning.Models;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models.ExtendedAgents;
using Agent.Runtime.Services;
using Agent.Web.Models.ExtendedAgents;
using Agent.Web.Models.ExtendedAgents.Response;
using Agent.Web.Services;
using Microsoft.AspNetCore.Mvc;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/extendedAgent")]
public class ExtendedAgentController : ControllerBase
{
    private readonly IResourceDeploymentService _resourceDeploymentService;
    private readonly IExtendedAgentService _extendedAgentService;
    private readonly ILogger<ExtendedAgentController> _logger;
    private readonly IConnectorResolver _connectorResolver;

    public ExtendedAgentController(
         IExtendedAgentService extendedAgentService,
        ILogger<ExtendedAgentController> logger,
        IResourceDeploymentService agentService,
        IConnectorResolver connectorResolver
       )
    {
        _resourceDeploymentService = agentService;
        _logger = logger;
        _extendedAgentService = extendedAgentService;
        _connectorResolver = connectorResolver;
    }

    /// <summary>
    /// Apply agent and tools configuration
    /// </summary>
    /// <returns>Apply response with operation details</returns>
    [HttpPut("apply")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [Consumes("application/yaml", "application/x-yaml", "text/yaml", "text/plain")]
    [ProducesResponseType(typeof(ExtendedAgentApplyResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExtendedAgentApplyResponse>> ApplyAgentConfiguration([FromBody] string yaml)
    {
        try
        {
            // Parse agent section
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlObject = deserializer.Deserialize(new StringReader(yaml));

            var jsonString = JsonSerializer.Serialize(yamlObject);

            // Now parse it into GenericResourceModel
            var generic = JsonSerializer.Deserialize<Core.Validation.GenericResourceModel>(jsonString);

            if (generic == null || string.IsNullOrEmpty(generic.Kind))
            {
                return BadRequest(new ExtendedAgentErrorResponse
                {
                    ErrorCode = "VALIDATION_FAILED",
                    Message = "Invalid YAML format or missing required fields",
                    Details = new ExtendedAgentErrorDetails(
                        [new ExtendedAgentErrorField("yamlContent", "YAML content is required and must contain 'kind' and 'spec' fields")]
                    )
                });
            }

            // Add comprehensive validation using AgentValidationService for agent deployments
            if (generic.Kind?.Equals("AgentDeployment", StringComparison.OrdinalIgnoreCase) == true)
            {
                try
                {
                    var validationService = new AgentValidationService();
                    var validationResult = await validationService.ValidateYamlAsync(yaml, false);

                    if (!validationResult.IsValid)
                    {
                        var errorDetails = validationResult.Errors.Select(error =>
                            new ExtendedAgentErrorField("yaml", error)).ToList();

                        return BadRequest(new ExtendedAgentErrorResponse
                        {
                            ErrorCode = "VALIDATION_FAILED",
                            Message = "Agent validation failed",
                            Details = new ExtendedAgentErrorDetails(errorDetails)
                        });
                    }

                    // Log warnings but don't fail the request
                    if (validationResult.Warnings.Count > 0)
                    {
                        _logger.LogInternalWarning("Agent validation warnings: {Warnings}",
                            string.Join("; ", validationResult.Warnings));
                    }
                }
                catch (Exception validationEx)
                {
                    _logger.LogInternalError(validationEx, "Error during agent validation");
                    return BadRequest(new ExtendedAgentErrorResponse
                    {
                        ErrorCode = "VALIDATION_ERROR",
                        Message = $"Validation error: {validationEx.Message}",
                        Details = new ExtendedAgentErrorDetails([
                            new ExtendedAgentErrorField("validation", validationEx.Message)
                        ])
                    });
                }
            }

            var result = new ExtendedAgentApply();

            // Handle different resource types
            switch (generic.Kind)
            {
                case "AgentConfiguration":
                    // Use AgentYamlParser to properly handle structured YAML
                    var agentDescriptor = AgentYamlParser.ParseAgentYaml(yaml);
                    if (agentDescriptor == null)
                    {
                        return BadRequest(new ExtendedAgentErrorResponse
                        {
                            ErrorCode = "PARSE_FAILED",
                            Message = "Failed to parse agent configuration"
                        });
                    }

                    // Convert to dictionary to access properties for metadata
                    var yamlDict = yamlObject as Dictionary<string, object>;

                    // Create AgentDeploymentModel using the parsed descriptor
                    var agentDeployment = new AgentDeploymentModel
                    {
                        ApiVersion = yamlDict?.TryGetValue("api_version", out var apiVersionObj) == true ?
                            apiVersionObj?.ToString() ?? "azuresre.ai/v1" : "azuresre.ai/v1",
                        Kind = "AgentConfiguration",
                        Metadata = yamlDict?.TryGetValue("metadata", out var metadataObj) == true && metadataObj != null ?
                            JsonSerializer.Deserialize<YamlMetadata>(JsonSerializer.Serialize(metadataObj)) ?? new YamlMetadata() :
                            new YamlMetadata(),
                        Spec = (YamlAgentDescriptor)agentDescriptor
                    };

                    await _resourceDeploymentService.ApplyAsync(agentDeployment);
                    result = new ExtendedAgentApply
                    {
                        Status = ExtendedAgentApplyStatus.Accepted,
                        Message = "Agent and tools deployment initiated",
                        OperationId = "",
                        Timestamp = DateTime.UtcNow,
                        Details = new ExtendedAgentApplyDetails
                        {
                            AgentName = agentDeployment.Spec.Name,
                            ToolsCount = agentDeployment.Spec.Tools?.Count ?? 0,
                        }
                    };
                    break;

                case "ToolList":
                case "ConnectorList":
                case "PluginConfiguration":
                case "CommonToolsList":
                case "CommonPrompt":

                    // For non-agent resources, use the original approach
                    var resource = YamlResourceRouter.DeserializeResource(generic.Kind!, yaml);
                    switch (resource)
                    {
                        case ToolsDeploymentModel tool:
                            await _resourceDeploymentService.ApplyAsync(tool);
                            break;

                        case ConnectorsDeploymentModel connector:
                            await _resourceDeploymentService.ApplyAsync(connector);
                            break;

                        case PluginConfigDeploymentModel pluginConfig:
                            await _resourceDeploymentService.ApplyAsync(pluginConfig);
                            break;

                        case CommonToolsListDeploymentModel commonToolsList:
                            await _resourceDeploymentService.ApplyAsync(commonToolsList);
                            break;

                        case CommonPromptDeploymentModel commonPrompt:
                            await _resourceDeploymentService.ApplyAsync(commonPrompt);
                            break;

                        default:
                            return BadRequest($"Unsupported resource type for kind: {generic.Kind}");
                    }
                    break;

                default:
                    return BadRequest($"Unsupported kind: {generic.Kind}");
            }

            var webResponse = ExtendedAgentApplyResponse.FromRuntime(result);

            return Accepted(webResponse);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all agents
    /// </summary>
    /// <param name="page">Page number (1-based, default: 1)</param>
    /// <param name="limit">Number of agents per page (1-200, default: 50)</param>
    /// <param name="search">Search agents by name or description</param>
    /// <returns>List of agents with pagination</returns>
    [HttpGet("agents")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ExtendedAgentsListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExtendedAgentsListResponse>> ListAgents(
        [FromQuery][Range(1, 200)] int limit = 50,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery] string? search = null)
    {
        try
        {
            var result = await _extendedAgentService.GetAgentsAsync(page, limit, search);
            var webResponse = PaginatedResponse<YamlAgentDescriptor>.FromPaginatedList(result);
            return Ok(webResponse);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in ListAgents");
            return StatusCode(500, new ExtendedAgentErrorResponse
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An internal error occurred while retrieving agents"
            });
        }
    }

    /// <summary>
    /// List all tools
    /// </summary>
    /// <param name="page">Page number (1-based, default: 1)</param>
    /// <param name="limit">Number of tools to return per page (1-200, default: 50)</param>
    /// <param name="search">Search tools by name or description</param>
    /// <returns>List of tools with pagination</returns>
    [HttpGet("tools")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(ExtendedAgentToolsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExtendedAgentToolsResponse>> ListTools(
        [FromQuery][Range(1, 200)] int limit = 50,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery] string? search = null)
    {
        try
        {
            var result = await _extendedAgentService.GetToolsAsync(page, limit, search);
            var response = PaginatedResponse<YamlToolDefinitionBase>.FromPaginatedList(result);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in ListTools");
            return StatusCode(500, new ExtendedAgentErrorResponse
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An internal error occurred while retrieving tools"
            });
        }
    }

    /// <summary>
    /// List all data connectors
    /// </summary>
    /// <returns>List of data connectors</returns>
    [HttpGet("dataconnectors")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(List<DataConnectorBasicInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public ActionResult<List<DataConnectorBasicInfo>> ListDataConnectors()
    {
        try
        {
            var dataConnectors = _connectorResolver.GetAllDataConnectors();
            return Ok(dataConnectors);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in ListDataConnectors");
            return StatusCode(500, new ExtendedAgentErrorResponse
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An internal error occurred while retrieving data connectors"
            });
        }
    }

    /// <summary>
    /// Delete an agent
    /// </summary>
    /// <param name="agentName">The name of the agent to delete</param>
    /// <returns>Delete operation result</returns>
    [HttpDelete("agents/{agentName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [ProducesResponseType(typeof(ExtendedAgentDeleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExtendedAgentDeleteResponse>> DeleteAgent([FromRoute] string agentName)
    {
        try
        {
            var deleted = await _extendedAgentService.DeleteAgentAsync(agentName);

            if (!deleted)
            {
                return NotFound(new ExtendedAgentErrorResponse
                {
                    ErrorCode = "AGENT_NOT_FOUND",
                    Message = $"Agent '{agentName}' not found"
                });
            }

            return Ok(new ExtendedAgentDeleteResponse
            {
                Status = "success",
                Message = $"Agent '{agentName}' successfully deleted",
                ResourceName = agentName,
                ResourceType = "agent"
            });
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in DeleteAgent for agent {AgentName}", agentName);
            return StatusCode(500, new ExtendedAgentErrorResponse
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An internal error occurred while deleting the agent"
            });
        }
    }

    /// <summary>
    /// Delete a tool
    /// </summary>
    /// <param name="toolName">The name of the tool to delete</param>
    /// <returns>Delete operation result</returns>
    [HttpDelete("tools/{toolName}")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [ProducesResponseType(typeof(ExtendedAgentDeleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ExtendedAgentConflictResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteTool([FromRoute] string toolName)
    {
        try
        {
            var (deleted, dependentAgents) = await _extendedAgentService.DeleteToolAsync(toolName);

            if (dependentAgents.Any())
            {
                return Conflict(new ExtendedAgentConflictResponse
                {
                    Status = "conflict",
                    ErrorCode = "TOOL_IN_USE",
                    Message = $"Tool '{toolName}' cannot be deleted because it is used by {dependentAgents.Count} agent(s)",
                    ResourceName = toolName,
                    ResourceType = "tool",
                    ConflictReason = "Tool is referenced by existing agents",
                    DependentAgents = dependentAgents
                });
            }

            if (!deleted)
            {
                return NotFound(new ExtendedAgentErrorResponse
                {
                    ErrorCode = "TOOL_NOT_FOUND",
                    Message = $"Tool '{toolName}' not found"
                });
            }

            return Ok(new ExtendedAgentDeleteResponse
            {
                Status = "success",
                Message = $"Tool '{toolName}' successfully deleted",
                ResourceName = toolName,
                ResourceType = "tool"
            });
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in DeleteTool for tool {ToolName}", toolName);
            return StatusCode(500, new ExtendedAgentErrorResponse
            {
                ErrorCode = "INTERNAL_ERROR",
                Message = "An internal error occurred while deleting the tool"
            });
        }
    }
}
