// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Logging;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Data.Storage;

/// <summary>
/// Azure Blob Storage implementation for remote file storage operations.
/// This class contains only Azure SDK operations without any business logic.
/// </summary>
public class AzureBlobRemoteFileStorage : IRemoteFileStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureBlobRemoteFileStorage> _logger;
    private readonly HashSet<string> _ensuredContainers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _containerLock = new();

    public AzureBlobRemoteFileStorage(
        IOptions<ToolOutputSettings> settings,
        IAuthenticationService authService,
        ILogger<AzureBlobRemoteFileStorage> logger)
    {
        var settingsValue = settings.Value;

        if (string.IsNullOrWhiteSpace(settingsValue.StorageAccountName))
        {
            throw new ArgumentException("Storage account name cannot be null or empty.", nameof(settings));
        }

        var tokenCredential = authService.GetToolOutputBlobStorageCredential();
        _blobServiceClient = new BlobServiceClient(
            new Uri($"https://{settingsValue.StorageAccountName}.{settingsValue.BlobStorageDomainSuffix}"),
            tokenCredential);

        _logger = logger;
    }

    /// <summary>
    /// Ensures the specified container exists, creating it if necessary.
    /// Uses internal caching to avoid redundant existence checks.
    /// </summary>
    private void EnsureContainerExists(string containerName)
    {
        var normalizedName = containerName.ToLowerInvariant();

        lock (_containerLock)
        {
            if (_ensuredContainers.Contains(normalizedName))
            {
                return;
            }

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(normalizedName);
                containerClient.CreateIfNotExists();
                _ensuredContainers.Add(normalizedName);
                _logger.LogInternalInformation("Ensured blob container exists: {ContainerName}", normalizedName);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to ensure blob container exists: {ContainerName}", normalizedName);
                throw;
            }
        }
    }

    /// <inheritdoc />
    public async Task UploadAsync(
        string containerName,
        string blobPath,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (content == null || content.Length == 0)
        {
            throw new ArgumentException("Content cannot be null or empty.", nameof(content));
        }

        if (string.IsNullOrWhiteSpace(blobPath))
        {
            throw new ArgumentException("Blob path cannot be null or empty.", nameof(blobPath));
        }

        EnsureContainerExists(containerName);

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLowerInvariant());
            var blobClient = containerClient.GetBlobClient(blobPath);

            using var stream = new MemoryStream(content);
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);

            _logger.LogInternalInformation(
                "Successfully uploaded to blob storage: {Container}/{BlobPath}, Size: {Size} bytes",
                containerName, blobPath, content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to upload to blob storage: {Container}/{BlobPath}", containerName, blobPath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UploadAsync(
        string containerName,
        string blobPath,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException("Content cannot be null or empty.", nameof(content));
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        await UploadAsync(containerName, blobPath, bytes, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DownloadAsync(
        string containerName,
        string blobPath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            throw new ArgumentException("Blob path cannot be null or empty.", nameof(blobPath));
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Destination path cannot be null or empty.", nameof(destinationPath));
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLowerInvariant());
            var blobClient = containerClient.GetBlobClient(blobPath);

            // Check if blob exists
            var exists = await blobClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                _logger.LogInternalWarning("Blob not found in storage: {Container}/{BlobPath}", containerName, blobPath);
                return false;
            }

            // Ensure destination directory exists
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            // Download directly to file
            await blobClient.DownloadToAsync(destinationPath, cancellationToken);

            _logger.LogInternalInformation(
                "Successfully downloaded from blob storage: {Container}/{BlobPath} to {DestinationPath}",
                containerName, blobPath, destinationPath);

            return true;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInternalWarning("Blob not found in storage: {Container}/{BlobPath}", containerName, blobPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to download from blob storage: {Container}/{BlobPath}", containerName, blobPath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteByPrefixAsync(
        string containerName,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        var deletedCount = 0;

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLowerInvariant());

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                try
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                    deletedCount++;
                    _logger.LogInternalDebug("Deleted blob: {Container}/{BlobName}", containerName, blobItem.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Failed to delete blob: {Container}/{BlobName}", containerName, blobItem.Name);
                }
            }

            _logger.LogInternalInformation("Deleted {DeletedCount} blob(s) with prefix {Prefix} from {Container}", deletedCount, prefix, containerName);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to delete blobs with prefix {Prefix} from {Container}", prefix, containerName);
        }

        return deletedCount;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        string containerName,
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return false;
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLowerInvariant());
            var blobClient = containerClient.GetBlobClient(blobPath);

            var exists = await blobClient.ExistsAsync(cancellationToken);
            return exists.Value;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to check blob existence: {Container}/{BlobPath}", containerName, blobPath);
            return false;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ListBlobsAsync(
        string containerName,
        string? prefix = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLowerInvariant());

        // Check if container exists
        try
        {
            var exists = await containerClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                _logger.LogInternalDebug("Container does not exist: {ContainerName}", containerName);
                yield break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to check container existence: {ContainerName}", containerName);
            yield break;
        }

        await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            yield return blobItem.Name;
        }
    }
}
