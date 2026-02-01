// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.Reasoning;
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

        runHooks.TaskToolInvocationEnd += async (context, agent, executionId, toolName, success) =>
        {
            await OnTaskToolInvocationEndAsync(executionId, toolName, success);
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

    private async Task OnTaskToolInvocationEndAsync(string executionId, string toolName, bool success)
    {
        var data = new
        {
            executionId,
            toolName,
            status = success ? "Completed" : "Failed",
            completedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(data);
        await _outboundCommunicationService.StreamTaskToolExecutionUpdateAsync(
            _threadId,
            json,
            StreamMessageType.TaskToolInvocationEnd);
    }
}
