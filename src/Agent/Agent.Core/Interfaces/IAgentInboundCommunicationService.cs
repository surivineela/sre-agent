// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;

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
    Task<(Models.Api.v1.Thread, AgentContext)> CreateAgentThread(
        string title, 
        string message, 
        AgentTypeEnum agentTypeEnum,
        ThreadSource source = ThreadSource.Conversation,
        string incidentId = "");

    /// <summary>
    /// Used for alert scenarios, where we need to create a new thread for an agent to work from,
    /// and trigger teams to notify the engineer as well
    /// </summary>
    Task<Models.Api.v1.Thread> CreateAlertThreadWithTeams(
        string title, 
        string message, 
        AgentTypeEnum agentTypeEnum,
        ThreadSource source = ThreadSource.Alert);

    /// <summary>
    /// Processes a thread that has been created for an alert, and starts the orchestration
    /// </summary>
    Task ProcessAlertMessageAsync(ThreadMessage message);

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

    /// <summary>
    /// Processes a user's feedback on a message, which can be positive or negative.
    /// </summary>
    /// <param name="messageFeedback"></param>
    /// <returns></returns>
    Task<MessageFeedback> ProcessFeedbackAsync(ThreadMessageFeedback messageFeedback);
}
