// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Delegate for saving tool output to storage.
/// </summary>
/// <param name="threadId">The thread ID</param>
/// <param name="toolName">The tool name</param>
/// <param name="content">The content to save</param>
/// <param name="contentType">The content type (e.g., "json", "txt")</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>The file key for the saved content</returns>
public delegate Task<string> SaveToolOutputDelegate(
    Guid threadId,
    string toolName,
    string content,
    string contentType,
    CancellationToken cancellationToken);

/// <summary>
/// Context provided to processors during output processing.
/// </summary>
public class ToolOutputProcessorContext
{
    /// <summary>
    /// The thread ID for this execution.
    /// </summary>
    public required Guid ThreadId { get; init; }

    /// <summary>
    /// The name of the tool that produced the output.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Delegate for saving output to storage.
    /// </summary>
    public required SaveToolOutputDelegate SaveOutput { get; init; }
}

/// <summary>
/// Defines a processor for handling tool output of a specific type.
/// Processors can transform, format, or enrich tool outputs as needed.
/// </summary>
public interface IToolOutputProcessor
{
    /// <summary>
    /// Processes the tool output object and returns the processed result.
    /// </summary>
    /// <param name="output">The tool output object to process</param>
    /// <param name="context">Context with thread info and storage capabilities</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processed output object</returns>
    Task<object?> ProcessAsync(
        object? output,
        ToolOutputProcessorContext context,
        CancellationToken cancellationToken = default);
}
