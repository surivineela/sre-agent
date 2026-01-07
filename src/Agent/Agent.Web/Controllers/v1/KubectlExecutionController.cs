// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
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
public class KubectlExecutionController : ControllerBase
{
    private readonly IThreadRepository _threadRepository;
    private readonly IKubePlugin _kubePlugin;
    private readonly ILogger<KubectlExecutionController> _logger;
    private readonly CustomerLogger _customerLogger;
    private readonly IReasoningLoopManager _reasoningLoopManager;
    private readonly CoreSettings _coreSettings;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

    public KubectlExecutionController(
        IThreadRepository threadRepository,
        IKubePlugin kubePlugin,
        IReasoningLoopManager reasoningLoopManager,
        CoreSettings coreSettings,
        ILogger<KubectlExecutionController> logger,
        CustomerLogger customerLogger,
        IWebHostEnvironment hostEnvironment,
        IAgentOutboundCommunicationService agentOutboundCommunicationService)
    {
        _reasoningLoopManager = reasoningLoopManager;
        _threadRepository = threadRepository;
        _kubePlugin = kubePlugin;
        _logger = logger;
        _customerLogger = customerLogger;
        _coreSettings = coreSettings;
        _hostEnvironment = hostEnvironment;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
    }

    /// <summary>
    /// Execute or cancel a Kubectl command
    /// </summary>
    [HttpPost("{threadId}/{executionId}/action")]
    [AuthorizeArmOperation(ArmOperations.AgentThreadApproveActionId)]
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
        var execution = await _threadRepository.GetKubectlExecutionAsync(threadGuid, executionGuid);
        if (execution == null)
        {
            _customerLogger.LogMessage($"[ChatThreadId {threadId}] Kubectl attempted execution not found: {executionId}");
            _customerLogger.LogCustomEvent("KubectlExecution", new Dictionary<string, string>
            {
                { "ChatThreadId", threadId },
                { "ExecutionId", executionId },
                { "Action", request.Action },
                { "User", request.User ?? string.Empty },
                { "Error", "Attempted execution not found" }
            });
            _customerLogger.LogToolEvents("KubectlExecution", executionGuid.ToString(), "Attempted execution not found", isExecutionFailed: true, properties: new Dictionary<string, string>
            {
                { "ChatThreadId", threadId },
                { "Action", request.Action },
                { "User", request.User ?? string.Empty }
            });
            return NotFound(new { error = "Execution not found" });
        }
        // Validate current status
        if (!execution.Status.IsPending())
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

        var tokenScope = Constants.AksOboTokenScope;
        var exchangedTokens = Request.Headers[Constants.ExchangedTokensHeader].ToString();
        var exchangedTokenScopes = Request.Headers[Constants.ExchangedTokenScopesHeader].ToString();

        if (!string.IsNullOrEmpty(exchangedTokens) && !string.IsNullOrEmpty(exchangedTokenScopes))
        {
            token = exchangedTokens;
            tokenScope = exchangedTokenScopes;
        }

        // Get user info from token or use provided user
        var userName = "Unknown User";
        var userId = request.User ?? "agent-default"; // Use provided user or default
        string? userEmail = null;

        // Check if user is sreagent-client (frontend default)
        if (userId == "agent-default")
        {
            userName = "SRE Agent";
        }
        else if (!string.IsNullOrEmpty(authzHeader) && authzHeader.StartsWith("Bearer "))
        {
            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(authzHeader.Substring("Bearer ".Length).Trim()) as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;

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
        var kubectlExecutions = await _threadRepository.GetMessagesWithKubectlAsync(threadGuid);
        var messageId = kubectlExecutions.FirstOrDefault(m => m.KubectlExecution?.Id == executionGuid)?.Id ?? default;

        switch (request.Action.ToLower())
        {
            case "run":
                var executionDoc = await _threadRepository.GetKubectlExecutionAsync(threadGuid, executionGuid);
                if (executionDoc == null || executionDoc.AgentContextId == null)
                {
                    return NotFound(new { error = "AgentContextId not set in the executionDoc" });
                }

                var agentContext = await _threadRepository.GetAgentContextAsync(agentContextId: executionDoc.AgentContextId.Value, threadId: threadGuid);
                // Update execution with user info

                // Extract the resource ID from the command or use a placeholder
                // This would need to be adapted to your actual command structure
                var resourceId = execution.ClusterResourceId;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        CliExecutionResult result;
                        if (execution.Status == KubectlExecutionStatus.Pending)
                        {
                            execution = execution with
                            {
                                Status = KubectlExecutionStatus.Running,
                                StartedTimestamp = DateTime.UtcNow,
                                ExecutedBy = new Author(
                                    DisplayName: "SRE Agent Client",
                                    UserId: "sreagent-client",
                                    Role: Role.SREAgent
                                )
                            };
                            await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);
                            await _agentOutboundCommunicationService.NotifyKubectlUpdate(threadGuid, execution, messageId);

                            _logger.LogInternalInformation($"[{threadGuid}]Executing {executionGuid} with agent identity");
                            result = await _kubePlugin.ExecuteKubectlCommandSafely(resourceId, execution.Command, execution.Stdin);
                            if (result.ErrorOccurred && result.ErrorType == CliErrorType.AuthorizationError)
                            {
                                // trigger obo flow
                                execution = execution with
                                {
                                    Status = KubectlExecutionStatus.PendingAuthorization,
                                    Description = $"{execution.Description}",
                                    Output = null,
                                    ExecutedBy = null,
                                    Error = null,
                                    StartedTimestamp = null,
                                    CompletedTimestamp = null,
                                    RequiredScopes = result.RequiredScopes,
                                };
                                await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);
                                await _agentOutboundCommunicationService.NotifyKubectlUpdate(threadGuid, execution, messageId);
                                return;
                            }
                        }
                        else
                        {
                            execution = execution with
                            {
                                Status = KubectlExecutionStatus.Running,
                                StartedTimestamp = DateTime.UtcNow,
                                ExecutedBy = new Author(
                                    DisplayName: $"{userName} <{userEmail}>",
                                    UserId: userId,
                                    Role: Role.User
                                )
                            };
                            await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);
                            await _agentOutboundCommunicationService.NotifyKubectlUpdate(threadGuid, execution, messageId);

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
                                                Description: $"Execute kubectl command {execution.Command}",
                                                Status: ApprovalDecision.Authorized,
                                                CreatedTimestamp: execution.CreatedTimestamp,
                                                DecisionTimestamp: DateTime.UtcNow,
                                                OrchestrationId: null,
                                                AgentContextId: agentContext?.Id,
                                                DecisionUser: execution.ExecutedBy,
                                                OboToken: token,
                                                OboTokenScope: tokenScope);

                            await _threadRepository.CreateApprovalAsync(approval);

                            var approvalContext = new ApprovalContext(
                                ThreadId: threadGuid,
                                ApprovalId: approval.Id,
                                UseOboToken: true
                            );
                            ToolStatic.AsyncLocalApprovalContext.Value = approvalContext;

                            _logger.LogInternalInformation($"[{threadGuid}]Executing {executionGuid} with obo token");
                            result = await _kubePlugin.ExecuteKubectlCommandSafely(resourceId, execution.Command, execution.Stdin);
                        }

                        var output = result.Output;
                        execution = execution with
                        {
                            Status = result.ErrorOccurred ? KubectlExecutionStatus.Failed : KubectlExecutionStatus.Completed,
                            Output = result.Output,
                            CompletedTimestamp = DateTime.UtcNow
                        };

                        await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);
                        await _agentOutboundCommunicationService.NotifyKubectlUpdate(threadGuid, execution, messageId);
                        if (agentContext != null)
                        {
                            var functionCall = !string.IsNullOrEmpty(execution.OriginalFunctionCall)
                                ? JsonSerializer.Deserialize<FunctionCallContent>(execution.OriginalFunctionCall)
                                : null;
                            if (functionCall != null)
                            {
                                await _reasoningLoopManager.AppendFunctionCallMessagesAsync(agentContext,
                                [
                                    new(ChatRole.Assistant, [functionCall]),
                                    new(ChatRole.Tool, [new FunctionResultContent(functionCall.CallId, output)])
                                ]);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, "Failed to execute command");

                        // Update execution with failure
                        execution = execution with
                        {
                            Status = KubectlExecutionStatus.Failed,
                            Error = ex.Message,
                            CompletedTimestamp = DateTime.UtcNow
                        };

                        if (agentContext != null)
                        {
                            var functionCall = !string.IsNullOrEmpty(execution.OriginalFunctionCall) ? JsonSerializer.Deserialize<FunctionCallContent>(execution.OriginalFunctionCall) : null;
                            if (functionCall != null)
                            {
                                await _reasoningLoopManager.AppendFunctionCallMessagesAsync(agentContext,
                                [
                                    new(ChatRole.Assistant, [functionCall]),
                                    new(ChatRole.Tool, [new FunctionResultContent(
                                        functionCall.CallId,
                                        $"Execution Failed: {execution.Command}, Result: {ex.Message}")])
                                ]);
                            }
                        }

                        await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);
                        await _agentOutboundCommunicationService.NotifyKubectlUpdate(threadGuid, execution, messageId);
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
                    Status = KubectlExecutionStatus.Cancelled,
                    CompletedTimestamp = DateTime.UtcNow,
                    ExecutedBy = new Author(
                        DisplayName: $"{userName} <{userEmail}>",
                        UserId: userId,
                        Role: Role.User
                    )
                };
                await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);
                await _agentOutboundCommunicationService.NotifyKubectlUpdate(threadGuid, execution, messageId);

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
    [HttpGet("{threadId}/{executionId}/status")]
    [AuthorizeArmOperation(ArmOperations.AgentThreadReadActionId)]
    public async Task<IActionResult> GetExecutionStatus(string threadId, string executionId)
    {
        _logger.LogInternalInformation("Getting execution status for thread {ThreadId} execution {ExecutionId}",
            threadId, executionId);

        var execution = await _threadRepository.GetKubectlExecutionAsync(
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
    /// Get kubectl execution output
    /// </summary>
    [HttpGet("{threadId}/{executionId}/output")]
    [AuthorizeArmOperation(ArmOperations.AgentThreadReadActionId)]
    public async Task<IActionResult> GetExecutionOutput(string threadId, string executionId)
    {
        _logger.LogInternalInformation("Getting kubectl execution output for thread {ThreadId} execution {ExecutionId}",
            threadId, executionId);

        try
        {
            var execution = await _threadRepository.GetKubectlExecutionAsync(
                Guid.Parse(threadId),
                Guid.Parse(executionId)
            );

            if (execution == null)
            {
                return NotFound(new { error = "Execution not found" });
            }

            // Return current state as JSON
            return Ok(new
            {
                output = execution.Output ?? "",
                status = execution.Status.ToString(),
                error = execution.Error,
                completed = execution.Status != KubectlExecutionStatus.Running &&
                            execution.Status != KubectlExecutionStatus.Pending &&
                            execution.Status != KubectlExecutionStatus.PendingAuthorization,
                completedTimestamp = execution.CompletedTimestamp
            });
        }
        catch (FormatException ex)
        {
            _logger.LogInternalError(ex, "Invalid GUID format for threadId or executionId");
            return BadRequest(new { error = "Invalid ID format" });
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting kubectl execution output");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }        /// <summary>
             /// Model for execution action request
             /// </summary>
    public class ExecutionActionRequest
    {
        [Required]
        public string Action { get; set; } = string.Empty; // "run" or "cancel"

        public string? User { get; set; }
    }
}
