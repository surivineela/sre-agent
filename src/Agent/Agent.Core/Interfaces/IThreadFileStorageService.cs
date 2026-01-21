// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Interfaces;

/// <summary>
/// Interface for thread file storage service with business logic.
/// Handles thread file uploads/downloads, tool output storage, and local caching.
/// Remote storage is optional; works in local-only mode when not configured.
/// </summary>
public interface IThreadFileStorageService
{
    /// <summary>
    /// Uploads a file for a specific thread with local backup
    /// </summary>
    /// <param name="threadId">The thread ID to store the file under</param>
    /// <param name="fileName">The name of the file to store</param>
    /// <param name="content">The file content as a byte array</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The file key that can be used to retrieve the file</returns>
    Task<string> UploadThreadFileAsync(
        Guid threadId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from storage for a specific thread.
    /// Uses local-first approach: checks local cache first, then remote storage if available.
    /// </summary>
    /// <param name="threadId">The thread ID the file belongs to</param>
    /// <param name="fileName">The name of the file to download</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The local file path if the file was found and downloaded, null if the file does not exist</returns>
    Task<string?> DownloadThreadFileAsync(
        Guid threadId,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a tool output with auto-generated filename and local backup
    /// </summary>
    /// <param name="threadId">The thread ID associated with this output</param>
    /// <param name="toolName">The name of the tool that produced this output</param>
    /// <param name="content">The full content to store</param>
    /// <param name="extension">The file extension for the output (e.g., json, yaml, txt)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The file key for the saved output</returns>
    Task<string> SaveToolOutputAsync(
        Guid threadId,
        string toolName,
        string content,
        string extension,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tool output file, ensuring it exists locally.
    /// Downloads from remote storage if not available locally and remote is configured.
    /// </summary>
    /// <param name="fileKey">The file key to retrieve</param>
    /// <param name="threadId">Optional thread ID for additional context (used for thread file lookups)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The local file path if the file exists, null otherwise</returns>
    Task<string?> GetToolOutputAsync(string fileKey, string? threadId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up all files (both local and remote) associated with a specific thread
    /// </summary>
    /// <param name="threadId">The thread ID to clean up files for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of files deleted</returns>
    Task<int> CleanupThreadFilesAsync(
        Guid threadId,
        CancellationToken cancellationToken = default);
}
