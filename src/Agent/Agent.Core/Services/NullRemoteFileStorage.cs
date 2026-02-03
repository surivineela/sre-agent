// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;

namespace Agent.Core.Services;

/// <summary>
/// A null object implementation of IRemoteFileStorage for when remote storage is not configured.
/// All operations succeed silently without actually storing or retrieving any data.
/// </summary>
public class NullRemoteFileStorage : IRemoteFileStorage
{
    /// <inheritdoc />
    public Task UploadAsync(
        string containerName,
        string blobPath,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        // No-op: silently succeed without storing
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UploadAsync(
        string containerName,
        string blobPath,
        string content,
        CancellationToken cancellationToken = default)
    {
        // No-op: silently succeed without storing
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> DownloadAsync(
        string containerName,
        string blobPath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        // No-op: file doesn't exist in remote storage
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<int> DeleteByPrefixAsync(
        string containerName,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        // No-op: no files to delete
        return Task.FromResult(0);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        string containerName,
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        // No-op: file doesn't exist in remote storage
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListBlobsAsync(
        string containerName,
        string? prefix = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // No-op: no blobs to list
        await Task.CompletedTask;
        yield break;
    }
}
