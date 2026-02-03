// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.Reasoning;
using ApiTaskToolExecution = Agent.Core.Models.Api.v1.TaskToolExecution;
using ApiTaskToolExecutionGroup = Agent.Core.Models.Api.v1.TaskToolExecutionGroup;
using ApiTaskToolExecutionStatus = Agent.Core.Models.Api.v1.TaskToolExecutionStatus;
using FrameworkTaskToolExecution = Agent.Framework.TaskTool.TaskToolExecution;
using FrameworkTaskToolExecutionGroup = Agent.Framework.TaskTool.TaskToolExecutionGroup;

namespace Agent.Runtime.AgentTasks.Handlers;

/// <summary>
/// Helper class that subscribes to Task tool execution hooks and streams updates via SignalR.
/// </summary>
public sealed class TaskToolStreamingHelper
{
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly Guid _threadId;

    // Track message IDs for each group so we can update the same message on end
    private readonly Dictionary<string, Guid> _groupMessageIds = new();

    public TaskToolStreamingHelper(IAgentOutboundCommunicationService outboundCommunicationService, Guid threadId)
    {
        _outboundCommunicationService = outboundCommunicationService;
        _threadId = threadId;
    }

    /// <summary>
    /// Subscribes the Task tool streaming hooks to the provided run hooks.
    /// </summary>
    public void SubscribeTo(RunHooks<AgentContext> runHooks)
    {
        runHooks.TaskToolGroupStart += async (context, agent, group) =>
        {
            await OnTaskToolGroupStartAsync(group);
        };

        runHooks.TaskToolGroupEnd += async (context, agent, group) =>
        {
            await OnTaskToolGroupEndAsync(group);
        };

        runHooks.TaskToolExecutionStart += async (context, agent, execution) =>
        {
            await OnTaskToolExecutionStartAsync(execution);
        };

        runHooks.TaskToolExecutionEnd += async (context, agent, execution) =>
        {
            await OnTaskToolExecutionEndAsync(execution);
        };

        runHooks.TaskToolInvocationStart += async (context, agent, executionId, toolName, description) =>
        {
            await OnTaskToolInvocationStartAsync(executionId, toolName, description);
        };

        runHooks.TaskToolInvocationEnd += async (context, agent, executionId, toolName, success, output) =>
        {
            await OnTaskToolInvocationEndAsync(executionId, toolName, success, output);
        };
    }

    private async Task OnTaskToolGroupStartAsync(FrameworkTaskToolExecutionGroup group)
    {
        var data = new
        {
            groupId = group.GroupId,
            startedAt = group.StartedAt,
            executions = group.Executions.Select(e => new
            {
                executionId = e.ExecutionId,
                subAgentType = e.SubAgentType.ToString(),
                description = e.Description,
                status = e.Status.ToString(),
                startedAt = e.StartedAt
            })
        };

        var json = JsonSerializer.Serialize(data);
        await _outboundCommunicationService.StreamTaskToolExecutionUpdateAsync(
            _threadId,
            json,
            StreamMessageType.TaskToolGroupStart);

        // Persist to database immediately so the card shows up on page refresh
        var apiGroup = ConvertToApiModel(group);
        var messageId = await _outboundCommunicationService.AppendAgentTaskToolExecutionGroupMessage(_threadId, apiGroup);
        _groupMessageIds[group.GroupId] = messageId;
    }

    private async Task OnTaskToolGroupEndAsync(FrameworkTaskToolExecutionGroup group)
    {
        var data = new
        {
            groupId = group.GroupId,
            startedAt = group.StartedAt,
            completedAt = group.CompletedAt,
            isComplete = group.IsComplete,
            executions = group.Executions.Select(e => new
            {
                executionId = e.ExecutionId,
                subAgentType = e.SubAgentType.ToString(),
                description = e.Description,
                status = e.Status.ToString(),
                startedAt = e.StartedAt,
                completedAt = e.CompletedAt,
                error = e.Error
            })
        };

        var json = JsonSerializer.Serialize(data);
        await _outboundCommunicationService.StreamTaskToolExecutionUpdateAsync(
            _threadId,
            json,
            StreamMessageType.TaskToolGroupEnd);

        // Update the persisted message with final state (reuse the same messageId)
        var apiGroup = ConvertToApiModel(group);
        var messageId = _groupMessageIds.TryGetValue(group.GroupId, out var existingId) ? existingId : Guid.NewGuid();
        await _outboundCommunicationService.AppendAgentTaskToolExecutionGroupMessage(_threadId, apiGroup, messageId);
        _groupMessageIds.Remove(group.GroupId);
    }

    /// <summary>
    /// Converts a Framework TaskToolExecutionGroup to an API TaskToolExecutionGroup for persistence.
    /// </summary>
    private static ApiTaskToolExecutionGroup ConvertToApiModel(FrameworkTaskToolExecutionGroup group)
    {
        return new ApiTaskToolExecutionGroup
        {
            Id = group.GroupId,
            Executions = group.Executions.Select(e => new ApiTaskToolExecution
            {
                Id = e.ExecutionId,
                Description = e.Description,
                SubagentType = e.SubAgentType.ToString(),
                Prompt = null, // Don't persist prompts to minimize storage
                Status = ConvertStatus(e.Status),
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt,
                Result = TruncateResult(e.Result), // Truncate large results to minimize storage
                Error = e.Error
            }).ToList()
        };
    }

    /// <summary>
    /// Truncates the result to a reasonable size for storage (max 2000 chars).
    /// </summary>
    private static string? TruncateResult(string? result)
    {
        if (string.IsNullOrEmpty(result))
        {
            return null;
        }

        const int maxLength = 2000;
        if (result.Length <= maxLength)
        {
            return result;
        }

        return result.Substring(0, maxLength - 50) + "\n\n... [truncated, " + (result.Length - maxLength + 50) + " more chars]";
    }

    /// <summary>
    /// Converts Framework status to API status.
    /// </summary>
    private static ApiTaskToolExecutionStatus ConvertStatus(Agent.Framework.TaskTool.TaskToolExecutionStatus status)
    {
        return status switch
        {
            Agent.Framework.TaskTool.TaskToolExecutionStatus.Pending => ApiTaskToolExecutionStatus.Pending,
            Agent.Framework.TaskTool.TaskToolExecutionStatus.Running => ApiTaskToolExecutionStatus.Running,
            Agent.Framework.TaskTool.TaskToolExecutionStatus.Completed => ApiTaskToolExecutionStatus.Completed,
            Agent.Framework.TaskTool.TaskToolExecutionStatus.Failed => ApiTaskToolExecutionStatus.Failed,
            Agent.Framework.TaskTool.TaskToolExecutionStatus.Cancelled => ApiTaskToolExecutionStatus.Cancelled,
            _ => ApiTaskToolExecutionStatus.Running
        };
    }

    private async Task OnTaskToolExecutionStartAsync(FrameworkTaskToolExecution execution)
    {
        var data = new
        {
            executionId = execution.ExecutionId,
            subAgentType = execution.SubAgentType.ToString(),
            description = execution.Description,
            prompt = execution.Prompt,
            status = execution.Status.ToString(),
            startedAt = execution.StartedAt
        };

        var json = JsonSerializer.Serialize(data);
        await _outboundCommunicationService.StreamTaskToolExecutionUpdateAsync(
            _threadId,
            json,
            StreamMessageType.TaskToolExecutionStart);
    }

    private async Task OnTaskToolExecutionEndAsync(FrameworkTaskToolExecution execution)
    {
        var data = new
        {
            executionId = execution.ExecutionId,
            subAgentType = execution.SubAgentType.ToString(),
            description = execution.Description,
            status = execution.Status.ToString(),
            startedAt = execution.StartedAt,
            completedAt = execution.CompletedAt,
            error = execution.Error
        };

        var json = JsonSerializer.Serialize(data);
        await _outboundCommunicationService.StreamTaskToolExecutionUpdateAsync(
            _threadId,
            json,
            StreamMessageType.TaskToolExecutionEnd);
    }

    private async Task OnTaskToolInvocationStartAsync(string executionId, string toolName, string? description)
    {
        var data = new
        {
            executionId,
            toolName,
            description,
            status = "Running",
            startedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(data);
        await _outboundCommunicationService.StreamTaskToolExecutionUpdateAsync(
            _threadId,
            json,
            StreamMessageType.TaskToolInvocationStart);
    }

    private async Task OnTaskToolInvocationEndAsync(string executionId, string toolName, bool success, string? output)
    {
        var data = new
        {
            executionId,
            toolName,
            status = success ? "Completed" : "Failed",
            completedAt = DateTime.UtcNow,
            output
        };

        var json = JsonSerializer.Serialize(data);
        await _outboundCommunicationService.StreamTaskToolExecutionUpdateAsync(
            _threadId,
            json,
            StreamMessageType.TaskToolInvocationEnd);
    }
}
