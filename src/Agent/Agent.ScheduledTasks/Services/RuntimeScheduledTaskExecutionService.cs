// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Data.Repositories;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.ScheduledTasks.Services;

public class RuntimeScheduledTaskExecutionService : ScheduledTaskExecutionService
{
    private readonly IAgentInboundCommunicationService _agentService;
    private readonly IThreadRepository _threadRepository;

    public RuntimeScheduledTaskExecutionService(
        IScheduledTaskRepository repository,
        IAgentInboundCommunicationService agentService,
        IThreadRepository threadRepository,
        ILogger<RuntimeScheduledTaskExecutionService> logger)
        : base(repository, logger)
    {
        _agentService = agentService;
        _threadRepository = threadRepository;
    }

    public override async Task ExecuteScheduledTask(ScheduledTaskDocument task)
    {
        _logger.LogInternalInformation("Executing scheduled task: {TaskName} ({TaskId})", task.Name, task.Id);

        var executionTime = DateTime.UtcNow;
        var execution = new ScheduledTaskExecution(
            ExecutionTime: executionTime,
            ThreadId: null,
            Success: false,
            ErrorMessage: null,
            ExecutionMetadata: new Dictionary<string, object>
            {
                ["ScheduledExecutionTime"] = executionTime,
                ["TaskId"] = task.Id,
                ["TaskName"] = task.Name
            }
        );

        try
        {
            // Create or reuse thread based on task.ThreadId
            var (thread, agentContext) = task.ThreadId != null
                ? await GetOrCreateThread(task.ThreadId, task)
                : await CreateNewThread(task);

            execution = execution with { ThreadId = thread.Id.ToString() };

            // Execute the agent with the scheduled prompt - React will handle formatting
            var scheduledTaskMessage = $"[SCHEDULED_TASK_EXECUTION]{System.Text.Json.JsonSerializer.Serialize(new
            {
                taskId = task.Id,
                taskName = task.Name,
                description = task.Description,
                cronExpression = task.CronExpression,
                agentPrompt = task.AgentPrompt,
                executionTime = executionTime.ToString("O"),
                status = "Active"
            })}[/SCHEDULED_TASK_EXECUTION]";

            var threadMessage = new ThreadMessage(
                ThreadId: thread.Id,
                AgentContextId: agentContext.Id,
                MessageId: Guid.NewGuid(),
                Message: scheduledTaskMessage,
                AgentName: task.Agent,
                UserId: "scheduled-task",
                DisplayName: $"Azure SRE Agent - Scheduled Task",
                Timestamp: executionTime
            );

            await _agentService.ProcessAlertMessageAsync(threadMessage);

            // Mark execution as successful
            execution = execution with { Success = true };

            _logger.LogInternalInformation("Successfully executed scheduled task: {TaskId}", task.Id);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error executing scheduled task: {TaskId}", task.Id);
            execution = execution with
            {
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionMetadata = execution.ExecutionMetadata?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, object>()
            };
            execution.ExecutionMetadata["Exception"] = ex.ToString();
        }

        // Update task execution history and counters
        await UpdateTaskAfterExecution(task, execution);

        // Trigger compaction by sending /compact message to ReasoningLoop after successful execution
        // Only for tasks that reuse the same thread (task.ThreadId != null)
        if (execution.Success && execution.ThreadId != null && task.ThreadId != null)
        {
            try
            {
                await SendCompactMessageAsync(execution.ThreadId);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error sending compact message for scheduled task: {TaskId}", task.Id);
                // Don't fail the task if compaction message fails
            }
        }
    }

    private async Task<(Thread thread, AgentContext agentContext)> GetOrCreateThread(string threadId, ScheduledTaskDocument task)
    {
        _logger.LogInternalInformation("Getting or creating thread for scheduled task: {ThreadId}", threadId);

        var threadGuid = Guid.Parse(threadId);
        var thread = await _threadRepository.GetThreadAsync(threadGuid);

        if (thread != null)
        {
            // Thread exists, reuse it
            _logger.LogInternalInformation("Reusing existing thread: {ThreadId}", threadId);
            var agentContexts = await _threadRepository.GetAgentContextsForThreadAsync(threadGuid);
            var agentContext = agentContexts.FirstOrDefault();
            if (agentContext == null)
            {
                throw new InvalidOperationException($"No agent context found for thread: {threadId}");
            }
            return (thread, agentContext);
        }

        // Thread doesn't exist yet, create it (first execution of this task)
        _logger.LogInternalInformation("Creating dedicated thread for scheduled task: {TaskId}", task.Id);

        return await CreateNewThread(task);
    }

    private async Task<(Thread thread, AgentContext agentContext)> CreateNewThread(ScheduledTaskDocument task)
    {
        _logger.LogInternalInformation("Creating new thread for scheduled task: {TaskId}", task.Id);

        var title = $"Scheduled Task: {task.Name}";
        var message = $"[SCHEDULED_TASK_EXECUTION]{System.Text.Json.JsonSerializer.Serialize(new
        {
            taskId = task.Id,
            taskName = task.Name,
            description = task.Description,
            cronExpression = task.CronExpression,
            agentPrompt = task.AgentPrompt,
            executionTime = DateTime.UtcNow.ToString("O"),
            status = "Active"
        })}[/SCHEDULED_TASK_EXECUTION]";

        var (thread, agentContext) = await _agentService.CreateAgentThread(
            title: title,
            message: message,
            agentTypeEnum: AgentTypeEnum.Meta,
            source: ThreadSource.ScheduledTask
        );

        return (thread, agentContext);
    }

    private async Task SendCompactMessageAsync(string threadId)
    {
        _logger.LogInternalInformation("Sending /compact message to trigger compaction for thread: {ThreadId}", threadId);

        var threadGuid = Guid.Parse(threadId);

        // Get the agent context for this thread
        var agentContexts = await _threadRepository.GetAgentContextsForThreadAsync(threadGuid);
        var agentContext = agentContexts.FirstOrDefault();
        if (agentContext == null)
        {
            _logger.LogInternalWarning("No agent context found for thread {ThreadId}, cannot send compact message", threadId);
            return;
        }

        // Send /compact message to the thread, which will be handled by ReasoningLoop.HandleCompactCommandAsync
        var compactMessage = new ThreadMessage(
            ThreadId: threadGuid,
            AgentContextId: agentContext.Id,
            MessageId: Guid.NewGuid(),
            Message: "/compact",
            UserId: "scheduled-task",
            DisplayName: "Scheduled Task - Auto Compaction",
            Timestamp: DateTime.UtcNow
        );

        await _agentService.ProcessAlertMessageAsync(compactMessage);

        _logger.LogInternalInformation("Successfully sent /compact message to thread: {ThreadId}", threadId);
    }
}
