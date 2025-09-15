// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------ 

using Azure.Storage.Blobs.Models;

namespace Agent.Core.Clients.Storage
{
    public interface IAzureBlobStorageClient
    {
        Task UploadBlobContentsAsync(string containerName, string blobName, BinaryData contents, BlobUploadOptions? blobUploadOptions = null);
        Task UploadFromLocalFilePathAsync(string containerName, string localFilePath);
        Task<bool> DeleteBlobContentsAsync(string containerName, string blobName);
        Task<Stream> DownloadBlobContentsAsStreamAsync(string containerName, string blobName);
        Task<Stream> DownloadBlobContentsAsStreamAsync(Uri blobUrl);
        Task CopyBlobContentsAsync(Uri sourceBlobUri, string containerName, string blobName);
        Task<bool> CheckBlobExistsAsync(string containerName, string blobName);
        Task<BlobProperties> GetBlobPropertiesAsync(string containerName, string blobName, CancellationToken cancellationToken);
        Task<AzureBlobListPage> ListFilesAsync(string containerName, string? prefix = null, int? pageSize = null, bool showFullPath = false, string? continuationToken = null, CancellationToken cancellationToken = default);
    }
}
