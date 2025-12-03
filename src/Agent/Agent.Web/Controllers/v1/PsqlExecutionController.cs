// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Interface;
using Agent.Runtime.Helpers;
using Agent.Runtime.Reasoning;
using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class PsqlExecutionController : ControllerBase
{
    private readonly IThreadRepository _threadRepository;
    private readonly IPostgreSQLAutomationPlugin _postgreSQLAutomationPlugin;
    private readonly PostgresSQLCommandHelper _postgresSQLCommandHelper;
    private readonly ILogger<PsqlExecutionController> _logger;
    private readonly IReasoningLoopManager _reasoningLoopManager;
    private readonly CoreSettings _coreSettings;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

    public PsqlExecutionController(
        IThreadRepository threadRepository,
        IPostgreSQLAutomationPlugin postgreSQLAutomationPlugin,
        PostgresSQLCommandHelper postgresSQLCommandHelper,
        IReasoningLoopManager reasoningLoopManager,
        CoreSettings coreSettings,
        ILogger<PsqlExecutionController> logger,
        IHostEnvironment hostEnvironment,
        IAgentOutboundCommunicationService agentOutboundCommunicationService
    )
    {
        _reasoningLoopManager = reasoningLoopManager;
        _threadRepository = threadRepository;
        _postgreSQLAutomationPlugin = postgreSQLAutomationPlugin;
        _postgresSQLCommandHelper = postgresSQLCommandHelper;
        _logger = logger;
        _coreSettings = coreSettings;
        _hostEnvironment = hostEnvironment;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
    }

    /// <summary>
    /// Execute or cancel a PostgreSQL command
    /// </summary>
    [AuthorizeArmOperation(ArmOperations.AgentThreadApproveActionId)]
    [HttpPost("{threadId}/{executionId}/action")]
    public async Task<IActionResult> ExecuteAction(
        string threadId,
        string executionId,
        [FromBody] ExecutionActionRequest request)
    {
        _logger.LogInternalInformation("Executing action {Action} for thread {ThreadId} execution {ExecutionId}",
            request.Action, threadId, executionId);

        var threadGuid = Guid.Parse(threadId);
        var executionGuid = Guid.Parse(executionId);

        // Get current execution
        var execution = await _threadRepository.GetPsqlExecutionAsync(threadGuid, executionGuid);
        if (execution == null)
        {
            return NotFound(new { error = "Execution not found" });
        }

        // Validate current status
        if (execution.Status != AzCliExecutionStatus.Pending && execution.Status != AzCliExecutionStatus.PendingAuthorization)
        {
            return Conflict(new
            {
                error = $"Cannot {request.Action} execution in {execution.Status} state",
                currentStatus = execution.Status.ToString()
            });
        }

        var authzHeader = Request.Headers["Authorization"].ToString();
        var token = authzHeader.StartsWith("Bearer ") ? authzHeader.Substring("Bearer ".Length).Trim() : null;

        if (string.IsNullOrEmpty(token) && !_hostEnvironment.IsDevelopment())
        {
            return Unauthorized();
        }

        // Get user info from token or use provided user
        var userName = "Unknown User";
        var userId = request.User ?? "sreagent-client";
        string? userEmail = null;

        // Check if user is sreagent-client (frontend default)
        if (userId == "sreagent-client")
        {
            userName = "SRE Agent Client";
        }
        else if (!string.IsNullOrEmpty(authzHeader) && authzHeader.StartsWith("Bearer "))
        {
            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;

                if (jsonToken != null)
                {
                    userEmail = jsonToken.Claims.FirstOrDefault(c => c.Type == "upn")?.Value ?? jsonToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;
                    userId = jsonToken.Claims.FirstOrDefault(c => c.Type == "oid")?.Value ?? userId;
                    var givenName = jsonToken.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value;
                    var familyName = jsonToken.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value;

                    if (!string.IsNullOrEmpty(givenName) && !string.IsNullOrEmpty(familyName))
                    {
                        userName = $"{givenName} {familyName}";
                    }
                    else
                    {
                        userName = jsonToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? userName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to decode JWT token");
            }
        }

        // Get messageId for streaming purposes for given execution
        var psqlMessages = await _threadRepository.GetMessagesWithPsqlAsync(threadGuid);
        var messageId = psqlMessages.FirstOrDefault(m => m.PsqlExecution?.Id == executionGuid)?.Id ?? default;

        switch (request.Action.ToLower())
        {
            case "run":
                var executionDoc = await _threadRepository.GetPsqlExecutionAsync(threadGuid, executionGuid);

                if (executionDoc == null || executionDoc.AgentContextId == null)
                {
                    return NotFound(new { error = "AgentContextId not set in the executionDoc" });
                }

                var agentContext = await _threadRepository.GetAgentContextAsync(agentContextId: executionDoc.AgentContextId.Value, threadId: threadGuid);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        CliExecutionResult result;
                        if (execution.Status == AzCliExecutionStatus.Pending)
                        {
                            execution = execution with
                            {
                                Status = AzCliExecutionStatus.Running,
                                StartedTimestamp = DateTime.UtcNow,
                                ExecutedBy = new Author(
                                    DisplayName: "SRE Agent Client",
                                    UserId: "sreagent-client",
                                    Role: Role.SREAgent
                                )
                            };
                            await _threadRepository.UpdatePsqlExecutionAsync(threadGuid, execution);

                            _logger.LogInternalInformation($"[{threadGuid}]Executing {executionGuid} with agent identity");
                            // this is an approval (not authorization) action, use agent identity
                            // Use direct plugin call instead of tool factory
                            var cliToolResult = await _postgreSQLAutomationPlugin.RunPsqlReadCommandAsync(execution.Command);
                            result = cliToolResult.CliExecutionResult;
                            if (result.ErrorOccurred && (result.ErrorType == CliErrorType.AuthorizationError))
                            {
                                // trigger obo flow
                                var updatedExecution = execution with
                                {
                                    Status = AzCliExecutionStatus.PendingAuthorization,
                                    Description = $"{execution.Description}",
                                    Output = null,
                                    ExecutedBy = null,
                                    Error = null,
                                    StartedTimestamp = null,
                                    CompletedTimestamp = null,
                                };
                                await _threadRepository.UpdatePsqlExecutionAsync(threadGuid, updatedExecution);
                                await _agentOutboundCommunicationService.NotifyPsqlUpdate(threadGuid, updatedExecution, messageId);
                                return;
                            }
                        }
                        else
                        {
                            // this is an authorization action, use obo token
                            execution = execution with
                            {
                                Status = AzCliExecutionStatus.Running,
                                StartedTimestamp = DateTime.UtcNow,
                                ExecutedBy = new Author(
                                    DisplayName: $"{userName} <{userEmail}>",
                                    UserId: userId,
                                    Role: Role.User
                                )
                            };
                            await _threadRepository.UpdatePsqlExecutionAsync(threadGuid, execution);
                            await _agentOutboundCommunicationService.NotifyPsqlUpdate(threadGuid, execution, messageId);

                            FunctionCallContent? functionCall = null;
                            if (!string.IsNullOrEmpty(execution.OriginalFunctionCall))
                            {
                                functionCall = JsonSerializer.Deserialize<FunctionCallContent>(execution.OriginalFunctionCall);
                            }
                            var title = ApprovalHelper.GenerateUniqueApprovalTitle(
                                            threadId,
                                            agentContext?.Id.ToString() ?? string.Empty,
                                            functionCall?.Name ?? string.Empty,
                                            functionCall?.Arguments ?? new Dictionary<string, object?>());
                            var approval = new Approval(
                                                Id: Guid.NewGuid(),
                                                ThreadId: threadId,
                                                Title: title,
                                                Description: $"Execute PostgreSQL command {execution.Command}",
                                                Status: ApprovalDecision.Authorized,
                                                CreatedTimestamp: execution.CreatedTimestamp,
                                                DecisionTimestamp: DateTime.UtcNow,
                                                OrchestrationId: null,
                                                AgentContextId: agentContext?.Id,
                                                DecisionUser: execution.ExecutedBy,
                                                OboToken: token,
                                                OboTokenScope: Constants.DefaultOboTokenScope);

                            await _threadRepository.CreateApprovalAsync(approval);

                            var approvalContext = new ApprovalContext(
                                ThreadId: threadGuid,
                                ApprovalId: approval.Id,
                                UseOboToken: true
                            );
                            Core.ToolStatic.AsyncLocalApprovalContext.Value = approvalContext;

                            _logger.LogInternalInformation($"[{threadGuid}]Executing {executionGuid} with obo token");

                            // Extract resourceId and database from original function call
                            string? resourceId = null;
                            var database = "postgres"; // Default database

                            if (functionCall?.Arguments != null)
                            {
                                try
                                {
                                    if (functionCall.Arguments.TryGetValue("resourceId", out var resourceIdObj))
                                    {
                                        resourceId = resourceIdObj?.ToString();
                                    }

                                    if (functionCall.Arguments.TryGetValue("database", out var databaseObj))
                                    {
                                        database = databaseObj?.ToString() ?? "postgres";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogInternalWarning($"Failed to parse function call arguments: {ex.Message}");
                                }
                            }

                            // Execute PostgreSQL command with extracted parameters
                            if (!string.IsNullOrEmpty(resourceId))
                            {
                                result = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(execution.Command, resourceId, database);
                            }
                            else
                            {
                                _logger.LogInternalWarning("No resourceId found in function call, falling back to 1-parameter method");
                                result = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(execution.Command);
                            }
                        }

                        execution = execution with
                        {
                            Status = result.ErrorOccurred ? AzCliExecutionStatus.Failed : AzCliExecutionStatus.Completed,
                            Output = result.Output,
                            Error = result.ErrorOccurred ? result.Output : null,
                            CompletedTimestamp = DateTime.UtcNow
                        };

                        await _threadRepository.UpdatePsqlExecutionAsync(threadGuid, execution);
                        await _agentOutboundCommunicationService.NotifyPsqlUpdate(threadGuid, execution, messageId);

                        if (agentContext != null)
                        {
                            var functionCall = !string.IsNullOrEmpty(execution.OriginalFunctionCall) ? JsonSerializer.Deserialize<FunctionCallContent>(execution.OriginalFunctionCall) : null;
                            if (functionCall != null)
                            {
                                await _reasoningLoopManager.AppendFunctionCallMessagesAsync(agentContext, new List<ChatMessage>
                                {
                                    new(ChatRole.Assistant,
                                        new List<AIContent>{ functionCall}),
                                    new(ChatRole.Tool,
                                    new List<AIContent>
                                    {
                                        new FunctionResultContent(functionCall.CallId, result.Output ?? "Command completed")
                                    })
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, "Failed to execute command");

                        // Update execution with failure
                        execution = execution with
                        {
                            Status = AzCliExecutionStatus.Failed,
                            Error = ex.Message,
                            CompletedTimestamp = DateTime.UtcNow
                        };

                        if (agentContext != null)
                        {
                            var functionCall = !string.IsNullOrEmpty(execution.OriginalFunctionCall) ? JsonSerializer.Deserialize<FunctionCallContent>(execution.OriginalFunctionCall) : null;
                            if (functionCall != null)
                            {
                                await _reasoningLoopManager.AppendFunctionCallMessagesAsync(agentContext, new List<ChatMessage>
                                {
                                    new(ChatRole.Assistant,
                                        new List<AIContent>{functionCall }),
                                    new(ChatRole.Tool,
                                    new List<AIContent>
                                    {
                                        new FunctionResultContent(functionCall.CallId, $"Execution Failed: {execution.Command}, Result: {ex.Message}")
                                    })
                                });
                            }
                        }
                        await _threadRepository.UpdatePsqlExecutionAsync(threadGuid, execution);
                        await _agentOutboundCommunicationService.NotifyPsqlUpdate(threadGuid, execution, messageId);
                    }
                });

                return Ok(new
                {
                    status = "Running",
                    executedBy = $"{userName} <{userEmail}>",
                    executedById = userId,
                    startedTimestamp = DateTime.UtcNow
                });

            case "cancel":
                // Update status to cancelled
                execution = execution with
                {
                    Status = AzCliExecutionStatus.Cancelled,
                    CompletedTimestamp = DateTime.UtcNow,
                    ExecutedBy = new Author(
                        DisplayName: $"{userName} <{userEmail}>",
                        UserId: userId,
                        Role: Role.User
                    )
                };
                await _threadRepository.UpdatePsqlExecutionAsync(threadGuid, execution);
                await _agentOutboundCommunicationService.NotifyPsqlUpdate(threadGuid, execution, messageId);

                return Ok(new
                {
                    status = "Cancelled",
                    cancelledBy = $"{userName} <{userEmail}>",
                    cancelledById = userId,
                    cancelledTimestamp = DateTime.UtcNow
                });

            default:
                return BadRequest(new { error = $"Invalid action: {request.Action}" });
        }
    }

    /// <summary>
    /// Get execution status
    /// </summary>
    [AuthorizeArmOperation(ArmOperations.AgentThreadReadActionId)]
    [HttpGet("{threadId}/{executionId}/status")]
    public async Task<IActionResult> GetExecutionStatus(string threadId, string executionId)
    {
        _logger.LogInternalInformation("Getting execution status for thread {ThreadId} execution {ExecutionId}",
            threadId, executionId);

        var execution = await _threadRepository.GetPsqlExecutionAsync(
            Guid.Parse(threadId),
            Guid.Parse(executionId)
        );

        if (execution == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            id = execution.Id,
            status = execution.Status.ToString(),
            output = execution.Output,
            error = execution.Error,
            startedTimestamp = execution.StartedTimestamp,
            completedTimestamp = execution.CompletedTimestamp,
            command = execution.Command,
            description = execution.Description,
            executedBy = execution.ExecutedBy
        });
    }

    /// <summary>
    /// Get execution output (non-streaming version)
    /// </summary>
    [AuthorizeArmOperation(ArmOperations.AgentThreadReadActionId)]
    [HttpGet("{threadId}/{executionId}/output")]
    public async Task<IActionResult> GetExecutionOutput(string threadId, string executionId)
    {
        _logger.LogInternalInformation("Getting execution output for thread {ThreadId} execution {ExecutionId}",
            threadId, executionId);

        var execution = await _threadRepository.GetPsqlExecutionAsync(
            Guid.Parse(threadId),
            Guid.Parse(executionId)
        );

        if (execution == null)
        {
            return NotFound();
        }

        // Return current state as JSON
        return Ok(new
        {
            output = execution.Output ?? "",
            status = execution.Status.ToString(),
            error = execution.Error,
            completed = execution.Status != AzCliExecutionStatus.Running &&
                        execution.Status != AzCliExecutionStatus.Pending,
            completedTimestamp = execution.CompletedTimestamp
        });
    }

    /// <summary>
    /// Model for execution action request
    /// </summary>
    public class ExecutionActionRequest
    {
        [Required]
        public required string Action { get; set; } // "run" or "cancel"

        public required string User { get; set; }
    }
}
