// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.ScheduledTasks.Services;
using Agent.Web.Authorization;
using Agent.Web.Models.ExtendedAgents.Response;
using Agent.Web.Models.ScheduledTasks.Request;
using Agent.Web.Models.ScheduledTasks.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using static Agent.Core.Constants;

namespace Agent.Web.Controllers.v1
{
    public record CreateScheduledTaskApiRequest(
        [Required] string Name,
        [Required] string Description,
        [Required] string CronExpression,
        [Required] string AgentPrompt,
        string? Agent = null,
        DateTime? StartTime = null,
        DateTime? EndTime = null,
        string? ThreadId = null,
        Dictionary<string, object>? ExecutionContext = null,
        int? MaxExecutions = null,
        string? NotificationChannel = null
    );

    public record UpdateScheduledTaskApiRequest(
        string? Name = null,
        string? Description = null,
        string? CronExpression = null,
        string? AgentPrompt = null,
        DateTime? StartTime = null,
        DateTime? EndTime = null,
        ScheduledTaskStatus? Status = null,
        Dictionary<string, object>? ExecutionContext = null,
        int? MaxExecutions = null,
        string? NotificationChannel = null
    );

    [ApiController]
    [Route("api/v1/[controller]")]
    public class ScheduledTasksController(
        IScheduledTaskManagementService scheduledTaskService,
        ILogger<ScheduledTasksController> logger,
        IChatClientProvider chatClientProvider) : ControllerBase
    {
        [HttpGet]
        [AuthorizeArmOperation(ArmOperations.AgentScheduledTaskReadActionId)]
        public async Task<IActionResult> GetScheduledTasks()
        {
            try
            {
                logger.LogInternalInformation("Getting all scheduled tasks");
                var tasks = await scheduledTaskService.ListScheduledTasks();

                var response = tasks.Select(task => new
                {
                    task.Id,
                    task.Name,
                    task.Description,
                    task.Status,
                    task.CronExpression,
                    task.AgentPrompt,
                    task.Agent,
                    task.CreatedBy,
                    task.CreatedAt,
                    task.LastExecutionTime,
                    NextExecutionTime = ScheduledTaskExecutionService.GetNextExecutionTime(task, DateTime.UtcNow),
                    task.ExecutionCount,
                    task.MaxExecutions,
                    task.ThreadId
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error getting scheduled tasks");
                return StatusCode(500, new { error = "Failed to retrieve scheduled tasks" });
            }
        }

        [HttpGet("{id}")]
        [AuthorizeArmOperation(ArmOperations.AgentScheduledTaskReadActionId)]
        public async Task<IActionResult> GetScheduledTask(string id)
        {
            try
            {
                logger.LogInternalInformation("Getting scheduled task: {TaskId}", id);
                var task = await scheduledTaskService.GetScheduledTask(id);

                if (task == null)
                {
                    return NotFound(new { error = $"Scheduled task {id} not found" });
                }

                var response = new
                {
                    task.Id,
                    task.Name,
                    task.Description,
                    task.Status,
                    task.CronExpression,
                    task.StartTime,
                    task.EndTime,
                    task.AgentPrompt,
                    task.Agent,
                    task.ThreadId,
                    task.CreatedBy,
                    task.CreatedAt,
                    task.LastExecutionTime,
                    NextExecutionTime = ScheduledTaskExecutionService.GetNextExecutionTime(task, DateTime.UtcNow),
                    task.ExecutionCount,
                    task.MaxExecutions,
                    task.NotificationChannel,
                    task.ExecutionContext,
                    ExecutionHistory = task.ExecutionHistory?.OrderByDescending(e => e.ExecutionTime).Take(10).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error getting scheduled task: {TaskId}", id);
                return StatusCode(500, new { error = "Failed to retrieve scheduled task" });
            }
        }

        [HttpPost]
        [AuthorizeArmOperation(ArmOperations.AgentScheduledTaskWriteActionId)]
        public async Task<IActionResult> CreateScheduledTask([FromBody] CreateScheduledTaskApiRequest request)
        {
            try
            {
                logger.LogInternalInformation("Creating scheduled task: {TaskName}", request.Name);

                var createRequest = new CreateScheduledTaskRequest(
                    Name: request.Name,
                    Description: request.Description,
                    CronExpression: request.CronExpression,
                    StartTime: request.StartTime ?? DateTime.UtcNow,
                    EndTime: request.EndTime,
                    AgentPrompt: request.AgentPrompt,
                    Agent: request.Agent,
                    ThreadId: request.ThreadId,
                    CreatedBy: "api", // TODO: Get from authentication context
                    ExecutionContext: request.ExecutionContext,
                    MaxExecutions: request.MaxExecutions,
                    NotificationChannel: request.NotificationChannel
                );

                var task = await scheduledTaskService.CreateScheduledTask(createRequest);

                logger.LogInternalInformation("Created scheduled task: {TaskId}", task.Id);
                return CreatedAtAction(
                    nameof(GetScheduledTask),
                    new { id = task.Id },
                    new { taskId = task.Id, message = "Scheduled task created successfully" });
            }
            catch (ArgumentException ex)
            {
                logger.LogInternalWarning(ex, "Invalid request for creating scheduled task");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error creating scheduled task: {TaskName}", request.Name);
                return StatusCode(500, new { error = "Failed to create scheduled task" });
            }
        }

        [HttpPut("{id}")]
        [AuthorizeArmOperation(ArmOperations.AgentScheduledTaskWriteActionId)]
        public async Task<IActionResult> UpdateScheduledTask(string id, [FromBody] UpdateScheduledTaskApiRequest request)
        {
            try
            {
                logger.LogInternalInformation("Updating scheduled task: {TaskId}", id);

                var updateRequest = new UpdateScheduledTaskRequest(
                    Name: request.Name,
                    Description: request.Description,
                    CronExpression: request.CronExpression,
                    StartTime: request.StartTime,
                    EndTime: request.EndTime,
                    AgentPrompt: request.AgentPrompt,
                    Status: request.Status,
                    ExecutionContext: request.ExecutionContext,
                    MaxExecutions: request.MaxExecutions,
                    NotificationChannel: request.NotificationChannel
                );

                var task = await scheduledTaskService.UpdateScheduledTask(id, updateRequest);

                logger.LogInternalInformation("Updated scheduled task: {TaskId}", id);
                return Ok(new { message = "Scheduled task updated successfully" });
            }
            catch (ArgumentException ex)
            {
                logger.LogInternalWarning(ex, "Invalid request for updating scheduled task: {TaskId}", id);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error updating scheduled task: {TaskId}", id);
                return StatusCode(500, new { error = "Failed to update scheduled task" });
            }
        }

        /// <summary>
        /// Generates a cron expression from natural-language schedule descriptions using the platform's AI client.
        /// </summary>
        [HttpPost("cron/generate")]
        [AuthorizeArmOperation(ArmOperations.AgentScheduledTaskWriteActionId)]
        public async Task<ActionResult<CronExpressionGenerationResponse>> GenerateCronExpression(
            [FromBody] CronExpressionGenerationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new { error = "Description is required to generate a cron expression." });
            }

            try
            {
                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine("You are an expert scheduler who translates natural language into precise cron expressions.");
                promptBuilder.AppendLine("Always return valid cron expressions that align with the user's intent.");
                promptBuilder.AppendLine("If the request is ambiguous, make reasonable assumptions but list them explicitly.");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Return ONLY JSON with this shape:");
                promptBuilder.AppendLine("{");
                promptBuilder.AppendLine("  \"cronExpression\": \"cron string in standard format\",");
                promptBuilder.AppendLine("  \"humanReadableDescription\": \"succinct plain-language summary\",");
                promptBuilder.AppendLine("  \"timezone\": \"IANA timezone or null if unspecified\",");
                promptBuilder.AppendLine("  \"assumptions\": [\"List any assumptions you made\"],");
                promptBuilder.AppendLine("  \"warnings\": [\"List any validation warnings\"],");
                promptBuilder.AppendLine("  \"examples\": [\"Optional related cron examples\"]");
                promptBuilder.AppendLine("}");
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("User request between <<< and >>>");
                promptBuilder.AppendLine("<<<");
                promptBuilder.AppendLine(request.Description.Trim());
                promptBuilder.AppendLine(">>>");

                if (!string.IsNullOrWhiteSpace(request.Timezone))
                {
                    promptBuilder.AppendLine($"Timezone hint: {request.Timezone}");
                }

                if (request.StartTime.HasValue)
                {
                    promptBuilder.AppendLine($"Preferred start time: {request.StartTime:O}");
                }

                if (!string.IsNullOrWhiteSpace(request.Format))
                {
                    promptBuilder.AppendLine($"Cron format preference: {request.Format}");
                }

                var chatOptions = new ChatOptions { Temperature = 1.0f };
                var chatResponse = await chatClientProvider.DefaultModel.GetResponseAsync(promptBuilder.ToString(), chatOptions, HttpContext.RequestAborted);
                var content = chatResponse?.GetMessage()?.Text ?? string.Empty;

                logger.LogInternalInformation("Cron generation raw response: {Content}", content);

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                CronExpressionGenerationResponse? result = null;
                if (!string.IsNullOrWhiteSpace(content))
                {
                    try
                    {
                        result = JsonSerializer.Deserialize<CronExpressionGenerationResponse>(content, jsonOptions);
                    }
                    catch (JsonException jsonEx)
                    {
                        logger.LogInternalWarning(jsonEx, "Failed to parse cron generation response JSON.");
                    }
                }

                if (result != null)
                {
                    result.Timezone ??= request.Timezone;
                    result.Assumptions ??= new List<string>();
                    result.Warnings ??= new List<string>();
                    result.Examples ??= new List<string>();
                    return Ok(result);
                }
                else
                {
                    return StatusCode(500, new { error = "Failed to generate cron expression" });
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error generating cron expression from natural language.");
                return StatusCode(500, new { error = "Failed to generate cron expression" });
            }
        }

        /// <summary>
        /// Improves the agent instructions used by a scheduled task.
        /// </summary>
        [HttpPost("prompt/improve")]
        [AuthorizeArmOperation(ArmOperations.AgentScheduledTaskWriteActionId)]
        public async Task<ActionResult<ScheduledTaskPromptImprovementResponse>> ImproveScheduledTaskPrompt(
            [FromBody] ScheduledTaskPromptImprovementRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return BadRequest(new { error = "Prompt is required." });
            }

            try
            {
                var systemPrompt = """
You are an elite Azure SRE automation engineer who writes concise, production-ready scheduled task instructions.

Improve the user's draft so an Azure SRE Agent can execute recurring operational workflows safely and consistently.

Return ONLY valid JSON with this shape:
{
    "improvedPrompt": "replacement prompt text",
    "warnings": ["List any critical issues that still need human review"],
    "suggestions": ["Optional enhancements the user may apply"],
    "followUpQuestions": ["Questions to clarify missing context, if any"]
}

Keep improvements grounded, specific, and task-focused. Avoid changing intent but tighten clarity, dependencies, and success criteria. Highlight gaps (credentials, resource IDs, time windows, validation steps).

Prompt to improve (between <<< and >>>):
<<<
""";

                var prompt = systemPrompt + request.Prompt.Trim() + "\n>>>";

                var chatOptions = new ChatOptions { Temperature = 1.0f };
                var chatResponse = await chatClientProvider.DefaultModel.GetResponseAsync(prompt, chatOptions, HttpContext.RequestAborted);
                var content = chatResponse?.GetMessage()?.Text ?? string.Empty;

                logger.LogInternalInformation("Scheduled task prompt improvement raw response: {Content}", content);

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var result = JsonSerializer.Deserialize<ScheduledTaskPromptImprovementResponse>(content, jsonOptions);
                if (result == null || string.IsNullOrWhiteSpace(result.ImprovedPrompt))
                {
                    return BadRequest("Failed to parse response");
                }

                result.Warnings ??= new List<string>();
                result.Suggestions ??= new List<string>();
                result.FollowUpQuestions ??= new List<string>();

                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error improving scheduled task prompt.");
                return StatusCode(500, new ScheduledTaskPromptImprovementResponse
                {
                    ImprovedPrompt = request.Prompt,
                    Warnings = new List<string> { "Failed to improve prompt due to an unexpected error." },
                    Suggestions = new List<string> { ex.Message },
                    FollowUpQuestions = new List<string>()
                });
            }
        }

        [HttpDelete("{id}")]
        [AuthorizeArmOperation(ArmOperations.AgentScheduledTaskDeleteActionId)]
        public async Task<IActionResult> DeleteScheduledTask(string id)
        {
            try
            {
                logger.LogInternalInformation("Deleting scheduled task: {TaskId}", id);

                var success = await scheduledTaskService.DeleteScheduledTask(id);

                if (!success)
                {
                    return NotFound(new { error = $"Scheduled task {id} not found" });
                }

                logger.LogInternalInformation("Deleted scheduled task: {TaskId}", id);
                return Ok(new { message = "Scheduled task deleted successfully" });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error deleting scheduled task: {TaskId}", id);
                return StatusCode(500, new { error = "Failed to delete scheduled task" });
            }
        }

        [HttpPost("{id}/pause")]
        [AuthorizeArmOperation(ArmOperations.AgentScheduledTaskWriteActionId)]
        public async Task<IActionResult> PauseScheduledTask(string id)
        {
            try
            {
                logger.LogInternalInformation("Pausing scheduled task: {TaskId}", id);

                var success = await scheduledTaskService.PauseScheduledTask(id);

                if (!success)
                {
                    return NotFound(new { error = $"Scheduled task {id} not found" });
                }

                logger.LogInternalInformation("Paused scheduled task: {TaskId}", id);
                return Ok(new { message = "Scheduled task paused successfully" });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error pausing scheduled task: {TaskId}", id);
                return StatusCode(500, new { error = "Failed to pause scheduled task" });
            }
        }

        [HttpPost("{id}/resume")]
        [AuthorizeArmOperation(ArmOperations.AgentScheduledTaskWriteActionId)]
        public async Task<IActionResult> ResumeScheduledTask(string id)
        {
            try
            {
                logger.LogInternalInformation("Resuming scheduled task: {TaskId}", id);

                var success = await scheduledTaskService.ResumeScheduledTask(id);

                if (!success)
                {
                    return NotFound(new { error = $"Scheduled task {id} not found" });
                }

                logger.LogInternalInformation("Resumed scheduled task: {TaskId}", id);
                return Ok(new { message = "Scheduled task resumed successfully" });
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error resuming scheduled task: {TaskId}", id);
                return StatusCode(500, new { error = "Failed to resume scheduled task" });
            }
        }

        [HttpGet("{id}/executions")]
        [AuthorizeArmOperation(ArmOperations.AgentScheduledTaskReadActionId)]
        public async Task<IActionResult> GetTaskExecutionHistory(string id)
        {
            try
            {
                logger.LogInternalInformation("Getting execution history for task: {TaskId}", id);

                var executions = await scheduledTaskService.GetTaskExecutionHistory(id);

                var response = executions
                    .OrderByDescending(e => e.ExecutionTime)
                    .Select(e => new
                    {
                        e.ExecutionTime,
                        e.ThreadId,
                        e.Success,
                        e.ErrorMessage,
                        e.ExecutionMetadata
                    })
                    .ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error getting execution history for task: {TaskId}", id);
                return StatusCode(500, new { error = "Failed to retrieve execution history" });
            }
        }

        [HttpGet("thread/{threadId}")]
        [AuthorizeArmOperation(ArmOperations.AgentScheduledTaskReadActionId)]
        public async Task<IActionResult> GetScheduledTasksByThread(string threadId)
        {
            try
            {
                logger.LogInternalInformation("Getting scheduled tasks for thread: {ThreadId}", threadId);

                var tasks = await scheduledTaskService.GetTasksByThread(threadId);

                var response = tasks.Select(task => new
                {
                    task.Id,
                    task.Name,
                    task.Description,
                    task.Status,
                    task.CronExpression,
                    task.CreatedAt,
                    task.LastExecutionTime,
                    NextExecutionTime = ScheduledTaskExecutionService.GetNextExecutionTime(task, DateTime.UtcNow),
                    task.ExecutionCount
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error getting scheduled tasks for thread: {ThreadId}", threadId);
                return StatusCode(500, new { error = "Failed to retrieve scheduled tasks for thread" });
            }
        }
    }
}
