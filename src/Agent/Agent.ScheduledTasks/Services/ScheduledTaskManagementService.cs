// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Data.DataModels;
using Agent.Data.Repositories;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace Agent.ScheduledTasks.Services;

public class ScheduledTaskManagementService : IScheduledTaskManagementService
{
    private readonly IScheduledTaskRepository _repository;
    private readonly ILogger<ScheduledTaskManagementService> _logger;
    private readonly ScheduledTaskExecutionService _executionService;

    public ScheduledTaskManagementService(
        IScheduledTaskRepository repository,
        ILogger<ScheduledTaskManagementService> logger,
        ScheduledTaskExecutionService executionService)
    {
        _repository = repository;
        _logger = logger;
        _executionService = executionService;
    }

    public async Task<List<ScheduledTaskDocument>> ListScheduledTasks()
    {
        _logger.LogInternalInformation("Listing all scheduled tasks (including non-active)");
        try
        {
            return await _repository.GetAllScheduledTasksAsync();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error listing scheduled tasks");
            throw;
        }
    }

    public async Task<ScheduledTaskDocument?> GetScheduledTask(string taskId)
    {
        _logger.LogInternalInformation("Getting scheduled task: {TaskId}", taskId);

        try
        {
            return await _repository.GetScheduledTaskAsync(taskId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting scheduled task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<ScheduledTaskDocument> CreateScheduledTask(CreateScheduledTaskRequest request)
    {
        _logger.LogInternalInformation("Creating scheduled task: {TaskName}", request.Name);
        try
        {
            // Validate cron expression
            if (!IsValidCronExpression(request.CronExpression))
            {
                throw new ArgumentException($"Invalid cron expression: {request.CronExpression}");
            }

            var taskDocument = new ScheduledTaskDocument(
                Id: Guid.NewGuid().ToString(),
                Name: request.Name,
                Description: request.Description,
                CronExpression: request.CronExpression,
                StartTime: request.StartTime,
                EndTime: request.EndTime,
                AgentPrompt: request.AgentPrompt,
                Agent: request.Agent,
                ThreadId: request.ThreadId,
                CreatedBy: request.CreatedBy,
                CreatedAt: DateTime.UtcNow,
                LastExecutionTime: null,
                Status: ScheduledTaskStatus.Active,
                ExecutionContext: request.ExecutionContext,
                ExecutionHistory: new List<ScheduledTaskExecution>(),
                MaxExecutions: request.MaxExecutions,
                ExecutionCount: 0,
                NotificationChannel: request.NotificationChannel,
                AgentMode: request.AgentMode ?? AgentModes.Autonomous.ToLowerInvariant()
            );

            var createdTask = await _repository.CreateScheduledTaskAsync(taskDocument);
            _logger.LogInternalInformation("Successfully created scheduled task: {TaskId}", createdTask.Id);

            return createdTask;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error creating scheduled task: {TaskName}", request.Name);
            throw;
        }
    }

    public async Task<ScheduledTaskDocument> UpdateScheduledTask(string taskId, UpdateScheduledTaskRequest request)
    {
        _logger.LogInternalInformation("Updating scheduled task: {TaskId}", taskId);

        try
        {
            var existingTask = await _repository.GetScheduledTaskAsync(taskId);
            if (existingTask == null)
            {
                throw new ArgumentException($"Scheduled task not found: {taskId}");
            }

            // Update fields if provided
            if (request.Name != null) existingTask.Name = request.Name;
            if (request.Description != null) existingTask.Description = request.Description;
            if (request.AgentPrompt != null) existingTask.AgentPrompt = request.AgentPrompt;
            if (request.Status != null) existingTask.Status = request.Status.Value;
            if (request.ExecutionContext != null) existingTask.ExecutionContext = request.ExecutionContext;
            if (request.MaxExecutions != null) existingTask.MaxExecutions = request.MaxExecutions;
            if (request.NotificationChannel != null) existingTask.NotificationChannel = request.NotificationChannel;
            if (request.AgentMode != null) existingTask.AgentMode = request.AgentMode;
            if (request.StartTime != null) existingTask.StartTime = request.StartTime.Value;
            if (request.EndTime != null) existingTask.EndTime = request.EndTime;

            // Always update ThreadId - frontend sends either a GUID string or null
            existingTask.ThreadId = request.ThreadId;

            // Handle cron expression update
            if (request.CronExpression != null)
            {
                if (!IsValidCronExpression(request.CronExpression))
                {
                    throw new ArgumentException($"Invalid cron expression: {request.CronExpression}");
                }
                existingTask.CronExpression = request.CronExpression;
            }

            var updatedTask = await _repository.UpdateScheduledTaskAsync(existingTask);
            _logger.LogInternalInformation("Successfully updated scheduled task: {TaskId}", taskId);

            return updatedTask;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating scheduled task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<bool> DeleteScheduledTask(string taskId)
    {
        _logger.LogInternalInformation("Deleting scheduled task: {TaskId}", taskId);

        try
        {
            return await _repository.DeleteScheduledTaskAsync(taskId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error deleting scheduled task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<bool> PauseScheduledTask(string taskId)
    {
        _logger.LogInternalInformation("Pausing scheduled task: {TaskId}", taskId);

        try
        {
            var task = await _repository.GetScheduledTaskAsync(taskId);
            if (task == null)
            {
                _logger.LogInternalWarning("Scheduled task not found for pausing: {TaskId}", taskId);
                return false;
            }

            task.Status = ScheduledTaskStatus.Paused;
            await _repository.UpdateScheduledTaskAsync(task);

            _logger.LogInternalInformation("Successfully paused scheduled task: {TaskId}", taskId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error pausing scheduled task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<bool> ResumeScheduledTask(string taskId)
    {
        _logger.LogInternalInformation("Resuming scheduled task: {TaskId}", taskId);

        try
        {
            var task = await _repository.GetScheduledTaskAsync(taskId);
            if (task == null)
            {
                _logger.LogInternalWarning("Scheduled task not found for resuming: {TaskId}", taskId);
                return false;
            }

            task.Status = ScheduledTaskStatus.Active;
            await _repository.UpdateScheduledTaskAsync(task);

            _logger.LogInternalInformation("Successfully resumed scheduled task: {TaskId}", taskId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error resuming scheduled task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<List<ScheduledTaskExecution>> GetTaskExecutionHistory(string taskId)
    {
        _logger.LogInternalInformation("Getting execution history for task: {TaskId}", taskId);

        try
        {
            var task = await _repository.GetScheduledTaskAsync(taskId);
            return task?.ExecutionHistory ?? new List<ScheduledTaskExecution>();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting execution history for task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<List<ScheduledTaskDocument>> GetTasksByThread(string threadId)
    {
        _logger.LogInternalInformation("Getting scheduled tasks for thread: {ThreadId}", threadId);

        try
        {
            return await _repository.GetScheduledTasksByThreadAsync(threadId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting tasks for thread: {ThreadId}", threadId);
            throw;
        }
    }

    public async Task<ScheduledTaskExecution?> ExecuteTaskNow(string taskId)
    {
        _logger.LogInternalInformation("Manually executing task: {TaskId}", taskId);

        try
        {
            var task = await _repository.GetScheduledTaskAsync(taskId);
            if (task == null)
            {
                _logger.LogInternalWarning("Task not found for manual execution: {TaskId}", taskId);
                return null;
            }

            // Get execution history count before execution to find the newly added execution
            var executionHistoryCountBefore = task.ExecutionHistory?.Count ?? 0;

            // Execute the task using the execution service (this will update the task in the repository)
            await _executionService.ExecuteScheduledTask(task);

            // Retrieve the updated task to get the execution result
            var updatedTask = await _repository.GetScheduledTaskAsync(taskId);
            if (updatedTask == null || updatedTask.ExecutionHistory == null || updatedTask.ExecutionHistory.Count == executionHistoryCountBefore)
            {
                _logger.LogInternalWarning("Task execution did not record history: {TaskId}", taskId);
                return null;
            }

            // Get the most recent execution (the one we just added)
            var latestExecution = updatedTask.ExecutionHistory
                .OrderByDescending(e => e.ExecutionTime)
                .First();

            // Add manual execution flag to the metadata to distinguish from scheduled executions
            if (latestExecution.ExecutionMetadata == null)
            {
                latestExecution = latestExecution with
                {
                    ExecutionMetadata = new Dictionary<string, object>()
                };
            }

            latestExecution.ExecutionMetadata["ManualExecution"] = true;

            // Update the execution history with the manual execution flag
            var executionIndex = updatedTask.ExecutionHistory.Count - 1;
            updatedTask.ExecutionHistory[executionIndex] = latestExecution;
            await _repository.UpdateScheduledTaskAsync(updatedTask);

            _logger.LogInternalInformation("Task manually executed: {TaskId}, Success: {Success}", taskId, latestExecution.Success);
            return latestExecution;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error manually executing task: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<List<TaskExecutionSummary>> GetAllExecutions()
    {
        _logger.LogInternalInformation("Getting all executions across all scheduled tasks");

        try
        {
            var tasks = await _repository.GetAllScheduledTasksAsync();
            var executions = new List<TaskExecutionSummary>();

            foreach (var task in tasks)
            {
                if (task.ExecutionHistory != null)
                {
                    foreach (var execution in task.ExecutionHistory)
                    {
                        executions.Add(new TaskExecutionSummary(
                            TaskId: task.Id,
                            TaskName: task.Name,
                            Execution: execution
                        ));
                    }
                }
            }

            return executions;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting all executions");
            throw;
        }
    }

    private static bool IsValidCronExpression(string cronExpression)
    {
        try
        {
            CrontabSchedule.Parse(cronExpression);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
