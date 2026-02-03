// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Interfaces;

/// <summary>
/// Interface for agent file storage service with business logic.
/// Handles thread file uploads/downloads, tool output storage, memories sync, and local caching.
/// Remote storage is optional; works in local-only mode when not configured.
/// </summary>
public interface IAgentFileStorageService
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
    /// <param name="callId">The unique call ID for this tool invocation</param>
    /// <param name="content">The full content to store</param>
    /// <param name="extension">The file extension for the output (e.g., json, yaml, txt)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The file key for the saved output</returns>
    Task<string> SaveToolOutputAsync(
        Guid threadId,
        string toolName,
        string callId,
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

    /// <summary>
    /// Uploads all files from the repository instructions folder.
    /// Scans: {MemoriesPath}/repoInstructions/{repoName}/
    /// </summary>
    /// <param name="repoName">The repository name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of files uploaded</returns>
    Task<int> UploadWorkspaceRepoInstructionsAsync(
        string repoName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads all files from the session insights folder for a thread.
    /// Scans: {MemoriesPath}/sessionInsights/{threadId}/
    /// </summary>
    /// <param name="threadId">The thread ID associated with the session</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of files uploaded</returns>
    Task<int> UploadWorkspaceSessionInsightsAsync(
        Guid threadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads all files from the synthesized knowledge folder.
    /// Scans: {MemoriesPath}/synthesizedKnowledge/
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of files uploaded</returns>
    Task<int> UploadWorkspaceSynthesizedKnowledgeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads all memory files from remote blob storage to the local memories folder.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of files downloaded</returns>
    Task<int> DownloadMemoryFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all files from the repository instructions folder (local and blob).
    /// Deletes: {MemoriesPath}/.github/{repoName}/ and blob memories/.github/{repoName}/
    /// </summary>
    /// <param name="repoName">The repository name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of files deleted</returns>
    Task<int> DeleteWorkspaceRepoInstructionsAsync(
        string repoName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all files from the session insights folder for a thread (local and blob).
    /// Deletes: {MemoriesPath}/sessionInsights/{threadId}/ and blob memories/sessionInsights/{threadId}/
    /// If threadId is null, deletes all session insights.
    /// </summary>
    /// <param name="threadId">The thread ID, or null to delete all session insights</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of files deleted</returns>
    Task<int> DeleteWorkspaceSessionInsightsAsync(
        Guid? threadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all files from the synthesized knowledge folder (local and blob).
    /// Deletes: {MemoriesPath}/synthesizedKnowledge/ and blob memories/synthesizedKnowledge/
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of files deleted</returns>
    Task<int> DeleteWorkspaceSynthesizedKnowledgeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads all files from the repository instructions folder from blob storage to local.
    /// Downloads: blob memories/.github/{repoName}/ to {MemoriesPath}/.github/{repoName}/
    /// </summary>
    /// <param name="repoName">The repository name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of files downloaded</returns>
    Task<int> DownloadWorkspaceRepoInstructionsAsync(
        string repoName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads all files from the synthesized knowledge folder from blob storage to local.
    /// Downloads: blob memories/synthesizedKnowledge/ to {MemoriesPath}/synthesizedKnowledge/
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of files downloaded</returns>
    Task<int> DownloadWorkspaceSynthesizedKnowledgeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads session insights from blob storage and returns as a tar.gz stream.
    /// Does NOT save to local storage - for direct streaming to client.
    /// </summary>
    /// <param name="threadId">The thread ID, or null for all session insights</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A MemoryStream containing the tar.gz archive, or null if no files found</returns>
    Task<MemoryStream?> DownloadWorkspaceSessionInsightsToStreamAsync(
        Guid? threadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads all memory files from the local memories folder to remote blob storage.
    /// This includes repo instructions, session insights, and synthesized knowledge.
    /// Only uploads files that have changed since last upload.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The total number of files uploaded</returns>
    Task<int> UploadAllMemoriesAsync(CancellationToken cancellationToken = default);
}
