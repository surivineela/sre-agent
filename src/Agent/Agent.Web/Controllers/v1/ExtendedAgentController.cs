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
using Agent.Plugins.Connector;
using Agent.Plugins.Kusto;
using Agent.Runtime.Helpers;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models.ExtendedAgents;
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
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IToolFactory<AgentContext> _toolFactory;

    public ExtendedAgentController(
        IExtendedAgentService extendedAgentService,
        ILogger<ExtendedAgentController> logger,
        IResourceDeploymentService agentService,
        IConnectorResolver connectorResolver,
        IAuthenticationService authenticationService,
        IChatClientProvider chatClientProvider,
        IToolFactory<AgentContext> toolFactory
       )
    {
        _resourceDeploymentService = agentService;
        _logger = logger;
        _extendedAgentService = extendedAgentService;
        _connectorResolver = connectorResolver;
        _authenticationService = authenticationService;
        _chatClientProvider = chatClientProvider;
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
            _logger.LogInternalInformation("ApplyAgentConfiguration: Received apply request");
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
                _logger.LogInternalWarning("ApplyAgentConfiguration: Missing kind on resource");
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

            var resourceKind = generic.Kind ?? "unknown";
            _logger.LogInternalInformation("ApplyAgentConfiguration: Processing resource kind {Kind}", resourceKind);

            var result = new ExtendedAgentApply();

            // Handle different resource types
            switch (generic.Kind)
            {
                case "AgentConfiguration":
                    // Validate YAML structure before parsing
                    var structureValidationErrors = _extendedAgentService.ValidateYamlStructure(yamlDict);
                    if (structureValidationErrors.Count > 0)
                    {
                        _logger.LogInternalWarning("ApplyAgentConfiguration: YAML structure validation failed for agent");
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

                    var agentName = agentDeployment.Spec?.Name ?? "unknown";
                    _logger.LogInternalInformation("ApplyAgentConfiguration: Applying agent {AgentName}", agentName);
                    await _resourceDeploymentService.ApplyAsync(agentDeployment);
                    result = new ExtendedAgentApply
                    {
                        Status = ExtendedAgentApplyStatus.Accepted,
                        Message = "Agent and tools deployment initiated",
                        OperationId = "",
                        Timestamp = DateTime.UtcNow,
                        Details = new ExtendedAgentApplyDetails
                        {
                            AgentName = agentDeployment.Spec?.Name ?? string.Empty,
                            ToolsCount = agentDeployment.Spec?.Tools?.Count ?? 0,
                            McpToolsCount = agentDeployment.Spec?.McpTools?.Count ?? 0
                        }
                    };
                    _logger.LogInternalInformation("ApplyAgentConfiguration: Agent {AgentName} apply accepted", agentName);
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
                            var toolCount = tool.Spec?.Tools?.Count ?? 0;
                            _logger.LogInternalInformation("ApplyAgentConfiguration: Applying tool list with {Count} tools", toolCount);
                            await _resourceDeploymentService.ApplyAsync(tool);
                            break;

                        case ConnectorsDeploymentModel connector:
                            var connectorCount = connector.Spec?.Connectors?.Count ?? 0;
                            _logger.LogInternalInformation("ApplyAgentConfiguration: Applying connector list with {Count} connectors", connectorCount);
                            await _resourceDeploymentService.ApplyAsync(connector);
                            break;

                        case PluginConfigDeploymentModel pluginConfig:
                            var pluginName = pluginConfig.Spec?.PluginName ?? "unknown";
                            _logger.LogInternalInformation("ApplyAgentConfiguration: Applying plugin configuration for {PluginName}", pluginName);
                            await _resourceDeploymentService.ApplyAsync(pluginConfig);
                            break;

                        case CommonToolsListDeploymentModel commonToolsList:
                            var commonToolsCount = commonToolsList.Spec?.CommonToolsLists?.Count ?? 0;
                            _logger.LogInternalInformation("ApplyAgentConfiguration: Applying common tools list with {Count} entries", commonToolsCount);
                            await _resourceDeploymentService.ApplyAsync(commonToolsList);
                            break;

                        case CommonPromptDeploymentModel commonPrompt:
                            var promptCount = commonPrompt.Spec?.CommonPrompts?.Count ?? 0;
                            _logger.LogInternalInformation("ApplyAgentConfiguration: Applying common prompt list with {Count} prompts", promptCount);
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
            _logger.LogInternalInformation("ApplyAgentConfiguration: Returning Accepted for kind {Kind}", resourceKind);

            return Accepted(webResponse);
        }
        catch (ValidationException ex)
        {
            _logger.LogInternalError(ex, "ApplyAgentConfiguration: Validation exception encountered");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ApplyAgentConfiguration: Unexpected exception encountered");
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
    [ProducesResponseType(typeof(PaginatedResponse<YamlAgentDescriptor>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaginatedResponse<YamlAgentDescriptor>>> ListAgents(
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

Important: Keep simple prompts simple. Do not bloat straightforward prompts, if already good return same prompt back. **You must** add a statement and <seld-reflect> note in the generated prompt about ending the turn when asking user a question, and not repeating the same question.

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
            var chatResponse = await _chatClientProvider.DefaultModel.GetResponseAsync(prompt, chatOptions, HttpContext.RequestAborted);
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
    /// <param name="stableOnly">If true, exclude incident handler tools (platform-specific tools)</param>
    /// <returns>List of system tools</returns>
    [HttpGet("systemtools")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(List<Agent.Framework.ToolInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Agent.Framework.ToolInfo>>> ListSystemTools([FromQuery] string? search = null, [FromQuery] bool stableOnly = false)
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

            // Filter to stable/published tools if stableOnly is true
            if (!FirstPartyHelper.IsFirstPartyTenant() && stableOnly)
            {
                // Load published tools from PublishedTools.json
                var publishedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var publishedToolsPath = Path.Combine(AppContext.BaseDirectory, "PublishedTools.json");
                    if (System.IO.File.Exists(publishedToolsPath))
                    {
                        var publishedToolsJson = await System.IO.File.ReadAllTextAsync(publishedToolsPath);
                        var publishedToolsDoc = JsonDocument.Parse(publishedToolsJson);

                        if (publishedToolsDoc.RootElement.TryGetProperty("tools", out var toolsArray))
                        {
                            foreach (var toolElement in toolsArray.EnumerateArray())
                            {
                                if (toolElement.TryGetProperty("name", out var nameProperty))
                                {
                                    var toolName = nameProperty.GetString();
                                    if (!string.IsNullOrEmpty(toolName))
                                    {
                                        publishedToolNames.Add(toolName);
                                    }
                                }
                            }
                        }

                        _logger.LogInternalInformation("Loaded {Count} published tools from PublishedTools.json", publishedToolNames.Count);
                    }
                    else
                    {
                        _logger.LogInternalWarning("PublishedTools.json not found at {Path}, falling back to IsIncidentHandlerTool filter", publishedToolsPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Failed to load PublishedTools.json, falling back to IsIncidentHandlerTool filter");
                }

                // Filter: if we have published tools list, use it; otherwise fall back to IsIncidentHandlerTool flag
                if (publishedToolNames.Count > 0)
                {
                    systemTools = systemTools
                        .Where(tool => publishedToolNames.Contains(tool.Name))
                        .ToList();
                }
                else
                {
                    // Fallback: exclude incident handler tools
                    systemTools = systemTools
                        .Where(tool => !tool.IsIncidentHandlerTool)
                        .ToList();
                }
            }

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
    /// Generates targeted insights for the playground experience including confidence scoring and recommended actions.
    /// </summary>
    [HttpPost("playground-insights")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentWriteActionId)]
    [ProducesResponseType(typeof(PlaygroundInsightsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PlaygroundInsightsResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PlaygroundInsightsResponse>> GetPlaygroundInsights([FromBody] PlaygroundInsightsRequest request)
    {
        try
        {
            var systemPrompt = """
You are an expert AI agent evaluator implementing a rigorous assessment framework with ACTIONABLE improvements via generated diffs.

# EVALUATION PHILOSOPHY
Effective agents balance autonomy with reliability. Your evaluation should provide both comprehensive assessment AND immediate, applicable fixes through code diffs.

# ROOT-CAUSE TRIAGE (FIRST)
Before proposing fixes, classify the primary gap using evidence from the chat, prompt, and available tools:
• TOOL GAP: User intent required a capability the agent lacked → prioritize `toolAdd` with generic tools.
• PROMPT GAP: Tools existed but the prompt didn’t instruct correct usage → prioritize `promptPatch` (or `promptRewrite` when severe).
• SAFETY/POLICY GAP: Destructive paths lacked gates → add safety fixes.
• ORCHESTRATION GAP: Task shape needs routing/chaining/guard loops → add minimal orchestration.
Always state the root cause in each actionItem's "detail" and connect it to user intent.

# PROMPT IMPROVEMENT STRATEGY
When evaluating prompts, consider the scope and depth of changes needed:

**CONTEXT-DRIVEN RECOMMENDATIONS (CRITICAL):**
Always frame recommendations by connecting user intent to agent capabilities:
1. **Identify what the user tried to do** — Extract from chat messages, transcript summary, and recent conversation.
2. **Diagnose the mismatch** — What's missing in the prompt/tools that prevented success?
3. **Explain conversationally** — "You chatted about [X] but your agent only has [Y], so we need to fix [Z]."
Avoid trivial or generic remarks (e.g., “say hi to people”); keep suggestions domain-specific and actionable.

**Example Framing:**
- ❌ BAD: "Add success criteria to goal"
- ✅ GOOD: "Your agent can't diagnose Container Apps because it only has greeting tools and no Azure CLI access"
- ✅ GOOD: "You asked about pod failures but the prompt doesn't mention Kubernetes—it's focused on general greetings"

**PREFER COMPLETE REWRITES when:**
- Overall confidence score < 50 (Major Gaps or Requires Redesign)
- Multiple critical dimensions score below 50
- Fundamental structural issues exist (missing role, unclear goals, no success criteria)
- Intent Match ≤ 2 (significant misalignment with purpose)
- The chat conversation shows systemic failures or confusion patterns

**Complete Rewrite Guidelines:**
- If you believe prompt is poorly structured or misaligned, opt for a full rewrite.
- Provide a full replacement prompt using the agent design principles below.
- Include clear role definition, goals, success criteria, and tool policies.
- Structure the rewrite with sections: Role, Goals, Success Criteria, Tool Usage, Process Flow.
- Mark as "type": "promptRewrite" with "severity": "error".
- Set impact level to "high" with scoreIncrease ≥ 30.
- In the "detail" field, explain: "You tried to [user intent] but the agent is set up for [current capability]. This rewrite refocuses it on [domain] with the right tools and instructions."

**Use Incremental Patches when:**
- Confidence score ≥ 50 (Needs Refinement or better)
- Issues are isolated to specific sections (missing stop conditions, unclear tool policy)
- The core structure is sound but needs enhancement
- Only 1–2 dimensions need significant improvement

**Example Complete Rewrite Format:**
```
@@ -1,N +1,M @@
-<entire old prompt>
+# ROLE
+You are a [specific role] specialized in [domain].
+
+# PRIMARY GOALS
+1. [Goal with measurable success criteria]
+2. [Goal with clear completion conditions]
+
+# SUCCESS CRITERIA
+- [Observable outcome]
+- [Time/quality SLA]
+
+# TOOL USAGE POLICY
+- Prefer read-only tools before writes
+- Use generic tools (`RunAzCli*`) over resource-specific
+- Require confirmation for destructive actions
+
+# PROCESS
+1. [Step-by-step workflow]
+2. [Decision points and stop conditions]
```

# AGENTIC DESIGN PRINCIPLES (PLAYGROUND)
Prefer simple, composable patterns over heavyweight frameworks. Start with an augmented LLM (tools, retrieval, memory) and escalate complexity only if metrics show wins. Choose the workflow that fits the task shape:
• Prompt chaining for crisp decomposition and gates
• Routing for clean separation of concerns
• Parallelization (sectioning/voting) for speed or confidence
• Orchestrator→workers for unpredictable subtask graphs
• Evaluator→optimizer when iterative refinement helps
Design a great ACI (agent–computer interface): crystal-clear tool specs with examples, boundaries, and poka-yoke inputs. Expose planning steps via evidence and “notes,” not in the JSON payload fields.

# PLAYGROUND DATA SOURCES (EVIDENCE)
Ground judgments in the provided artifacts: Primary goal, Prompt, Available tools, Chat issues observed, Tool tester feedback, Transcript summary, Recent chat snippets. Never infer tools not in “Available tools.”
When proposing fixes, explicitly assess: **Would adding a tool have solved the user’s ask? Would a prompt change have solved it?** Choose the smallest fix that closes the gap.

# CRITICAL FORMATTING RULES
1) When suggesting tools, ALWAYS wrap tool names in backticks: `ToolName`
2) Return ONLY **minified JSON** (no Markdown, no commentary).
3) **Prompt changes** must be unified diffs placed in `actionItems[i].patch` as a single string (no code fences; use `\n` inside the string). **Tool additions** use `type":"toolAdd"` with rationale; do **not** embed diffs for toolAdd.
4) Only suggest tools from the "Available tools" list provided.
5) `confidenceLabel` MUST be one of: "Production Ready","Pilot Ready","Needs Refinement","Major Gaps","Requires Redesign".
6) Never include thinking/plans outside the schema; use "notes" for any brief rationale.
7) Diffs must be programmatically applicable and minimal; preserve placeholders/variables.

# GENERIC-OVER-SPECIFIC TOOL POLICY (MANDATORY)
Always prefer generic tools over resource-specific tools when both can achieve the goal (e.g., prefer `RunAzCliReadCommands`/`RunAzCliWriteCommands`/`GetAzCliHelp` or other generic query tools over narrowly scoped resource tools). This improves reuse, reliability, and debuggability.
• Ordering: suggest generic tools first; propose a resource-specific tool only when the generic option demonstrably cannot satisfy the requirement.
• If recommending a specific tool, you MUST include: (a) a brief fallback policy using the generic tool, (b) why the specific tool is necessary (capability gap/latency), and (c) a safety note.
• Include a user-facing explanation in "promptInsights" starting with **"Tooling rationale:"** that explains—in plain language—why generic tools are preferred in this case.

# AZURE RESOURCE OPERATIONS POLICY (MANDATORY)
For ANY Azure resource operations, diagnostics, or management tasks:
• **Proactively recommend Azure CLI tools** when relevant: `RunAzCliReadCommands` (read/diagnostics), `RunAzCliWriteCommands` (writes), `GetAzCliHelp` (command discovery).
• **MANDATORY toolAdd** when BOTH (i) Azure CLI tools are in "Available tools" but NOT in the agent's configured tools, AND (ii) goals/prompt/chat reference Azure resources (VirtualMachine, WebApp, ContainerApp, KeyVault, StorageAccount, SQL, CosmosDB, etc.). Prioritize adding `RunAzCliReadCommands` first.
• **REPLACE resource-specific tools** with Azure CLI equivalents when CLI can satisfy the requirement.
• **Tool hierarchy (STRICT ORDER):**
  1. `RunAzCliReadCommands` for ALL read/query/diagnostic operations.
  2. `RunAzCliWriteCommands` only for required modifications.
  3. Resource-specific tools **only** if Azure CLI cannot achieve the goal.
  4. Include `GetAzCliHelp` when the agent works with Azure and discovery is needed.
• **Prompt policy patch** when Azure work is present: instruct to prefer `RunAzCliReadCommands` for read queries, `RunAzCliWriteCommands` for writes, pass `--subscription` for scope, and fall back to specific tools only if CLI cannot fulfill the need.

**Example toolAdd actionItem (prioritize RunAzCliReadCommands):**
```json
{
  "id": "add-azcli-read-001",
  "type": "toolAdd",
  "title": "Add `RunAzCliReadCommands` for Container Apps diagnostics",
  "detail": "Your agent targets Container Apps but lacks `RunAzCliReadCommands`. Adding it enables: status checks (az containerapp show), logs (az containerapp logs), revision list, and config inspection—standard Azure diagnostics.",
  "severity": "error",
  "impact": {
    "scoreIncrease": 25,
    "dimension": "toolFit",
    "description": "Enables Azure read operations via standard CLI interface",
    "level": "high"
  },
  "patch": "Add tool: `RunAzCliReadCommands`",
  "autoApplicable": true
}
```

# TOOL FILTERING POLICY (CRITICAL)
Do NOT suggest workflow/notification tools with no remediation value:
• NEVER suggest: `NotifyUser`, `AskUserForInput`, `SendNotification`, `SendEmail`, `CreateIncident`, `UpdateIncident`, or similar.
• ALWAYS prefer tools that remediate, diagnose, or query systems (`RunAzCliReadCommands`, `RunAzCliWriteCommands`, `RunKustoQuery`, etc.).
• If only workflow tools are missing, recommend prompt improvements that reduce reliance on human interaction.

# EVALUATION DIMENSIONS WITH SCORING

## 1. COMPLETENESS (0–100)
- Role definition and operational context
- Explicit goals with success criteria
- Comprehensive instruction coverage
- Edge case handling

Scoring:
- 90–100: Production-ready with all elements
- 70–89: Solid foundation, minor gaps
- 50–69: Functional but missing key elements
- 30–49: Major structural gaps
- 0–29: Fundamentally incomplete

## 2. INTENT MATCH (1–5) — BE STRICT
How well execution aligns with stated goals.

CRITICAL: Most agents score 2–3. Be harsh and realistic.
- 5: Perfect alignment; zero waste; expert focus
- 4: Strong alignment; ≤2 minor gaps
- 3: Basic alignment; notable gaps or diffuse scope
- 2: Significant misalignment; vague connection to goal
- 1: Fundamental disconnect

Evidence required:
- Goal↔instruction alignment analysis
- Chat behavior patterns: on-task vs off-task
- Boundary clarity; success criteria explicitness
- **HALLUCINATION DETECTION**: Compare chat/tool calls vs "Available tools" — list any hallucinated tools.
- **NO ACTION DETECTION**: If goal requires action and no tools were used, cap at 2 and flag it.

Scoring aids:
- Compute `intent_alignment_ratio = on_goal_steps / max(1,total_steps)` with one concrete on-task and one off-task example.
- Penalties:
  - 1–2 hallucinated tools → cap at 2, create HIGH-priority fix
  - 3+ hallucinated tools or systemic → cap at 1, require promptRewrite
  - Missing success criteria → cap at 3 until patched
  - No tool actions when required → cap at 2

## 3. TOOL FIT (0–100)
- Necessary vs available tools; redundancy/conflicts
- Missing critical capabilities; orchestration patterns
- Hallucination impact penalties:
  - Resource-specific suggested when generic suffices → −10 to −20
  - Hallucinated tools → −20 (1–2) or −40 (3+)

## 4. PROMPT CLARITY (0–100)
- Specific, actionable, logically ordered, LLM-friendly

## 5. SAFETY (0–100)
- Confirmation gates for destructive actions
- Error handling, fallbacks, rate limits, guardrails

## 6. ACTIONABILITY (0–100)
- Clear triggers, decision criteria, response format, tool invocation patterns

## METRICS TO CONSIDER (QUALITATIVE IF NEEDED)
- Steps to first correct tool call; tool success/retry ratio
- Wasted tool calls; latency to actionable output
- Tokens/turn (rough)
Put any metric notes in "notes" only.

# SCORING METHODOLOGY
Overall confidence (0–100) using weighted dimensions:
- Completeness: 20%
- Intent Match (scaled 1–5 → 0–100 by ×20): 25%
- Tool Fit: 20%
- Prompt Clarity: 15%
- Safety: 15%
- Actionability: 5%

Confidence Labels:
- 90–100: Production Ready
- 75–89: Pilot Ready
- 50–74: Needs Refinement
- 25–49: Major Gaps
- 0–24: Requires Redesign

# PLAYGROUND-AWARE PRIORITIZATION
For the TOP 5 actionItems:
• Favor fixes that increase `intent_alignment_ratio`, add success criteria/stop conditions, and correct tool policy.
• Prefer **minimal** diffs preserving variable names/placeholders.
• Mark conflicts/dependencies (e.g., routing before optimizer loop).
• Do NOT suggest tools outside "Available tools". If a critical tool is missing, propose the closest **generic** alternative from the list or a prompt patch to work without it.
• Prefer generic tools; if a specific tool is chosen, add a "Tooling rationale:" entry in "promptInsights".

# SAFETY BY DEFAULT
Default to read-only/dry-run for destructive capability; require explicit confirmation gates; add fallback/rollback and rate-limits. If absent, include a HIGH-impact safety patch.

# ACTIONABLE IMPROVEMENTS
IMPORTANT: Return 0–5 actionable fixes based on actual needs:
- **If the agent is well-configured** (confidenceScore ≥ 75, no severity="error" issues, no hallucinations): Return an EMPTY actionItems array. Stop nitpicking after 2-3 rounds of fixes.
- **If improvements exist**: Only suggest HIGH-impact fixes (scoreIncrease ≥ 10). Limit to TOP 5, ordered by impact (descending).
- **Quality over quantity**: Don't suggest marginal improvements (scoreIncrease < 5) or stylistic preferences. Better to return 0 suggestions than to fabricate issues.

For EACH issue, provide structured fixes with diffs and impact levels:

## Impact Classification
- HIGH: Core functionality/safety (≥15 points)
- MEDIUM: Clarity/completeness (8–14 points)
- LOW: Minor enhancements (1–7 points)

## Prompt Patch Format
Generate unified diff-style changes with adequate context (5–7 lines) for clarity. Example:
```
@@ -8,7 +8,11 @@
 # ROLE
 You are an extended SRE Operations Agent.

 Role definition: You are an extended SRE
-Operations Agent answering or diagnosing Azure-
-related issues for engineering teams.
+Operations Agent specialized in diagnosing Azure
+Container Apps issues and providing resolution steps.
+Success: User receives actionable fix within 5 minutes.

 # PRIMARY GOALS
 1. Diagnose Azure service health issues
```

## Tool Addition Format
Specify exact tool and rationale (no diff required for toolAdd). Example:
```
Add `RunAzCliReadCommands` for Container Apps diagnostics
Required for: resource investigation, health checks
Impact: HIGH (+12 points to toolFit)
```

## STANDARD HIGH-IMPACT FIX TEMPLATES
1) Add success criteria & stop conditions
```
@@ -3,6 +3,10 @@
 You are an Azure SRE Operations Assistant.

 # PRIMARY GOALS
 Goal: <keep>
+Success criteria: <observable outcome, SLA, artifact>
+Stop conditions: maxIterations=5, maxCost=$X,
+checkpoint=ask-human on blocker

 # TOOLS
```
2) Clarify tool policy & invocation patterns
```
@@ -8,7 +8,11 @@
 Success criteria: User receives actionable fix within 5 minutes.

 # TOOLS
 Tools:
+Use `RunAzCliReadCommands` for state inspection before any write.
+Require dry-run/confirmation for destructive tools.
+Prefer generic tools (`RunAzCli*`) before resource-specific tools.

 # PROCESS
```
3) Add routing or chaining when task shape warrants
```
@@ -3,2 +3,5 @@
 If input intent ∈ {refund, tech, general} then route → specialized prompt;
+Else keep single-call baseline with retrieval and examples.
```
4) Add evaluator→optimizer guard
```
@@ -2,2 +2,5 @@
 Draft → Evaluate against criteria → Patch or Approve;
+limit to 2 loops.
```

# RESPONSE SCHEMA
Return ONLY this **minified JSON**:
{
  "confidenceScore": number,
  "confidenceLabel": "Production Ready|Pilot Ready|Needs Refinement|Major Gaps|Requires Redesign",
  "subScores": [
    {"id":"completeness","label":"COMPLETENESS","score":number,"evidence":"...","improvements":["fix-ids"]},
    {"id":"intentMatch","label":"INTENT MATCH","score":number,"evidence":"...","improvements":["fix-ids"]},
    {"id":"toolFit","label":"TOOL FIT","score":number,"evidence":"...","improvements":["fix-ids"]},
    {"id":"promptClarity","label":"PROMPT CLARITY","score":number,"evidence":"...","improvements":["fix-ids"]},
    {"id":"safety","label":"SAFETY","score":number,"evidence":"...","improvements":["fix-ids"]},
    {"id":"actionability","label":"ACTIONABILITY","score":number,"evidence":"...","improvements":["fix-ids"]}
  ],
  "promptInsights": ["Specific, actionable insight about the prompt"],
  "toolSuggestions": ["Add `ToolName` for specific capability"],
  "chatDiagnostics": ["Specific failure pattern with evidence. MUST include hallucination detection: Compare all tool references in chat against 'Available tools' and report 'Hallucinated tools: [`Tool1`,`Tool2`]' or 'No hallucinations detected'"],
  "actionItems": [
    {
      "id":"fix-001",
      "type":"promptPatch|promptRewrite|toolAdd|safety|clarity",
      "title":"User-intent–anchored summary (e.g., 'Your agent can't diagnose Container Apps because it only has greeting tools')",
      "detail":"1) What the user was trying to do, 2) Why current setup fails, 3) How this fix closes the gap.",
      "severity":"error|warning|info",
      "impact":{"scoreIncrease":number,"dimension":"completeness|intent|toolFit|clarity|safety|actionability","description":"...","level":"high|medium|low"},
      "patch":"Unified diff string for prompt changes or 'Add tool: `Tool`' for toolAdd",
      "autoApplicable":boolean,
      "conflicts":["other-fix-ids"],
      "requires":["prerequisite-fix-ids"]
    }
  ],
  "notes": ["Important caveats or context"],
  "suggestedSequence": ["fix-001","fix-003","fix-002"]
}

CRITICAL: Return 0–5 highest-impact actionItems, ordered by `impact.scoreIncrease` (descending). An empty array is acceptable for well-configured agents.

# DIFF GENERATION RULES
1) **Balanced Detail**: Provide enough context to understand the change clearly without being overly verbose. Include complete logical sections when appropriate.
2) **Context Preservation**: Include 5–7 lines of context around changes to provide better understanding of the modification.
3) **Applicability**: Patch must apply cleanly; escape newlines (`\n`) in JSON string; no code fences.
4) **Conflict/Dependencies**: Mark mutually exclusive fixes and prerequisites.
5) **Rewrite Guard**: Only replace the entire prompt when rewrite criteria are met; otherwise patch narrowly.
6) **Comprehensive Patches**: When making changes, include related context that helps understand the full scope of the modification. Don't be afraid to include a few extra lines if it makes the change clearer.

# EXAMPLES
## Good Prompt Diff (with adequate context)
@@ -3,8 +3,12 @@
 You are an Azure SRE Operations Assistant.

 # PRIMARY GOALS
 Goal: Start each conversation with 'Hola' to
-establish a friendly, consistent greeting for SRE
-interactions with engineers.
+establish a friendly greeting for SRE operations.
+Success criteria: User acknowledges positively and
+provides issue details within first 2 exchanges.

 # TOOLS
 Use the following tools to assist with diagnostics:

## Good Tool Suggestion
Add `RunAzCliReadCommands` to enable Azure CLI queries
Required for: resource state inspection, configuration validation
Impact: +15 points to toolFit, enables 80% of common diagnostics

You MUST suggest tools ONLY from the "Available tools" list provided.
Downrank suggestions that add complexity without measured benefit. Prefer a simple single-call + retrieval baseline before multi-agent orchestration.

## Hallucination Fix Example (HIGH Priority)
**chatDiagnostics entry:**
"Agent attempted to use `GetContainerAppLogs` and `RestartContainerApp` which don't exist in Available tools. Hallucinated tools: [`GetContainerAppLogs`, `RestartContainerApp`]."

**actionItem example:**
{
  "id":"fix-hallucination-001",
  "type":"promptPatch",
  "title":"Your agent tried to use Container App tools that don't exist—add Azure CLI tools instead",
  "detail":"You asked about Container Apps diagnostics, but the agent attempted to call `GetContainerAppLogs` and `RestartContainerApp` which aren't in the available tool list (hallucination). This fix adds the actual available tools (`RunAzCliReadCommands` for diagnostics) and updates the prompt to use only real tools.",
  "severity":"error",
  "impact":{"scoreIncrease":25,"dimension":"intent","description":"Eliminates hallucinations and enables actual Container Apps diagnostics","level":"high"},
  "patch":"@@ -1,5 +1,10 @@\n You are an Azure support agent.\n+\n+# AVAILABLE TOOLS\n+Use ONLY these tools: `RunAzCliReadCommands`, `RunKustoQuery`\n+NEVER attempt to call tools not in this list.\n+\n # DIAGNOSTICS\n-Use appropriate tools to investigate issues.\n+Use `RunAzCliReadCommands` with 'az containerapp logs show' for Container Apps diagnostics."
}

# EVALUATION CALIBRATION
Real-world benchmarks for scoring:
- GitHub Copilot Workspace agents: 75–85
- Production Azure SRE agents: 75–85
- Typical first iteration: 45–65
- Untested prototypes: 30–50

Your evaluation should reflect realistic expectations, not theoretical perfection. Prefer the smallest effective recommendation and request evidence-gathering via "notes" (not extra fields). All outputs MUST validate against the schema; if any field is unknown, omit it—do not invent placeholders. Always return **minified JSON** with exactly the fields in the schema.

# SELF-REFLECTION ON DIFF QUALITY (CRITICAL)
Before returning your response, VALIDATE each diff in actionItems:

**Diff Format Requirements:**
1. **Additions ONLY (pure additions)**: If adding new text without removing anything, format as:
   ```
   @@ -X,Y +X,Y+N @@
    <context line>
    <context line>
   +<new line being added>
   +<new line being added>
    <context line>
   ```
   ✅ Lines starting with `+` are NEW content being added
   ❌ Do NOT include `-` lines if you're only adding text

2. **Removals ONLY**: If removing text without adding:
   ```
   @@ -X,Y +X,Y-N @@
    <context line>
   -<line being removed>
   -<line being removed>
    <context line>
   ```

3. **Modifications (remove + add)**: If changing existing text:
   ```
   @@ -X,Y +X,Y @@
    <context line>
   -<old version of line>
   +<new version of line>
    <context line>
   ```

4. **Tool additions**: NEVER put tool additions in patch format. Use `type: "toolAdd"` with `patch: "Add tool: \`ToolName\`"`

**Validation Checklist (review EVERY diff):**
- [ ] Does the diff have both `-` and `+` lines when you're only adding content? → FIX IT (remove `-` lines)
- [ ] Are you including tools in the diff instead of using toolAdd type? → FIX IT (separate tools from prompt patches)
- [ ] Are line numbers in @@ header realistic? → Verify they match the actual prompt structure
- [ ] Is context sufficient (5-7 lines)? → Add more context if needed
- [ ] Can this diff be applied programmatically? → Test mental application

**Self-Correction:**
If ANY diff fails validation, REWRITE it before returning the response. Do not send malformed diffs.
""";

            var builder = new StringBuilder();
            builder.AppendLine(systemPrompt);
            builder.AppendLine("\n--- Agent Context ---");
            builder.AppendLine($"Agent name: {request.AgentName ?? "(unspecified)"}");
            if (!string.IsNullOrWhiteSpace(request.AgentGoal))
            {
                builder.AppendLine($"Primary goal: {request.AgentGoal}");
            }

            builder.AppendLine("Prompt (<<< >>>):");
            builder.AppendLine("<<<");
            builder.AppendLine(request.Prompt ?? string.Empty);
            builder.AppendLine(">>>");

            if (request.Tools.Count > 0 || request.SystemTools.Count > 0)
            {
                builder.AppendLine("\nLinked tools:");
                if (request.Tools.Count > 0)
                {
                    builder.AppendLine("- Extended tools: " + string.Join(", ", request.Tools));
                }
                if (request.SystemTools.Count > 0)
                {
                    builder.AppendLine("- System tools: " + string.Join(", ", request.SystemTools));
                }
            }

            // Add available tools context for accurate recommendations
            if (request.AvailableTools?.Count > 0 || request.AvailableSystemTools?.Count > 0)
            {
                builder.AppendLine("\nAvailable tools (use ONLY these in suggestions):");
                if (request.AvailableTools?.Count > 0)
                {
                    builder.AppendLine("- Available extended tools: " + string.Join(", ", request.AvailableTools));
                }
                if (request.AvailableSystemTools?.Count > 0)
                {
                    builder.AppendLine("- Available system tools: " + string.Join(", ", request.AvailableSystemTools));
                }
            }

            if (request.ChatFindings.Count > 0)
            {
                builder.AppendLine("\nChat issues observed:");
                foreach (var finding in request.ChatFindings)
                {
                    builder.AppendLine($"- [{finding.Severity}] {finding.Title}: {finding.Detail}");
                }
            }

            if (request.ToolFindings.Count > 0)
            {
                builder.AppendLine("\nTool tester feedback:");
                foreach (var finding in request.ToolFindings)
                {
                    builder.AppendLine($"- [{finding.Severity}] {finding.Title}: {finding.Detail}");
                }
            }

            if (!string.IsNullOrWhiteSpace(request.TranscriptSummary))
            {
                builder.AppendLine("\nTranscript summary:");
                builder.AppendLine(request.TranscriptSummary);
            }

            if (request.RecentMessages.Count > 0)
            {
                builder.AppendLine("\nRecent chat snippets:");
                foreach (var message in request.RecentMessages)
                {
                    builder.AppendLine("- " + message);
                }
            }

            var chatOptions = new ChatOptions { Temperature = 0.2f };
            var prompt = builder.ToString();
            var chatResponse = await _chatClientProvider.DefaultModel.GetResponseAsync(prompt, chatOptions, HttpContext.RequestAborted);
            var content = chatResponse?.GetMessage()?.Text ?? string.Empty;

            _logger.LogInternalInformation("Playground insights raw content: {Content}", content);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var insights = JsonSerializer.Deserialize<PlaygroundInsightsResponse>(content, jsonOptions);
            if (insights == null)
            {
                _logger.LogInternalWarning("Playground insights deserialization returned null.");
                return StatusCode(StatusCodes.Status500InternalServerError, BuildFallbackInsights("Failed to parse response"));
            }

            insights.ConfidenceScore = double.IsFinite(insights.ConfidenceScore)
                ? Math.Clamp(insights.ConfidenceScore, 0, 100)
                : 0;

            if (string.IsNullOrWhiteSpace(insights.ConfidenceLabel))
            {
                // Align fallback labels with the strict allowed set in the system prompt
                insights.ConfidenceLabel = insights.ConfidenceScore switch
                {
                    >= 90 => "Production Ready",
                    >= 75 => "Pilot Ready",
                    >= 50 => "Needs Refinement",
                    >= 25 => "Major Gaps",
                    _ => "Requires Redesign"
                };
            }

            return Ok(insights);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error generating playground insights");
            return StatusCode(StatusCodes.Status500InternalServerError, BuildFallbackInsights(ex.Message));
        }
    }

    private static PlaygroundInsightsResponse BuildFallbackInsights(string message)
    {
        return new PlaygroundInsightsResponse
        {
            ConfidenceScore = 35,
            ConfidenceLabel = "Needs attention",
            ChatDiagnostics = new List<string> { "Insight service is temporarily unavailable." },
            Notes = new List<string> { message }
        };
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

    /// <summary>
    /// Execute a system tool with provided parameters
    /// </summary>
    /// <param name="request">The system tool execution request</param>
    /// <returns>The result of the tool execution</returns>
    [HttpPost("systemTool/execute")]
    [AuthorizeArmOperation(ArmOperations.AgentExtendedAgentReadActionId)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ExtendedAgentErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> ExecuteSystemTool([FromBody] SystemToolExecutionRequest request)
    {
        try
        {
            _logger.LogInternalInformation("ExecuteSystemTool: Executing tool {ToolName} from plugin {PluginName}",
                request.ToolName, request.PluginName);

            // Validate request
            if (string.IsNullOrWhiteSpace(request.ToolName))
            {
                return BadRequest(new ExtendedAgentErrorResponse(
                    Status: "error",
                    ErrorCode: "INVALID_REQUEST",
                    Message: "ToolName is required",
                    Timestamp: DateTime.UtcNow,
                    Details: null
                ));
            }

            if (string.IsNullOrWhiteSpace(request.PluginName))
            {
                return BadRequest(new ExtendedAgentErrorResponse(
                    Status: "error",
                    ErrorCode: "INVALID_REQUEST",
                    Message: "PluginName is required",
                    Timestamp: DateTime.UtcNow,
                    Details: null
                ));
            }

            // Ensure the requested tool exists before attempting execution
            if (!_toolFactory.TryFindTool(request.ToolName, out _))
            {
                _logger.LogInternalWarning("ExecuteSystemTool: Tool {ToolName} not found", request.ToolName);
                return NotFound(new ExtendedAgentErrorResponse(
                    Status: "error",
                    ErrorCode: "TOOL_NOT_FOUND",
                    Message: $"System tool '{request.ToolName}' not found in plugin '{request.PluginName}'",
                    Timestamp: DateTime.UtcNow,
                    Details: null
                ));
            }

            // Convert and normalize the parameters dictionary
            var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (request.Parameters != null)
            {
                foreach (var param in request.Parameters)
                {
                    arguments[param.Key] = param.Value;
                }
            }

            // Compute a thread identifier so context-aware tools can execute correctly
            Guid threadId;
            if (arguments.TryGetValue("ThreadId", out var providedThreadId) && providedThreadId != null)
            {
                if (providedThreadId is Guid guidValue)
                {
                    threadId = guidValue;
                }
                else if (!Guid.TryParse(Convert.ToString(providedThreadId, System.Globalization.CultureInfo.InvariantCulture), out threadId))
                {
                    threadId = Guid.NewGuid();
                    arguments["ThreadId"] = threadId.ToString();
                }
            }
            else
            {
                threadId = Guid.NewGuid();
                arguments["ThreadId"] = threadId.ToString();
                _logger.LogInternalInformation(
                    "ExecuteSystemTool: Injected ThreadId {ThreadId} for tool {ToolName} in plugin {PluginName}",
                    threadId,
                    request.ToolName,
                    request.PluginName);
            }

            // Obtain the tool with thread-specific wiring
            var toolFunction = _toolFactory.GetTool(request.ToolName, threadId);

            if (toolFunction is ContextAIFunction<AgentContext> contextTool)
            {
                // Provide a minimal AgentContext so plugins depending on ThreadId have it populated
                var playgroundContext = new AgentContext(
                    Id: Guid.NewGuid(),
                    ThreadId: threadId,
                    AgentType: AgentTypeEnum.Meta,
                    ContextState: ContextStateEnum.Processing,
                    WaitInformation: null,
                    ApprovalInformation: null,
                    AssignedInstanceId: null,
                    AssignmentExpires: null,
                    InputDataSerialized: null,
                    HandoffToAgentContextId: null,
                    HandoffFromAgentContextId: null,
                    CurrentAgent: null,
                    AgentHandoffChain: null,
                    AllowedTools: new List<string> { request.ToolName },
                    AgentMode: null);

                contextTool.SetContext(playgroundContext);
            }

            // Execute the tool
            var functionResult = await toolFunction.InvokeAsync(new AIFunctionArguments(arguments));

            // Return the result
            var result = new
            {
                Success = true,
                Data = functionResult,
                ToolName = request.ToolName,
                PluginName = request.PluginName
            };

            _logger.LogInternalInformation("ExecuteSystemTool: Successfully executed tool {ToolName}", request.ToolName);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInternalWarning(ex, "ExecuteSystemTool: Invalid parameters for tool {ToolName}", request.ToolName);
            return BadRequest(new ExtendedAgentErrorResponse(
                Status: "error",
                ErrorCode: "INVALID_PARAMETERS",
                Message: $"Invalid parameters: {ex.Message}",
                Timestamp: DateTime.UtcNow,
                Details: new ExtendedAgentErrorDetails(
                    Errors: new List<ExtendedAgentErrorField>
                    {
                        new ExtendedAgentErrorField("parameters", ex.Message)
                    }
                )
            ));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ExecuteSystemTool: Error executing tool {ToolName}", request.ToolName);
            return StatusCode(500, new ExtendedAgentErrorResponse(
                Status: "error",
                ErrorCode: "EXECUTION_FAILED",
                Message: $"Tool execution failed: {ex.Message}",
                Timestamp: DateTime.UtcNow,
                Details: new ExtendedAgentErrorDetails(
                    Errors: new List<ExtendedAgentErrorField>
                    {
                        new ExtendedAgentErrorField("execution", ex.Message)
                    }
                )
            ));
        }
    }
}
