// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------



namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Represents the context for an agent thread, we should add READ-ONLY information here.
/// Use ThreadService to operate on the thread like adding or getting messages.
/// </summary>
public class ThreadContext
{

    /// <summary>
    /// Unique identifier for the thread.
    /// </summary>
    public readonly Guid ThreadId;

    // TODO: add other read-only properties like OutboundClientConfiguration, ThreadType, etc. if needed.

    /// <summary>
    /// Initializes a new instance of the ThreadContext class with the specified thread ID and messages.
    /// </summary>
    /// <param name="threadId">The unique identifier for the thread.</param>
    public ThreadContext(Guid threadId)
    {
        ThreadId = threadId;
    }

}

