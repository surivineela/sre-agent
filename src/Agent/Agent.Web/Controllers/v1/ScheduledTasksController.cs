// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Agent.Data.DataModels;
using Agent.ScheduledTasks.Services;
using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        ILogger<ScheduledTasksController> logger) : ControllerBase
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
