using Microsoft.Extensions.AI;

namespace Agent.Runtime.Communication;

/// <summary>
/// Interface for processing messages from users to agents/orchestrations
/// </summary>
public interface IAgentInboundCommunicationService
{
    /// <summary>
    /// Processes a user message, determining if it should be routed to an existing orchestration 
    /// or handled by the meta-agent to potentially start a new orchestration
    /// </summary>
    Task<InboundServiceResponse> ProcessUserMessageAsync(ThreadMessage message);
}