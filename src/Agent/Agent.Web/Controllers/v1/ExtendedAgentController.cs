// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Core.Helpers.ExtendedAgents;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Validation;
using Agent.Framework;
using Agent.Framework.Reasoning.Models;
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
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
    private readonly IAuthenticationService _authenticationService;
    private readonly IChatClient _chatClient;
    private readonly IInstructionGenerationService _instructionGenerationService;
    private readonly IToolFactory<AgentContext> _toolFactory;

    public ExtendedAgentController(
         IExtendedAgentService extendedAgentService,
        ILogger<ExtendedAgentController> logger,
        IResourceDeploymentService agentService,
        IConnectorResolver connectorResolver,
        IAuthenticationService authenticationService,
        IChatClient chatClient,
        IInstructionGenerationService instructionGenerationService,
        IToolFactory<AgentContext> toolFactory
       )
    {
        _resourceDeploymentService = agentService;
        _logger = logger;
        _extendedAgentService = extendedAgentService;
        _connectorResolver = connectorResolver;
        _authenticationService = authenticationService;
        _chatClient = chatClient; // default chat client injected via DI
        _instructionGenerationService = instructionGenerationService;
        _toolFactory = toolFactory;
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

            var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(new StringReader(yaml));

            var jsonString = JsonSerializer.Serialize(yamlDict);

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
                    // Validate YAML structure before parsing
                    var structureValidationErrors = _extendedAgentService.ValidateYamlStructure(yamlDict);
                    if (structureValidationErrors.Count > 0)
                    {
                        var errorDetails = structureValidationErrors.Select(error =>
                            new ExtendedAgentErrorField("yaml", error)).ToList();

                        return BadRequest(new ExtendedAgentErrorResponse
                        {
                            ErrorCode = "YAML_STRUCTURE_INVALID",
                            Message = "YAML structure validation failed",
                            Details = new ExtendedAgentErrorDetails(errorDetails)
                        });
                    }

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
                            McpToolsCount = agentDeployment.Spec.McpTools?.Count ?? 0
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
    /// List all extendedagent tools
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
            var response = ExtendedAgentToolsResponse.FromRuntime(result);
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
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentDeleteActionId)]
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
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentDeleteActionId)]
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

    /// <summary>
    /// Test a Kusto query with provided parameters and query definition
    /// </summary>
    /// <param name="request">Test request with query, connector, and parameters</param>
    /// <returns>Query results limited to 50 rows</returns>
    [HttpPost("tools/{toolName}/test")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(KustoQueryTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<KustoQueryTestResponse>> TestKustoQuery(
        [FromRoute] string toolName,
        [FromBody] KustoQueryTestRequest request)
    {
        try
        {
            // Validate tool name - only Kusto is supported for now
            if (!toolName.Equals("kusto", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ExtendedAgentErrorResponse
                {
                    ErrorCode = "UNSUPPORTED_TOOL_TYPE",
                    Message = $"Tool type '{toolName}' is not supported for testing. Only 'kusto' is currently supported."
                });
            }

            // Validate request
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new ExtendedAgentErrorResponse
                {
                    ErrorCode = "INVALID_REQUEST",
                    Message = "Query is required"
                });
            }

            if (string.IsNullOrWhiteSpace(request.Connector))
            {
                return BadRequest(new ExtendedAgentErrorResponse
                {
                    ErrorCode = "INVALID_REQUEST",
                    Message = "Connector is required"
                });
            }

            if (string.IsNullOrWhiteSpace(request.Database))
            {
                return BadRequest(new ExtendedAgentErrorResponse
                {
                    ErrorCode = "INVALID_REQUEST",
                    Message = "Database is required"
                });
            }

            // Get the connector
            var connector = _connectorResolver.GetConnectorFromSettings<KustoConnector>(
                request.Connector,
                "kusto",
                request.Database);

            // Substitute parameters in the query
            var processedQuery = request.Query;
            foreach (var param in request.Parameters)
            {
                var placeholder = $"##{param.Key}##";
                processedQuery = processedQuery.Replace(placeholder, param.Value, StringComparison.OrdinalIgnoreCase);
            }

            // Enforce limit by appending "| take 50" if not already present
            var trimmedQuery = processedQuery.Trim();
            if (!trimmedQuery.Contains("| take", StringComparison.OrdinalIgnoreCase))
            {
                processedQuery = $"{processedQuery}\n| take 50";
            }

            // Create KustoClient and execute the query
            var kustoLogger = HttpContext.RequestServices.GetRequiredService<ILogger<KustoClient>>();
            var kustoClient = new KustoClient(kustoLogger, connector, _authenticationService);

            var startTime = DateTime.UtcNow;
            var queryResult = await kustoClient.ExecuteClusterKustoQuery(
                connector.ClusterUrl,
                request.Database,
                processedQuery,
                HttpContext.RequestAborted);
            var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            // Parse the result string to extract columns and rows
            var columns = new List<string>();
            var rows = new List<Dictionary<string, object>>();

            if (!string.IsNullOrWhiteSpace(queryResult.Result))
            {
                var lines = queryResult.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    // First line contains column names
                    columns = lines[0].Split('\t').ToList();

                    // Remaining lines are data rows
                    for (int i = 1; i < Math.Min(lines.Length, 51); i++) // Take at most 50 rows (+ 1 for header)
                    {
                        var values = lines[i].Split('\t');
                        var rowDict = new Dictionary<string, object>();
                        for (int j = 0; j < Math.Min(columns.Count, values.Length); j++)
                        {
                            rowDict[columns[j]] = values[j];
                        }
                        rows.Add(rowDict);
                    }
                }
            }

            var response = new KustoQueryTestResponse
            {
                Success = queryResult.Success,
                RowCount = queryResult.RowCount,
                Columns = columns,
                Rows = rows,
                ExecutionTimeMs = executionTime,
                QueryExecuted = processedQuery,
                ErrorMessage = queryResult.Success ? null : queryResult.Result
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in TestKustoQuery");

            // Return error as a failed query result
            return Ok(new KustoQueryTestResponse
            {
                Success = false,
                RowCount = 0,
                Columns = new List<string>(),
                Rows = new List<Dictionary<string, object>>(),
                ExecutionTimeMs = 0,
                QueryExecuted = request.Query,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Improves a system prompt for an extended agent and provides validation warnings
    /// </summary>
    [HttpPost("prompt-improvement")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    public async Task<ActionResult<PromptImprovementResponse>> ImprovePrompt([FromBody] PromptImprovementRequest request)
    {
        try
        {
            // Build a single composite prompt (system + instructions + user content) and request JSON output.
            var systemPrompt = """
You are a world class prompt and context engineering expert specializing in Azure SRE Operations agents.

Your task is to improve system prompts for extended agents that help Site Reliability Engineers extend Azure SRE Agent to their own systems to diagnose and resolve incidents, and perform operational tasks.

Required Element:
- Goal: Every prompt MUST have a clear, specific goal. If missing, add a critical warning.
- Handoff guidance: Provide a concise handoff description summarizing when other agents should delegate to this agent.

Optional Elements (suggest ONLY if they add genuine value):
- Role definition
- Specific tasks/capabilities
- Output format expectations
- Edge case handling
- Tone/style guidelines
- Constraints/limitations

Important: Keep simple prompts simple. Do not bloat straightforward prompts.

Return ONLY valid JSON with this shape:
{
    "improvedPrompt": "Improved version here",
    "warnings": ["List any critical issues (e.g. missing goal)"],
    "suggestions": ["Concise, situational improvements"],
    "handoffDescription": "Clear guidance on when to hand off to this agent, should be under 1024 chars"
}

User Prompt To Improve (between <<< and >>>):
<<<
""";

            var prompt = systemPrompt + request.Prompt + "\n>>>";

            var chatOptions = new ChatOptions { Temperature = 1.0f };
            var chatResponse = await _chatClient.GetResponseAsync(prompt, chatOptions, HttpContext.RequestAborted);
            var content = chatResponse?.GetMessage()?.Text ?? string.Empty;

            // Log the raw content for debugging
            _logger.LogInternalInformation("Raw LLM response content: {Content}", content);

            // Parse JSON response with case-insensitive options
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonResponse = JsonSerializer.Deserialize<PromptImprovementResponse>(content, jsonOptions);
            if (jsonResponse == null)
            {
                return BadRequest("Failed to parse response");
            }

            // Log the parsed response for debugging
            _logger.LogInternalInformation("Parsed response - ImprovedPrompt length: {ImprovedPromptLength}, Warnings: {WarningsCount}, Suggestions: {SuggestionsCount}",
                jsonResponse.ImprovedPrompt?.Length ?? 0,
                jsonResponse.Warnings?.Count ?? 0,
                jsonResponse.Suggestions?.Count ?? 0);

            return Ok(jsonResponse);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in ImprovePrompt");
            return StatusCode(500, new PromptImprovementResponse
            {
                ImprovedPrompt = request.Prompt,
                Warnings = new List<string> { "Failed to improve prompt due to an error" },
                Suggestions = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// List all available system tools
    /// </summary>
    /// <param name="search">Search tools by name or description</param>
    /// <returns>List of system tools</returns>
    [HttpGet("systemtools")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(List<Agent.Framework.ToolInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Agent.Framework.ToolInfo>>> ListSystemTools([FromQuery] string? search = null)
    {
        try
        {
            _logger.LogInternalInformation("ListSystemTools: Invoked with search: {Search}", search);

           // Get all tools (system + extended)
            var allTools = await Task.Run(() => _toolFactory.FetchAvailableToolInfo());

            // Get extended tools from database to filter them out
            var extendedToolsData = await _extendedAgentService.GetToolsAsync(1, 1000, null);
            var extendedToolNames = new HashSet<string>(
                extendedToolsData.Select(t => t.Name), 
                StringComparer.OrdinalIgnoreCase);

            // Filter to only system tools (exclude extended tools)
            var systemTools = allTools
                .Where(tool => !extendedToolNames.Contains(tool.Name))
                .ToList();

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLowerInvariant();
                systemTools = systemTools
                    .Where(tool => 
                        tool.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                        (tool.Description?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true) ||
                        (tool.PluginName?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList();
            }

            _logger.LogInternalInformation("ListSystemTools: Retrieved {Count} system tools (filtered {ExtendedCount} extended tools)", 
                systemTools.Count, extendedToolNames.Count);


            return Ok(systemTools);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in ListSystemTools");
            return StatusCode(500, new ExtendedAgentErrorResponse(
                Status: "error",
                ErrorCode: "SYSTEM_TOOLS_ERROR",
                Message: "Failed to list system tools",
                Timestamp: DateTime.UtcNow,
                Details: null
            ));
        }
    }

    /// <summary>
    /// List all tools
    /// Note: in the future organize to just use one endpoints that get toos. filter query parameter
    /// </summary>
    /// <param name="search">Search tools by name or description</param>
    /// <returns>List of system tools</returns>
    [HttpGet("alltools")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(List<Agent.Framework.ToolInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Agent.Framework.ToolInfo>>> ListAllTools([FromQuery] string? search = null)
    {
        try
        {
            _logger.LogInternalInformation("ListSystemTools: Invoked with search: {Search}", search);

            // Get all tools (system + extended)
            var allTools = await Task.Run(() => _toolFactory.FetchAvailableToolInfo());

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLowerInvariant();
                allTools = allTools
                    .Where(tool =>
                        tool.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                        (tool.Description?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true) ||
                        (tool.PluginName?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList();
            }
            return Ok(allTools);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in ListAllTools");
            return StatusCode(500, new ExtendedAgentErrorResponse(
                Status: "error",
                ErrorCode: "SYSTEM_TOOLS_ERROR",
                Message: "Failed to list system tools",
                Timestamp: DateTime.UtcNow,
                Details: null
            ));
        }
    }
}
