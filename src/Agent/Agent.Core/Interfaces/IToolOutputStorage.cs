// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Interfaces;

/// <summary>
/// Interface for storing and retrieving large tool outputs
/// </summary>
public interface IToolOutputStorage
{
    /// <summary>
    /// Saves a tool output to storage
    /// </summary>
    /// <param name="threadId">The thread ID associated with this output</param>
    /// <param name="toolName">The name of the tool that produced this output</param>
    /// <param name="content">The full content to store</param>
    /// <param name="fileExtension">The file extension for the output (e.g., .json, .yaml, .txt)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The file key for the saved output</returns>
    Task<string> SaveAsync(
        Guid threadId,
        string toolName,
        string content,
        string fileExtension,
        CancellationToken cancellationToken = default);

    /// Verifies that a file exists in storage and returns its local file path
    /// </summary>
    /// <param name="fileKey">The file key to check</param>
    /// <returns>The local file path if the file exists, null otherwise</returns>
    string? EnsureFileExist(string fileKey);

    /// <summary>
    /// Deletes all files associated with a specific thread
    /// </summary>
    /// <param name="threadId">The thread ID to delete files for</param>
    /// <returns>Number of files deleted</returns>
    int CleanupFilesByThreadId(Guid threadId);
}
