// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Storage;

/// <summary>
/// Azure Blob Storage implementation for storing large tool outputs
/// </summary>
public class AzureBlobToolOutputStorage : IToolOutputStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureBlobToolOutputStorage> _logger;
    private readonly string _localCachePath;
    private readonly string _containerName;

    public AzureBlobToolOutputStorage(
        ToolOutputSettings toolOutputSettings,
        IAuthenticationService authService,
        ILogger<AzureBlobToolOutputStorage> logger,
        string baseStoragePath)
    {
        if (string.IsNullOrWhiteSpace(baseStoragePath))
        {
            throw new ArgumentException("Base storage path cannot be null or empty.", nameof(baseStoragePath));
        }

        var tokenCredential = authService.GetToolOutputBlobStorageCredential();
        _blobServiceClient = new BlobServiceClient(
            new Uri($"https://{toolOutputSettings.StorageAccountName}.{toolOutputSettings.BlobStorageDomainSuffix}"),
            tokenCredential);

        _logger = logger;
        _containerName = toolOutputSettings.BlobStorageContainerName.ToLowerInvariant();

        // Ensure blob container exists
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            containerClient.CreateIfNotExists();
            _logger.LogInternalInformation("Ensured blob container exists: {ContainerName}", _containerName);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to ensure blob container exists: {ContainerName}", _containerName);
            throw;
        }

        // Always append "ToolOutput" subfolder to the base path, same as LocalToolOutputStorage
        _localCachePath = Path.Combine(baseStoragePath, "ToolOutput");

        // Ensure local cache directory exists
        try
        {
            Directory.CreateDirectory(_localCachePath);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to create local cache directory at {CachePath}", _localCachePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> SaveAsync(
        Guid threadId,
        string toolName,
        string content,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var fileName = $"{threadId}-{timestamp}.{fileExtension}";
        var blobName = $"{_containerName}/{fileName}";

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);

            _logger.LogInternalInformation("Successfully saved tool output to blob storage: {BlobName}", blobName);

            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to save tool output to blob storage: {BlobName}", blobName);
            throw;
        }
    }

    /// <inheritdoc />
    public string? EnsureFileExist(string fileKey)
    {
        try
        {
            var localFilePath = Path.Combine(_localCachePath, fileKey);

            // If file already exists locally, return its path
            if (File.Exists(localFilePath))
            {
                _logger.LogInternalDebug("Tool output file already exists locally: {FilePath}", localFilePath);
                return localFilePath;
            }

            // Download from blob storage
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fileKey);

            // Download to local cache - will throw if blob doesn't exist
            var downloadResponse = blobClient.DownloadTo(localFilePath);

            _logger.LogInternalInformation("Successfully downloaded tool output from blob storage: {FileKey} to {LocalPath}", fileKey, localFilePath);

            return localFilePath;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to ensure tool output file exists: {FileKey}", fileKey);
            return null;
        }
    }

    /// <inheritdoc />
    public int CleanupFilesByThreadId(Guid threadId)
    {
        var deletedCount = 0;

        try
        {
            var threadPrefix = $"{threadId}-";

            // Clean up blob storage
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobs = containerClient.GetBlobs(prefix: threadPrefix);

            foreach (var blobItem in blobs)
            {
                try
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    blobClient.DeleteIfExists();
                    deletedCount++;
                    _logger.LogInternalInformation("Deleted tool output blob for thread {ThreadId}: {BlobName}", threadId, blobItem.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to delete blob: {BlobName}", blobItem.Name);
                }
            }

            // Clean up local cache
            if (Directory.Exists(_localCachePath))
            {
                var localFiles = Directory.EnumerateFiles(_localCachePath, $"{threadId}-*.*", SearchOption.TopDirectoryOnly);
                foreach (var filePath in localFiles)
                {
                    try
                    {
                        File.Delete(filePath);
                        _logger.LogInternalInformation("Deleted local tool output file for thread {ThreadId}: {FilePath}", threadId, filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, "Failed to delete local file: {FilePath}", filePath);
                    }
                }
            }

            _logger.LogInternalInformation("Cleanup completed for thread {ThreadId}. Deleted {DeletedCount} blob(s).", threadId, deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to cleanup tool output files for thread {ThreadId}", threadId);
        }

        return deletedCount;
    }
}
