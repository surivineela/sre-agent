using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Runtime.Reasoning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class KubectlExecutionController : ControllerBase
    {
        private readonly IThreadRepository _threadRepository;
        private readonly IKubePlugin _kubePlugin;
        private readonly ILogger<KubectlExecutionController> _logger;
        private readonly IReasoningLoopManager _reasoningLoopManager;
        private readonly CoreSettings _coreSettings;
        private readonly IWebHostEnvironment _hostEnvironment;

        public KubectlExecutionController(
            IThreadRepository threadRepository,
            IKubePlugin kubePlugin,
            IReasoningLoopManager reasoningLoopManager,
            CoreSettings coreSettings,
            ILogger<KubectlExecutionController> logger,
            IWebHostEnvironment hostEnvironment)
        {
            _reasoningLoopManager = reasoningLoopManager;
            _threadRepository = threadRepository;
            _kubePlugin = kubePlugin;
            _logger = logger;
            _coreSettings = coreSettings;
            _hostEnvironment = hostEnvironment;
        }

        /// <summary>
        /// Execute or cancel a Kubectl command
        /// </summary>
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
            var execution = await _threadRepository.GetKubectlExecutionAsync(threadGuid, executionGuid);
            if (execution == null)
            {
                return NotFound(new { error = "Execution not found" });
            }            // Validate current status
            if (execution.Status != KubectlExecutionStatus.Pending && execution.Status != KubectlExecutionStatus.PendingAuthorization)
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
            string userName = "Unknown User";
            string userId = request.User ?? "agent-default"; // Use provided user or default
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

            switch (request.Action.ToLower())
            {
                case "run":
                    var executionDoc = await _threadRepository.GetKubectlExecutionAsync(threadGuid, executionGuid);
                    if (executionDoc.AgentContextId == null)
                    {
                        return NotFound(new { error = "AgentContextId not set in the executionDoc" });
                    }
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                    AgentContext agentContext = await _threadRepository.GetAgentContextAsync(agentContextId: executionDoc.AgentContextId.Value, threadId: threadGuid);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                    // Update execution with user info

                    // Extract the resource ID from the command or use a placeholder
                    // This would need to be adapted to your actual command structure
                    string resourceId = execution.ClusterResourceId;

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
                                    };
                                    await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);
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

                                _logger.LogInternalInformation($"[{threadGuid}]Executing {executionGuid} with obo token");
                                result = await _kubePlugin.ExecuteKubectlCommandSafely(resourceId, execution.Command, execution.Stdin, token ?? string.Empty);
                            }

                            var output = result.Output;
                            execution = execution with
                            {
                                Status = result.ErrorOccurred ? KubectlExecutionStatus.Failed : KubectlExecutionStatus.Completed,
                                Output = result.Output,
                                CompletedTimestamp = DateTime.UtcNow
                            };

                            await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);
                            if (_coreSettings.UseAgentFramework && agentContext != null)
                            {
                                var functionCall = !string.IsNullOrEmpty(execution.OriginalFunctionCall) ? JsonSerializer.Deserialize<FunctionCallContent>(execution.OriginalFunctionCall) : null;
                                if (functionCall != null)
                                {
                                    await _reasoningLoopManager.AppendFunctionCallMessagesAsync(agentContext, new List<Microsoft.Extensions.AI.ChatMessage>
                                    {
                                        new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant,
                                            new List<Microsoft.Extensions.AI.AIContent>{functionCall }),
                                        new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Tool,
                                        new List<Microsoft.Extensions.AI.AIContent>
                                        {
                                            new Microsoft.Extensions.AI.FunctionResultContent(functionCall.CallId, output)
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
                                Status = KubectlExecutionStatus.Failed,
                                Error = ex.Message,
                                CompletedTimestamp = DateTime.UtcNow
                            };

                            if (_coreSettings.UseAgentFramework && agentContext != null)
                            {
                                var functionCall = !string.IsNullOrEmpty(execution.OriginalFunctionCall) ? JsonSerializer.Deserialize<FunctionCallContent>(execution.OriginalFunctionCall) : null;
                                if (functionCall != null)
                                {
                                    await _reasoningLoopManager.AppendFunctionCallMessagesAsync(agentContext, new List<Microsoft.Extensions.AI.ChatMessage>
                                    {
                                        new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant,
                                            new List<Microsoft.Extensions.AI.AIContent>{functionCall }),
                                        new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Tool,
                                        new List<Microsoft.Extensions.AI.AIContent>
                                        {
                                            new Microsoft.Extensions.AI.FunctionResultContent(functionCall.CallId, $"Execution Failed: {execution.Command}, Result: {ex.Message}")
                                        })
                                    });
                                }
                            }

                            await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);
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
}
