// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Interfaces;

/// <summary>
/// Interface for pure remote file storage operations (Azure Blob Storage).
/// This interface contains only Azure SDK operations without any business logic.
/// </summary>
public interface IRemoteFileStorage
{
    /// <summary>
    /// Uploads binary content to remote storage
    /// </summary>
    /// <param name="containerName">The container name to upload to</param>
    /// <param name="blobPath">The blob path within the container</param>
    /// <param name="content">The binary content to upload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UploadAsync(
        string containerName,
        string blobPath,
        byte[] content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads text content to remote storage
    /// </summary>
    /// <param name="containerName">The container name to upload to</param>
    /// <param name="blobPath">The blob path within the container</param>
    /// <param name="content">The text content to upload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UploadAsync(
        string containerName,
        string blobPath,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a blob from remote storage to a local file path
    /// </summary>
    /// <param name="containerName">The container name to download from</param>
    /// <param name="blobPath">The blob path within the container</param>
    /// <param name="destinationPath">The local file path to save the downloaded file to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the file was downloaded successfully, false if the blob does not exist</returns>
    Task<bool> DownloadAsync(
        string containerName,
        string blobPath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all blobs matching a prefix in the specified container
    /// </summary>
    /// <param name="containerName">The container name to delete from</param>
    /// <param name="prefix">The prefix to match blobs against</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of blobs deleted</returns>
    Task<int> DeleteByPrefixAsync(
        string containerName,
        string prefix,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a blob exists in remote storage
    /// </summary>
    /// <param name="containerName">The container name to check in</param>
    /// <param name="blobPath">The blob path within the container</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the blob exists, false otherwise</returns>
    Task<bool> ExistsAsync(
        string containerName,
        string blobPath,
        CancellationToken cancellationToken = default);
}
