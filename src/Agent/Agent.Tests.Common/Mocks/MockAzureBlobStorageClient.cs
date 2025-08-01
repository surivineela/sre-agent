// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Clients.Storage;
using Azure.Storage.Blobs.Models;

namespace Agent.Tests.Common.Mocks
{
    internal class MockAzureBlobStorageClient : IAzureBlobStorageClient
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte[]>> _containers = new();

        public bool HasContainer(string name)
        {
            return _containers.ContainsKey(name);
        }

        public Task CopyBlobContentsAsync(Uri sourceBlobUri, string containerName, string blobName)
        {
            throw new NotImplementedException();
        }


        public Task<bool> DeleteBlobContentsAsync(string containerName, string blobName)
        {
            if (_containers.TryGetValue(containerName, out var blobs))
            {
                blobs.TryRemove(blobName, out _);
            }

            return Task.FromResult(true);
        }


        public Task<Stream> DownloadBlobContentsAsStreamAsync(string containerName, string blobName)
        {
            if (_containers.TryGetValue(containerName, out var blobs))
            {
                if (blobs.TryGetValue(blobName, out var blob))
                {
                    return Task.FromResult((Stream)new MemoryStream(blob));
                }
            }

            return Task.FromResult((Stream)new MemoryStream());
        }

        public Task<Stream> DownloadBlobContentsAsStreamAsync(Uri blobUrl)
        {
            throw new NotImplementedException();
        }

        public Task UploadBlobContentsAsync(string containerName, string blobName, BinaryData contents, BlobUploadOptions? blobUploadOptions = null)
        {
            byte[] buffer = contents.ToArray();
            GetOrAddContainerBlobs(containerName).AddOrUpdate(blobName, buffer, (key, oldValue) => buffer);

            return Task.CompletedTask;
        }

        public Task UploadFromLocalFilePathAsync(string containerName, string localFilePath)
        {
            // not actualy reading file here, change this behavior if we actually need to verify file contents in integration tests

            var fileName = Path.GetFileName(localFilePath) ?? "filename";
            var fileContents = System.Text.Encoding.Default.GetBytes("filecontents");
            GetOrAddContainerBlobs(containerName).AddOrUpdate(fileName, fileContents, (key, oldValue) => fileContents);

            return Task.CompletedTask;
        }

        public Task<bool> CheckBlobExistsAsync(string containerName, string blobName)
        {
            if (_containers.TryGetValue(containerName, out var blobs))
            {
                if (blobs.TryGetValue(blobName, out _))
                {
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }

        private ConcurrentDictionary<string, byte[]> GetOrAddContainerBlobs(string containerName)
        {
            return _containers.GetOrAdd(containerName, (key) => { return new ConcurrentDictionary<string, byte[]>(); });
        }

        public Task<BlobProperties> GetBlobPropertiesAsync(string containerName, string blobName, CancellationToken cancellationToken)
        {
            return Task.FromResult(new BlobProperties());
        }
    }
}
