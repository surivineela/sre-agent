// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

/// <summary>
/// Interface for handling streaming content from chat responses
/// </summary>
public interface IStreamContentHandler
{
    /// <summary>
    /// Number of characters to accumulate in the content cache before displaying
    /// </summary>
    const int ContentCacheThreshold = 50;

    /// <summary>
    /// Appends streaming content as it arrives
    /// </summary>
    /// <param name="content">The content to append</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task AppendAsync(string content);

    /// <summary>
    /// Called when streaming is complete
    /// </summary>
    /// <param name="finishReason">The reason why the streaming completed</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task CompleteAsync(ChatFinishReason? finishReason);

    /// <summary>
    /// Called when streaming is incomplete (partial response)
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task IncompleteAsync();
}
