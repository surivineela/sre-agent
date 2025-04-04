// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.SemanticKernel;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;


namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Represents the context for an agent thread, we should add READ-ONLY information here.
/// Use ThreadService to operate on the thread like adding or getting messages.
/// </summary>
public class ThreadContext
{
    private const int MaxMessagesInContext = 5;

    /// <summary>
    /// Unique identifier for the thread.
    /// </summary>
    public readonly Guid ThreadId;

    /// <summary>
    /// A list of the most recent messages to provide the Agent with the relevant context.
    /// </summary>
    public readonly Queue<Message> RecentMessages;

    // TODO: add other read-only properties like OutboundClientConfiguration, ThreadType, etc. if needed.

    /// <summary>
    /// Initializes a new instance of the ThreadContext class with the specified thread ID and messages.
    /// </summary>
    /// <param name="threadId">The unique identifier for the thread.</param>
    public ThreadContext(Guid threadId, Queue<Message>? recentMessages = null)
    {
        ThreadId = threadId;
        RecentMessages = new Queue<Message>();
    }


    public void AddMessage(Message message)
    {
        if (RecentMessages.Count >= MaxMessagesInContext)
        {
            RecentMessages.Dequeue();
        }

        RecentMessages.Enqueue(message);
    }
}

