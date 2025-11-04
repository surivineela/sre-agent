// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Interface for handling streaming content from chat responses
/// </summary>
public interface IStreamContentHandler
{
    /// <summary>
    /// Appends streaming content as it arrives
    /// </summary>
    /// <param name="content">The content to append</param>
    void Append(string content);

    /// <summary>
    /// Called when streaming is complete
    /// </summary>
    void Complete();
}
