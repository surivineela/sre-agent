// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Service for truncating large tool outputs and storing them in external storage
/// </summary>
public interface IToolOutputTruncationService
{
    /// <summary>
    /// Processes tool output, truncating and storing large outputs
    /// </summary>
    /// <param name="threadId">The thread ID for this execution</param>
    /// <param name="toolName">The name of the tool that produced this output</param>
    /// <param name="output">The original tool output (string or object that will be serialized to JSON)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The processed output - either the original if small enough,
    /// or a truncation message with file reference if large
    /// </returns>
    Task<object?> ProcessToolOutputAsync(
        Guid threadId,
        string toolName,
        object? output,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes tool output, truncating and storing large outputs (context-based overload)
    /// </summary>
    /// <typeparam name="TContext">The context type that contains a ThreadId property</typeparam>
    /// <param name="context">The context containing thread information</param>
    /// <param name="toolName">The name of the tool that produced this output</param>
    /// <param name="output">The original tool output (string or object that will be serialized to JSON)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The processed output - either the original if small enough,
    /// or a truncation message with file reference if large
    /// </returns>
    Task<object?> ProcessToolOutputAsync<TContext>(
        TContext? context,
        string toolName,
        object? output,
        CancellationToken cancellationToken = default) where TContext : class;
}
