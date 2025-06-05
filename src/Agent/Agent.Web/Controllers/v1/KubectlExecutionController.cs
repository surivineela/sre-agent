using System.ComponentModel.DataAnnotations;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Plugins;
using Agent.Runtime.Reasoning;
using Microsoft.AspNetCore.Mvc;

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

        public KubectlExecutionController(
            IThreadRepository threadRepository,
            IKubePlugin kubePlugin,
            IReasoningLoopManager reasoningLoopManager,
            CoreSettings coreSettings,
            ILogger<KubectlExecutionController> logger)
        {
            _reasoningLoopManager = reasoningLoopManager;
            _threadRepository = threadRepository;
            _kubePlugin = kubePlugin;
            _logger = logger;
            _coreSettings = coreSettings;
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
            }

            // Validate current status
            if (execution.Status != KubectlExecutionStatus.Pending)
            {
                return Conflict(new
                {
                    error = $"Cannot {request.Action} execution in {execution.Status} state",
                    currentStatus = execution.Status.ToString()
                });
            }

            // Get user info from token or use provided user
            var authzHeader = Request.Headers["Authorization"].ToString();
            string userName = "Unknown User";
            string userId = request.User ?? "sreagent-client"; // Use provided user or default

            // Check if user is sreagent-client (frontend default)
            if (userId == "sreagent-client")
            {
                userName = "SRE Agent Client";
            }
            else if (!string.IsNullOrEmpty(authzHeader) && authzHeader.StartsWith("Bearer "))
            {
                try
                {
                    var token = authzHeader.Substring("Bearer ".Length).Trim();
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var jsonToken = handler.ReadToken(token) as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;

                    if (jsonToken != null)
                    {
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
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                    AgentContext agentContext = await _threadRepository.GetAgentContextAsync(agentContextId: executionDoc.AgentContextId.Value, threadId: threadGuid);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                    // Update execution with user info
                    execution = execution with
                    {
                        Status = KubectlExecutionStatus.Running,
                        StartedTimestamp = DateTime.UtcNow,
                        ExecutedBy = new Author(
                            DisplayName: userName,
                            UserId: userId,
                            Role: Role.User
                        )
                    };
                    await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);

                    // Extract the resource ID from the command or use a placeholder
                    // This would need to be adapted to your actual command structure
                    string resourceId = execution.ClusterResourceId;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Execute the Kubectl command with stdin support
                            var output = await _kubePlugin.ExecuteKubectlCommandSafely(resourceId, execution.Command, execution.Stdin);

                            // Update execution with success
                            execution = execution with
                            {
                                Status = KubectlExecutionStatus.Completed,
                                Output = output,
                                CompletedTimestamp = DateTime.UtcNow
                            };

                            await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);
                            if (_coreSettings.UseAgentFramework && agentContext != null)
                            {
                                await _reasoningLoopManager.AppendNewMessageAsync(agentContext, new Microsoft.Extensions.AI.ChatMessage()
                                {
                                    Role = Microsoft.Extensions.AI.ChatRole.Assistant,
                                    Contents = new List<Microsoft.Extensions.AI.AIContent>
                                    {
                                        new Microsoft.Extensions.AI.TextContent($"Execution completed successfully: {execution.Command}, Result: {output}")
                                    }
                                });
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
                                await _reasoningLoopManager.AppendNewMessageAsync(agentContext, new Microsoft.Extensions.AI.ChatMessage()
                                {
                                    Role = Microsoft.Extensions.AI.ChatRole.Assistant,
                                    Contents = new List<Microsoft.Extensions.AI.AIContent>
                                    {
                                        new Microsoft.Extensions.AI.TextContent($"Execution Failed: {execution.Command}, Result: {ex.Message}")
                                    }
                                });
                            }

                            await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);
                        }
                    });

                    return Ok(new
                    {
                        status = "Running",
                        executedBy = userName,
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
                            DisplayName: userName,
                            UserId: userId,
                            Role: Role.User
                        )
                    };
                    await _threadRepository.UpdateKubectlExecutionAsync(threadGuid, execution);

                    return Ok(new
                    {
                        status = "Cancelled",
                        cancelledBy = userName,
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
        /// Stream execution output
        /// </summary>
        [HttpGet("{threadId}/{executionId}/output")]
        public async Task<IActionResult> GetExecutionOutput(string threadId, string executionId)
        {
            _logger.LogInternalInformation("Getting execution output for thread {ThreadId} execution {ExecutionId}",
                threadId, executionId);

            var execution = await _threadRepository.GetKubectlExecutionAsync(
                Guid.Parse(threadId),
                Guid.Parse(executionId)
            );

            if (execution == null)
            {
                return NotFound();
            }            // For streaming, we'll use Server-Sent Events
            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            var writer = new StreamWriter(Response.Body);

            try
            {
                while (execution.Status == KubectlExecutionStatus.Running ||
                       execution.Status == KubectlExecutionStatus.Pending)
                {
                    // Send current output
                    await writer.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(new
                    {
                        output = execution.Output ?? "",
                        status = execution.Status.ToString(),
                        error = execution.Error
                    })}\n\n");
                    await writer.FlushAsync();

                    // Wait before next poll
                    await Task.Delay(1000);

                    // Re-fetch execution
                    execution = await _threadRepository.GetKubectlExecutionAsync(
                        Guid.Parse(threadId),
                        Guid.Parse(executionId)
                    );

                    if (execution == null) break;
                }

                // Send final status
                await writer.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(new
                {
                    output = execution?.Output ?? "",
                    status = execution?.Status.ToString() ?? "Unknown",
                    error = execution?.Error,
                    completed = true
                })}\n\n");
                await writer.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error streaming execution output");
            }

            return new EmptyResult();
        }

        /// <summary>
        /// Model for execution action request
        /// </summary>
        public class ExecutionActionRequest
        {
            [Required]
            public string Action { get; set; } // "run" or "cancel"

            public string User { get; set; }
        }
    }
}
