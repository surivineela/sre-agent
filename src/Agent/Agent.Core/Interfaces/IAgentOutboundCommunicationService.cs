// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Core.Interfaces;

/// <summary>
/// Interface for SubAgents to communicate outward to threads.
/// Consumed by orchestrations/activities to send messages back to users.
/// </summary>
public interface IAgentOutboundCommunicationService
{
    /// <summary>
    /// Updates a thread with a message from an agent
    /// </summary>
    Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message, Guid? messageId = null, StreamMessageType? type = null);

    /// <summary>
    /// Updates a thread with a message from an agent with agent context
    /// </summary>
    Task UpdateThreadWithAgentMessageAsync(AgentContext context, ChatMessage message, Guid? messageId = null);

    /// <summary>
    /// Updates a thread with a message from an agent and contains agent task info
    /// </summary>
    Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message, AgentTaskInfo? agentTaskInfo, Guid? messageId = null, StreamMessageType? type = null);

    /// <summary>
    /// Updates a thread with a message from an agent and contains todo info
    /// </summary>
    Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message, AgentTaskInfo? agentTaskInfo, TodoInfo? todoInfo, Guid? messageId = null, StreamMessageType? type = null);

    /// <summary>
    /// Notifies about agent task completion
    /// </summary>
    Task NotifyCompletionAsync(string threadId, string orchestrationInstanceId, string status, string? summary = null);

    /// <summary>
    /// Notifies about agent task completion
    /// </summary>
    Task NotifyCompletionAsync(AgentContext context, string subAgentIdentifier, string status, string? summary = null);

    /// <summary>
    /// Notifies about an action
    /// </summary>
    Task NotifyActionAsync(Guid threadId, Models.Api.v1.Action action);

    /// <summary>
    /// Notifies a generic agent message to stream, does not save context to thread
    /// </summary>
    Task NotifyGenericAgentMessage(Guid threadId, Message message, StreamMessageType? type);

    /// <summary>
    /// Notifies about an update to a thread, such as creation or modification
    /// </summary>
    Task NotifyThreadEvent(Guid threadId, Core.Models.Api.v1.Thread thread);

    /// <summary>
    /// Notifies about an update to an AzCli execution
    /// </summary>
    Task NotifyAzCliUpdate(Guid threadId, AzCliExecution execution, Guid messageId = default);

    /// <summary>
    /// Notifies about an update to an approval
    /// </summary>
    Task NotifyApprovalUpdate(Guid threadId, Approval approval, Guid messageId = default);

    /// <summary>
    /// Notifies about an update to a Kubectl execution
    /// </summary>
    Task NotifyKubectlUpdate(Guid threadId, KubectlExecution execution, Guid messageId = default);

    Task NotifyIncidentStatusMetrics(Guid threadId, IncidentStatusMetrics metrics, Guid? messageId = null);

    /// <summary>
    /// Notifies about an update to a PostgreSQL execution
    /// </summary>
    Task NotifyPsqlUpdate(Guid threadId, PsqlExecution execution, Guid messageId = default);

    Task PostActivity(string threadId, Microsoft.Bot.Schema.Activity activity, string messageId = "");

    Task<Guid> AppendAgentImageMessage(Guid threadId, string message, Guid messageId = default);
    Task<Guid> AppendAgentApprovalMessage(Guid threadId, Approval approval);
    Task<Guid> AppendAgentMemorySearchMessage(Guid threadId, MemorySearchResult memorySearchResult, Guid messageId = default);

    /// <summary>
    /// Streams a message directly to the reasoning loop, bypassing normal tool call flow
    /// </summary>
    Task AppendAgentStreamMessage(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a task update directly to clients
    /// </summary>
    Task AppendAgentTaskUpdate(Guid threadId, string taskData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default);

    Task AppendAgentToolCallMessage(Guid threadId, AIFunction aiTool, Guid? messageId = null, string? callId = null, CancellationToken cancellationToken = default);
    Task AppendAgentManualToolCallMessage(Guid threadId, List<ManualToolCall>? manualToolCalls, Guid? messageId = null, CancellationToken cancellationToken = default);
    Task AppendAgentToolCallResult(Guid threadId, FunctionResultContent result, Guid? messageId = null, CancellationToken cancellationToken = default);
    Task AppendAgentManualToolCallResult(Guid threadId, List<ManualToolCallResult>? manualToolCallResults, Guid? messageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a message to the user stream for a specific thread.
    /// </summary>
    Task AppendUserStreamMessage(Guid threadId, string displayName, string message, Guid messageId, string? userId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals that signal processing is complete for a given message on a specific thread.
    /// Sends ChatFinishReason.Stop command back to the user
    /// </summary>
    Task SignalProcessingComplete(Guid threadId, Guid? messageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles AzCli tool results specifically for agent tasks/deep investigation.
    /// Saves results to agent task object step and sends task updates instead of streaming to chat.
    /// </summary>
    Task HandleAgentTaskAzCliResult(Guid threadId, AzCliExecution execution);

    /// <summary>
    /// Handles Kusto tool results specifically for agent tasks/deep investigation.
    /// Saves results to agent task object step instead of streaming to chat.
    /// </summary>
    Task HandleAgentTaskKustoResult(Guid threadId, string chatMessageContent);

    /// <summary>
    /// Handles Memory tool results specifically for agent tasks/deep investigation.
    /// Saves results to agent task object step instead of streaming to chat.
    /// </summary>
    Task HandleAgentTaskMemoryResult(Guid threadId, string chatMessageContent);
}
