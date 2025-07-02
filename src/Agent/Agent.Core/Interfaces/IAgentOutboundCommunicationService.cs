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
    Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message, Guid? messageId = null);

    /// <summary>
    /// Updates a thread with a message from an agent
    /// </summary>
    Task UpdateThreadWithAgentMessageAsync(AgentContext context, ChatMessage message, Guid? messageId = null);

    /// <summary>
    /// Notifies about agent task completion
    /// </summary>
    Task NotifyCompletionAsync(string threadId, string orchestrationInstanceId, string status, string? summary = null);

    /// <summary>
    /// Notifies about agent task completion
    /// </summary>
    Task NotifyCompletionAsync(AgentContext context, string subAgentIdentifier, string status, string? summary = null);

    /// <summary>
    /// Notifies a generic agent message to stream, does not save context to thread
    /// </summary>
    Task NotifyGenericAgentMessage(Guid threadId, Message message, StreamMessageType? type);

    Task PostActivity(string threadId, Microsoft.Bot.Schema.Activity activity, string messageId = "");

    Task<Guid> AppendAgentImageMessage(Guid threadId, string message);
    Task<Guid> AppendAgentApprovalMessage(Guid threadId, Approval approval);

    /// <summary>
    /// Streams a message directly to the reasoning loop, bypassing normal tool call flow
    /// </summary>
    Task AppendAgentStreamMessage(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, CancellationToken cancellationToken = default);

    Task AppendAgentToolCallMessage(Guid threadId, AIFunction aiTool, Guid? messageId = null, CancellationToken cancellationToken = default);
    Task AppendAgentManualToolCallMessage(Guid threadId, List<ManualToolCall>? manualToolCalls, Guid? messageId = null, CancellationToken cancellationToken = default);
    Task AppendAgentToolCallResult(Guid threadId, FunctionResultContent result, Guid? messageId = null, CancellationToken cancellationToken = default);
    Task AppendAgentManualToolCallResult(Guid threadId, List<ManualToolCallResult>? manualToolCallResults, Guid? messageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a message to the user stream for a specific thread.
    /// </summary>
    Task AppendUserStreamMessage(Guid threadId, string displayName, string message, Guid messageId, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals that signal processing is complete for a given message on a specific thread.
    /// Sends ChatFinishReason.Stop command back to the user
    /// </summary>
    Task SignalProcessingComplete(Guid threadId, Guid? messageId = null, CancellationToken cancellationToken = default);
}
