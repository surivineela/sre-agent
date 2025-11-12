// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.ScheduledTasks.Services;
using Microsoft.Extensions.AI;
using NCrontab;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class ScheduledTaskPluginDefinition(
    IScheduledTaskManagementService scheduledTaskService,
    IAgentOutboundCommunicationService agentOutboundCommunicationService
) : ContextToolTarget<AgentContext>
{
    [Description(
    """
    Create recurring tasks run by cron to monitor or perform actions at regular intervals. Ideal for monitoring incidents, checking system health, or performing maintenance. Tasks execute automatically per the cron schedule.

    Chain of Thought example:
    1) User wants ongoing monitoring (e.g., "monitor this issue for the next hour")
    2) Choose frequency (e.g., every 15 minutes)
    3) Calculate duration if specified (e.g., 1 hour = 4 executions at 15-minute intervals)
    4) Create descriptive task name and monitoring prompt
    5) Use this tool with appropriate cron expression and parameters

    Include as much metadata as possible in the prompt (e.g., azure resource ids, reasoning, ability for the agent to autonomously take actions).

    Cron format: "minute hour day month day-of-week"
    Examples: "*/15 * * * *" (every 15 minutes), "0 */1 * * *" (every hour), "0 9 * * 1-5" (9am weekdays)
    """)]
    public async Task<ScheduledTaskCreationResult> CreateScheduledMonitoringTask(
        [Description("Name for the scheduled task (should be descriptive and concise)")]
        string taskName,
        [Description("Description of what this task will monitor or do")]
        string description,
        [Description("Cron expression for scheduling. Examples: '*/15 * * * *' (every 15 min), '0 */1 * * *' (hourly), '0 9 * * 1-5' (9am weekdays)")]
        string cronExpression,
        [Description("What should the agent check or do when this task runs (detailed prompt for the agent)")]
        string agentPrompt,
        [Description("How long to run this task (in hours). Use 0 for indefinite, null for no end time")]
        int? durationHours = null,
        [Description("Maximum number of executions before task completes. Use null for unlimited")]
        int? maxExecutions = null,
        [Description("Whether to continue using the current thread context (true) or create new threads (false)")]
        bool useCurrentThread = true
    )
    {
        try
        {
            // Validate cron expression
            if (!IsValidCronExpression(cronExpression))
            {
                return new ScheduledTaskCreationResult(
                    Success: false,
                    Message: $"Invalid cron expression: {cronExpression}. Please use format 'minute hour day month day-of-week'"
                );
            }

            var startTime = DateTime.UtcNow;
            var endTime = durationHours.HasValue && durationHours > 0
                ? startTime.AddHours(durationHours.Value)
                : (DateTime?)null;

            var request = new CreateScheduledTaskRequest(
                Name: taskName,
                Description: description,
                CronExpression: cronExpression,
                StartTime: startTime,
                EndTime: endTime,
                AgentPrompt: agentPrompt,
                Agent: null, // Let the service determine the agent
                ThreadId: useCurrentThread ? Context?.ThreadId.ToString() : null,
                CreatedBy: "SREAgent",
                ExecutionContext: null,
                MaxExecutions: maxExecutions,
                NotificationChannel: null
            );

            var task = await scheduledTaskService.CreateScheduledTask(request);

            var durationText = durationHours.HasValue && durationHours > 0
                ? $" for the next {durationHours} hours"
                : " until manually stopped";

            var maxExecText = maxExecutions.HasValue
                ? $" (max {maxExecutions} executions)"
                : "";

            // Build structured payload and serialize to ensure proper escaping (quotes, newlines, etc.)
            var payload = new
            {
                taskId = task.Id,
                taskName,
                description,
                cronExpression,
                agentPrompt, // JsonSerializer will escape quotes/newlines correctly
                status = "Active",
                durationText,
                maxExecutionsText = maxExecText,
                createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var json = JsonSerializer.Serialize(payload);
            var cardMessage = $"[SCHEDULED_TASK_CREATED]{json}[/SCHEDULED_TASK_CREATED]";

            // Send the special message to the chat
            var threadId = Context?.ThreadId ?? Agent.Core.ToolStatic.AsyncLocalThreadId.Value;
            if (threadId == Guid.Empty)
            {
                threadId = Agent.Core.ToolStatic.AsyncLocalThreadId.Value;
            }

            var chatMessage = new ChatMessage(ChatRole.Assistant, cardMessage);
            await agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                threadId,
                string.Empty,
                chatMessage
            );

            return new ScheduledTaskCreationResult(
                Success: true,
                TaskId: task.Id,
                Message: $"Successfully created scheduled task '{taskName}'. Task will run {cronExpression}{durationText}{maxExecText}."
            );
        }
        catch (Exception ex)
        {
            return new ScheduledTaskCreationResult(
                Success: false,
                Message: $"Failed to create scheduled task: {ex.Message}"
            );
        }
    }

    [Description("List all active scheduled tasks, optionally filtered by the current thread")]
    public async Task<List<ScheduledTaskSummary>> ListScheduledTasks(
        [Description("Whether to show only tasks associated with the current thread (true) or all tasks (false)")]
        bool currentThreadOnly = false)
    {
        try
        {
            List<ScheduledTaskDocument> tasks;

            if (currentThreadOnly && Context?.ThreadId != null)
            {
                tasks = await scheduledTaskService.GetTasksByThread(Context.ThreadId.ToString());
            }
            else
            {
                tasks = await scheduledTaskService.ListScheduledTasks();
            }

            return tasks.Select(task => new ScheduledTaskSummary(
                Id: task.Id,
                Name: task.Name,
                Description: task.Description,
                Status: task.Status,
                LastExecutionTime: task.LastExecutionTime,
                ExecutionCount: task.ExecutionCount
            )).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to list scheduled tasks: {ex.Message}", ex);
        }
    }

    [Description("Cancel/delete a scheduled task by its ID")]
    public async Task<bool> CancelScheduledTask(
        [Description("The ID of the scheduled task to cancel")]
        string taskId)
    {
        try
        {
            var success = await scheduledTaskService.DeleteScheduledTask(taskId);
            return success;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to cancel scheduled task {taskId}: {ex.Message}", ex);
        }
    }

    [Description("Pause a scheduled task (stops execution but keeps the task)")]
    public async Task<bool> PauseScheduledTask(
        [Description("The ID of the scheduled task to pause")]
        string taskId)
    {
        try
        {
            return await scheduledTaskService.PauseScheduledTask(taskId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to pause scheduled task {taskId}: {ex.Message}", ex);
        }
    }

    [Description("Resume a paused scheduled task")]
    public async Task<bool> ResumeScheduledTask(
        [Description("The ID of the scheduled task to resume")]
        string taskId)
    {
        try
        {
            return await scheduledTaskService.ResumeScheduledTask(taskId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to resume scheduled task {taskId}: {ex.Message}", ex);
        }
    }

    [Description("Get execution history for a scheduled task")]
    public async Task<List<ScheduledTaskExecution>> GetTaskExecutionHistory(
        [Description("The ID of the scheduled task")]
        string taskId)
    {
        try
        {
            return await scheduledTaskService.GetTaskExecutionHistory(taskId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get execution history for task {taskId}: {ex.Message}", ex);
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
