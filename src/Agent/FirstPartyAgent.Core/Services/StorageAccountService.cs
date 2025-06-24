// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FirstPartyAgent.Core.Configuration;
using Agent.Core.Configuration;

namespace FirstPartyAgent.Core.Services
{
    public interface IStorageService
    {
        Task<string> ReadFileFromStorage(string containerName, string blobName);
        Task WriteContentToStorage(string containerName, string blobName, string content);
        Task<List<string>> ListFilesInContainer(string containerName);
        bool IsEnabled { get; }
    }

    public class StorageServiceDisabled: IStorageService
    {
        public bool IsEnabled => false;
        public async Task<string> ReadFileFromStorage(string containerName, string blobName)
        {
            return string.Empty;
        }
        public async Task WriteContentToStorage(string containerName, string blobName, string content)
        {
            return;
        }
        public async Task<List<string>> ListFilesInContainer(string containerName)
        {
            return new List<string>();
        }
    }
    public class StorageService : IStorageService
    {
        public bool IsEnabled => true;        
        private readonly BlobServiceClient _blobServiceClient;

        public StorageService(StorageAccountSettings storageAccountSettings)
        {
            string storageAccountUrl = storageAccountSettings.AccountUrl;
            var credential = new DefaultAzureCredential();
            _blobServiceClient = new BlobServiceClient(new Uri(storageAccountUrl), credential);
        }

        private async Task<BlobContainerClient> GetContainerClient(string containerName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync();
                return containerClient;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get container client: {ex.Message}");
            }
        }

        public async Task<string> ReadFileFromStorage(string containerName, string blobName)
        {
            BlobContainerClient containerClient = await GetContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);
            BlobDownloadInfo resultInfo = await blobClient.DownloadAsync();
            var buffer = new byte[resultInfo.ContentLength];
            StreamReader reader = new StreamReader(resultInfo.Content);
            string res = reader.ReadToEnd();
            return res;
        }

        public async Task<List<string>> ListFilesInContainer(string containerName)
        {
            BlobContainerClient containerClient = await GetContainerClient(containerName);
            List<string> fileNames = new List<string>();
            try
            {
                await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
                {
                    fileNames.Add(blobItem.Name);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to list files in container: {ex.Message}");
            }
            return fileNames;
        }

        public async Task WriteContentToStorage(string containerName, string blobName, string content)
        {
            try
            {
                BlobContainerClient containerClient = await GetContainerClient(containerName);
                BlobClient blobClient = containerClient.GetBlobClient(blobName);
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }
                // Removed unreliable content length validation
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write string to blob: {ex.Message}");
            }
        }
    }
}

