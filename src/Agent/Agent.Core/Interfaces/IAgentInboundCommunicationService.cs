using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Core.Interfaces;

/// <summary>
/// Interface for processing messages from users to agents/orchestrations
/// </summary>
public interface IAgentInboundCommunicationService
{
    /// <summary>
    /// Used for any proactive scenario, where some code has run and determined that we need to create a new thread for an agent to work from
    /// </summary>
    /// <param name="title"></param>
    /// <returns></returns>
    Task<Models.Api.v1.Thread> CreateAgentThread(string title, string message);

    /// <summary>
    /// Appends a message from the agent to the specified thread
    /// </summary>
    /// <param name="threadId">The ID of the thread to append to</param>
    /// <param name="message">The message content</param>
    /// <returns>The ID of the newly created message</returns>
    Task<Guid> AppendAgentImageMessage(Guid threadId, string message);

    /// <summary>
    /// Processes a user message, determining if it should be routed to an existing orchestration 
    /// or handled by the meta-agent to potentially start a new orchestration
    /// </summary>
    Task<InboundServiceResponse> ProcessUserMessageAsync(ThreadMessage message);
}