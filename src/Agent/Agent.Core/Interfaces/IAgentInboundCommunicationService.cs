// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Core.Interfaces;

public record IncidentDiscussion(
    string Id,
    string Message,
    string UserId,
    string UserDisplayName,
    DateTime CreatedTimestamp
);

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
        string incidentId = "",
        IncidentSource? incidentSource = null,
        bool isDailyReport = false,
        List<string>? AllowedTools = null,
        ThreadType threadType = ThreadType.Prod,
        string overrideAgentMode = "");

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
    /// Used for starting a thread on an incident
    /// </summary>
    Task<Models.Api.v1.Thread> CreateAndProcessIncidentThread(
        string title,
        string message,
        IncidentSource incidentSource,
        List<IncidentDiscussion> discussions, bool defaultHandler = true);

    Task AddNewDiscussionsToIncidentThread(
        Guid incidentThreadId,
        List<IncidentDiscussion> discussions);


    /// <summary>
    /// Processes a thread that has been created for an alert, and starts the orchestration
    /// </summary>
    Task ProcessAlertMessageAsync(ThreadMessage message, bool defaultHandler = true);

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

    Task<InboundServiceResponse> ProcessIncidentMessageAsync(ThreadMessage message, bool defaultHandler = true);

    /// <summary>
    /// Processes a user's feedback on a message, which can be positive or negative.
    /// </summary>
    /// <param name="messageFeedback"></param>
    /// <returns></returns>
    Task<MessageFeedback> ProcessFeedbackAsync(ThreadMessageFeedback messageFeedback);
}
