// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// This class holds thread local variables used for calling tools in the framework.
/// They must be thread local because agent executions can happen in different threads.
/// </summary>
public static class ToolStatic
{
    /// <summary>
    /// Holds the function call ID for the current tool execution. Set by Runner during tool processing.
    /// AsyncLocal because we want to keep the call ID for streaming in the current async context.
    /// </summary>
    public static readonly AsyncLocal<string?> AsyncLocalFunctionCallId = new();

    /// <summary>
    /// Holds the tool call message ID for the current tool execution. Set by OnToolStart hook for auto tools.
    /// AsyncLocal because we want to keep the message ID for streaming in the current async context.
    /// </summary>
    public static readonly AsyncLocal<Guid?> AsyncLocalToolCallMessageId = new();
} 