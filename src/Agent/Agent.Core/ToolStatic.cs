// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Agent.Core;

/// <summary>
/// This class holds thread local variables used for calling tools.
/// They must be thread local because durable tasks are executed in different threads.
/// </summary>
public static class ToolStatic
{
    /// <summary>
    /// Holds the thread ID for the current Durable task. Set by GenericExecuteActionActivity
    /// AsyncLocal because we want to keep the conversation thread ID for the current async context.
    /// </summary>
    public static readonly AsyncLocal<Guid> AsyncLocalThreadId = new ();

    /// <summary>
    /// Holds the approval ID for the current Durable task. Set by GenericExecuteActionActivity
    /// AsyncLocal because we want to keep the conversation thread ID for the current async context.
    /// </summary>
    public static readonly AsyncLocal<Guid?> AsyncLocalApprovalId = new ();
}
