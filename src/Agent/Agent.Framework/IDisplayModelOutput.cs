// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Interface for displaying model output content
/// </summary>
public interface IDisplayModelOutput
{
    /// <summary>
    /// Called when content should be displayed to the user
    /// </summary>
    /// <param name="content">The content to display</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task OnDisplay(string content);

    /// <summary>
    /// Called when streaming content ended
    /// </summary>
    /// <param name="content"></param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task OnComplete(string content = "");

    /// <summary>
    /// Called when streaming content is incomplete (partial response)
    /// </summary>
    Task OnIncomplete();
}
