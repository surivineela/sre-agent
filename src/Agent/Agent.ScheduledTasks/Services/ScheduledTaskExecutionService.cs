// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Data.Repositories;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace Agent.ScheduledTasks.Services;

public class ScheduledTaskExecutionService
{
    protected readonly IScheduledTaskRepository _repository;
    protected readonly ILogger<ScheduledTaskExecutionService> _logger;

    public ScheduledTaskExecutionService(
        IScheduledTaskRepository repository,
        ILogger<ScheduledTaskExecutionService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task ProcessDueTasksAsync(CancellationToken cancellationToken = default)
    {
        var currentTime = DateTime.UtcNow;
        _logger.LogInternalInformation("Processing due tasks at: {CurrentTime}", currentTime);

        try
        {
            var dueTasks = await _repository.GetTasksDueForExecutionAsync(currentTime);

            if (dueTasks.Count == 0)
            {
                _logger.LogInternalInformation("No tasks due for execution");
                return;
            }

            _logger.LogInternalInformation("Found {TaskCount} tasks due for execution", dueTasks.Count);

            foreach (var task in dueTasks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInternalInformation("Cancellation requested, stopping task execution");
                    break;
                }

                try
                {
                    await ExecuteScheduledTask(task);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to execute scheduled task: {TaskId}", task.Id);
                    // Continue with other tasks even if one fails
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error processing due tasks");
        }
    }

    public virtual async Task ExecuteScheduledTask(ScheduledTaskDocument task)
    {
        var executionTime = DateTime.UtcNow;
        var execution = new ScheduledTaskExecution(
            ExecutionTime: executionTime,
            ThreadId: null,
            Success: false,
            ErrorMessage: "Execution not implemented in base class",
            ExecutionMetadata: new Dictionary<string, object>
            {
                ["ScheduledExecutionTime"] = executionTime,
                ["TaskId"] = task.Id,
                ["TaskName"] = task.Name
            }
        );

        await UpdateTaskAfterExecution(task, execution);
    }

    protected async Task UpdateTaskAfterExecution(ScheduledTaskDocument task, ScheduledTaskExecution execution)
    {
        try
        {
            // Update execution history
            if (task.ExecutionHistory == null)
            {
                task.ExecutionHistory = new List<ScheduledTaskExecution>();
            }
            task.ExecutionHistory.Add(execution);

            // Keep only last 50 executions to avoid unbounded growth
            if (task.ExecutionHistory.Count > 50)
            {
                task.ExecutionHistory = task.ExecutionHistory
                    .OrderByDescending(e => e.ExecutionTime)
                    .Take(50)
                    .ToList();
            }

            // Update counters and status
            task.ExecutionCount++;
            task.LastExecutionTime = execution.ExecutionTime;

            // If task was configured to use same thread (has a placeholder threadId)
            // but thread wasn't created yet, capture the actual thread ID from first execution
            if (task.ThreadId != null && execution.ThreadId != null && task.ExecutionCount == 1)
            {
                task.ThreadId = execution.ThreadId;
                _logger.LogInternalInformation("Captured thread ID for scheduled task {TaskId}: {ThreadId}", task.Id, execution.ThreadId);
            }

            // Check if task should be completed
            if (task.MaxExecutions.HasValue && task.ExecutionCount >= task.MaxExecutions.Value)
            {
                task.Status = ScheduledTaskStatus.Completed;
                _logger.LogInternalInformation("Task reached max executions and marked as completed: {TaskId}", task.Id);
            }
            else if (task.EndTime.HasValue && DateTime.UtcNow > task.EndTime.Value)
            {
                task.Status = ScheduledTaskStatus.Expired;
                _logger.LogInternalInformation("Task expired and marked as expired: {TaskId}", task.Id);
            }
            else if (!execution.Success)
            {
                // Consider failing the task after multiple consecutive failures
                var recentFailures = task.ExecutionHistory
                    .OrderByDescending(e => e.ExecutionTime)
                    .Take(3)
                    .Count(e => !e.Success);

                if (recentFailures >= 3)
                {
                    task.Status = ScheduledTaskStatus.Failed;
                    _logger.LogInternalWarning("Task failed 3 consecutive times and marked as failed: {TaskId}", task.Id);
                }
            }

            await _repository.UpdateScheduledTaskAsync(task);
            _logger.LogInternalInformation("Updated task after execution: {TaskId}", task.Id);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating task after execution: {TaskId}", task.Id);
        }
    }

    public static DateTime? GetNextExecutionTime(ScheduledTaskDocument task, DateTime currentTime)
    {
        try
        {
            var schedule = CrontabSchedule.Parse(task.CronExpression);
            var baseTime = task.LastExecutionTime ?? task.StartTime;
            return schedule.GetNextOccurrence(baseTime > currentTime ? baseTime : currentTime);
        }
        catch
        {
            return null;
        }
    }
}
