// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
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
    Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message);

    /// <summary>
    /// Updates a thread with a message from an agent
    /// </summary>
    Task UpdateThreadWithAgentMessageAsync(AgentContext context, ChatMessage message);

    /// <summary>
    /// Notifies about agent task completion
    /// </summary>
    Task NotifyCompletionAsync(string threadId, string orchestrationInstanceId, string status, string? summary = null);

    /// <summary>
    /// Notifies about agent task completion
    /// </summary>
    Task NotifyCompletionAsync(AgentContext context, string subAgentIdentifier, string status, string? summary = null);

    Task PostActivity(string threadId, Microsoft.Bot.Schema.Activity activity, string messageId = "");

    Task<Guid> AppendAgentImageMessage(Guid threadId, string message);
    Task<Guid> AppendAgentApprovalMessage(Guid threadId, Approval approval);
}
