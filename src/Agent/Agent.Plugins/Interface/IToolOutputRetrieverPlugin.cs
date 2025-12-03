// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;

namespace Agent.Plugins.Interface;

/// <summary>
/// Interface for accessing and manipulating stored tool outputs
/// </summary>
public interface IToolOutputRetrieverPlugin
{
    /// <summary>
    /// Accesses large stored tool outputs by fileKey with various operations
    /// </summary>
    /// <param name="options">Options for the operation</param>
    /// <returns>Result based on the operation performed</returns>
    Task<string> RetrieveToolOutputAsync(ToolOutputRetrieverOptions options);
}
