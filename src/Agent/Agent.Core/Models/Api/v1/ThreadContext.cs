// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Microsoft.SemanticKernel;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;


namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Represents the context for an agent thread, we should add READ-ONLY information here.
/// Use ThreadService to operate on the thread like adding or getting messages.
/// </summary>
public class ThreadContext
{
    private const int MaxMessagesInContext = 15;

    /// <summary>
    /// Unique identifier for the thread.
    /// </summary>
    public readonly Guid ThreadId;

    /// <summary>
    /// The type of agent that is last processing the thread.
    /// </summary>
    public readonly AgentTypeEnum AgentTypeEnum;

    /// <summary>
    /// A list of the most recent messages to provide the Agent with the relevant context.
    /// </summary>
    public readonly Queue<Message> RecentMessages;

    public bool IsThreadActive { get; set; }

    public OutboundConfiguration OutboundConfiguration { get; set; } = new OutboundConfiguration();

    public OrchestrationState? OrchestrationState { get; set; } = new OrchestrationState();

    // TODO(jianbo): use ThreadService only to add/read properties, in case we forget to sync in cosmosdb.
    public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

    // TODO: add other read-only properties like OutboundClientConfiguration, ThreadType, etc. if needed.

    /// <summary>
    /// Initializes a new instance of the ThreadContext class with the specified thread ID and messages.
    /// </summary>
    /// <param name="threadId">The unique identifier for the thread.</param>
    public ThreadContext(Guid threadId, AgentTypeEnum agentTypeEnum, bool isThreadActive = true, Queue<Message>? recentMessages = null, OrchestrationState? orchestrationState = null, OutboundConfiguration? outboundConfiguration = null)
    {
        ThreadId = threadId;
        AgentTypeEnum = agentTypeEnum;
        RecentMessages = recentMessages ?? new Queue<Message>();
        OutboundConfiguration = outboundConfiguration ?? new OutboundConfiguration();
        IsThreadActive = isThreadActive;
        OrchestrationState = orchestrationState ?? new OrchestrationState();
    }


    public void AddMessage(Message message)
    {
        if (RecentMessages.Count >= MaxMessagesInContext)
        {
            RecentMessages.Dequeue();
        }

        RecentMessages.Enqueue(message);
    }

    public void ResumeThreadContext()
    {
        IsThreadActive = true;
    }

    public void ConcludeThreadContext()
    {
        IsThreadActive = false;
    }
}

public class OutboundConfiguration
{
    public Teams? Teams;
}

public class Teams
{
    public bool? Enabled = true;
}


public enum ReasoningState
{
    NotStarted, // this is not expected to be seen by customer
    OrchestrationInitialized,
    Waiting,
    PlanningNextAction,
    RunningFunctionCall,
    OrchestrationCompleted,
    Error
}


public class OrchestrationState
{
    public string OrchestrationInstanceId { get; set; } = string.Empty;
    public int StepCounter { get; set; }
    public ReasoningState ReasoningState { get; set; }
    public string StateMessage { get; set; } = string.Empty;
    public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
}