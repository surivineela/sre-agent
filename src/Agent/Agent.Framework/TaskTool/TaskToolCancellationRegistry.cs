// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;

namespace Agent.Framework.TaskTool;

/// <summary>
/// Registry for managing cancellation of Task tool executions.
/// Provides thread-safe tracking of running task executions and their cancellation tokens.
/// </summary>
public static class TaskToolCancellationRegistry
{
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _executionTokens = new();

    /// <summary>
    /// Registers a new task execution and returns a linked cancellation token.
    /// The returned token will be cancelled when either the parent token is cancelled
    /// or when CancelExecution is called for this execution ID.
    /// </summary>
    /// <param name="executionId">Unique identifier for the task execution</param>
    /// <param name="parentToken">The parent cancellation token from the calling context</param>
    /// <returns>A cancellation token that can be used by the task execution</returns>
    public static CancellationToken RegisterExecution(string executionId, CancellationToken parentToken)
    {
        var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        _executionTokens[executionId] = linkedSource;
        return linkedSource.Token;
    }

    /// <summary>
    /// Cancels a running task execution by its ID.
    /// </summary>
    /// <param name="executionId">The ID of the execution to cancel</param>
    /// <returns>True if the execution was found and cancelled, false if not found</returns>
    public static bool CancelExecution(string executionId)
    {
        if (_executionTokens.TryRemove(executionId, out var tokenSource))
        {
            try
            {
                tokenSource.Cancel();
                return true;
            }
            finally
            {
                tokenSource.Dispose();
            }
        }
        return false;
    }

    /// <summary>
    /// Unregisters a task execution when it completes (successfully or with error).
    /// Should be called in a finally block to ensure cleanup.
    /// </summary>
    /// <param name="executionId">The ID of the execution to unregister</param>
    public static void UnregisterExecution(string executionId)
    {
        if (_executionTokens.TryRemove(executionId, out var tokenSource))
        {
            tokenSource.Dispose();
        }
    }

    /// <summary>
    /// Checks if an execution is currently registered (still running).
    /// </summary>
    /// <param name="executionId">The ID of the execution to check</param>
    /// <returns>True if the execution is registered, false otherwise</returns>
    public static bool IsExecutionRunning(string executionId)
    {
        return _executionTokens.ContainsKey(executionId);
    }

    /// <summary>
    /// Gets the count of currently running task executions.
    /// Useful for diagnostics and monitoring.
    /// </summary>
    public static int RunningExecutionCount => _executionTokens.Count;
}
