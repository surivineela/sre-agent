// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Clients.Storage;
using Agent.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Core.DataConnectors;

public class DataConnectorStorage<TDataConnector> where TDataConnector : IDataConnector
{
    private readonly DataConnectorSettings _dataConnectorSettings;
    private readonly IAzureBlobStorageClient _storageClient;
    private readonly ILogger<DataConnectorStorage<TDataConnector>> _logger;
    private readonly string _containerName;
    private readonly string _rootPath;
    private readonly string _dataConnectorName;

    public DataConnectorStorage(
        IAzureBlobStorageClient storageClient,
        IOptions<DataConnectorSettings> dataConnectorSettings,
        ILogger<DataConnectorStorage<TDataConnector>> logger)
    {
        _dataConnectorSettings = dataConnectorSettings.Value ?? throw new ArgumentNullException(nameof(dataConnectorSettings));
        _storageClient = storageClient ?? throw new ArgumentNullException(nameof(storageClient));

        _dataConnectorName = typeof(TDataConnector).GetCustomAttribute<DataConnectorAttribute>()?.Type
                    ?? throw new InvalidOperationException($"Data connector type {typeof(TDataConnector).FullName} does not have a DataConnector attribute.");

        _containerName = _dataConnectorSettings.Storage.BlobStorageContainerName;
        _rootPath = _dataConnectorName.ToLowerInvariant();
        _logger = logger;
    }

    public Task UploadBlobContentsAsync(string blobName, BinaryData contents)
    {
        string fullPath = GetFullBlobPath(blobName);
        _logger.LogInternalInformation("Uploading blob contents for data connector {DataConnector} to container: {ContainerName}. Path: {BlobName}", _dataConnectorName, _containerName, fullPath);

        return _storageClient.UploadBlobContentsAsync(_containerName, fullPath, contents, null);
    }

    public Task<bool> DeleteBlobContentsAsync(string blobName)
    {
        string fullPath = GetFullBlobPath(blobName);
        _logger.LogInternalInformation("Deleting blob for data connector {DataConnector} to container: {ContainerName}. Path: {BlobName}", _dataConnectorName, _containerName, fullPath);

        return _storageClient.DeleteBlobContentsAsync(_containerName, fullPath);
    }

    public Task<Stream> DownloadBlobContentsAsStreamAsync(string blobName)
    {
        string fullPath = GetFullBlobPath(blobName);
        _logger.LogInternalInformation("Downloading blob contents for data connector {DataConnector} to container: {ContainerName}. Path: {BlobName}", _dataConnectorName, _containerName, fullPath);

        return _storageClient.DownloadBlobContentsAsStreamAsync(_containerName, fullPath);
    }

    public Task<Stream> DownloadBlobContentsAsStreamAsync(Uri blobUrl)
    {
        _logger.LogInternalInformation("Downloading blob contents for data connector {DataConnector} at URI {Uri}", _dataConnectorName, blobUrl);

        return _storageClient.DownloadBlobContentsAsStreamAsync(blobUrl);
    }

    private string GetFullBlobPath(string blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            throw new ArgumentException("Blob name cannot be null or empty.", nameof(blobName));
        }

        if (blobName.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return blobName; // Already in the correct format
        }

        return blobName.StartsWith("/")
            ? $"{_rootPath}{blobName}"
            : $"{_rootPath}/{blobName}";
    }
}
