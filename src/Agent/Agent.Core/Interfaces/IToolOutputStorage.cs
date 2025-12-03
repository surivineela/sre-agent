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
    /// Verifies that a file exists in storage and returns its local file path
    /// </summary>
    /// <param name="fileId">The file ID to check</param>
    /// <returns>The local file path if the file exists, null otherwise</returns>
    string? EnsureFileExist(string fileId);

    /// <summary>
    /// Deletes files older than the specified retention period
    /// </summary>
    /// <param name="retentionDays">Number of days to retain files</param>
    /// <returns>Number of files deleted</returns>
    int CleanupOldFiles(int retentionDays);
}
