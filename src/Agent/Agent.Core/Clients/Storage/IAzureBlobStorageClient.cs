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
    }
}
