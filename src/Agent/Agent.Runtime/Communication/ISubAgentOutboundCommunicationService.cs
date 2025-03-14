using Microsoft.Extensions.AI;

namespace Agent.Runtime.Communication;

/// <summary>
/// Interface for SubAgents to communicate outward to threads.
/// Consumed by orchestrations/activities to send messages back to users.
/// </summary>
public interface ISubAgentOutboundCommunicationService
{
    /// <summary>
    /// Updates a thread with a message from an agent
    /// </summary>
    Task UpdateThreadWithAgentMessageAsync(string threadId, string agentId, ChatMessage message);
    /// <summary>
    /// Notifies about agent task completion
    /// </summary>
    Task NotifyCompletionAsync(string threadId, string agentId, string status, string? summary = null);
}