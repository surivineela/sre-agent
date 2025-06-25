// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Agent.Core.Clients.Storage
{
    public class AzureBlobStorageClient : IAzureBlobStorageClient
    {
        private const int HttpResponseSuccessMin = 200;
        private const int HttpResponseSucessMax = 299;

        private readonly BlobServiceClient _blobServiceClient;

        public AzureBlobStorageClient(Uri connectionUri, TokenCredential tokenCredential)
        {
            _blobServiceClient = new BlobServiceClient(connectionUri, tokenCredential);
        }

        public async Task<List<string>> ListContainerNamesAsync()
        {
            var containerNames = new List<string>();

            await foreach (var container in _blobServiceClient.GetBlobContainersAsync())
            {
                containerNames.Add(container.Name);
            }

            return containerNames;
        }

        public async Task<List<BlobContainerItem>> GetBlobContainersAsync()
        {
            var blobContainersList = new List<BlobContainerItem>();

            var blobContainers = _blobServiceClient.GetBlobContainersAsync();

            if (blobContainers != null)
            {
                await foreach (var container in blobContainers)
                {
                    blobContainersList.Add(container);
                }

            }
           
            return blobContainersList;
        }

        public async Task UploadBlobContentsAsync(string containerName, string blobName, BinaryData contents, BlobUploadOptions? blobUploadOptions = null)
        {
            var blobContainerClient = await GetBlobContainerClient(containerName);
            var blobClient = blobContainerClient.GetBlobClient(blobName);
            var response = blobUploadOptions == null
                ? await blobClient.UploadAsync(contents, true)
                : await blobClient.UploadAsync(contents, blobUploadOptions);

            if (response.GetRawResponse() is not null && !IsResponseSuccess(response.GetRawResponse().Status))
            {
                throw new RequestFailedException($"Upload request received failed response status {response.GetRawResponse().Status}");
            }
        }

        public async Task UploadFromLocalFilePathAsync(string containerName, string localFilePath)
        {
            var blobContainerClient = await GetBlobContainerClient(containerName);
            var fileName = Path.GetFileName(localFilePath);
            var blobClient = blobContainerClient.GetBlobClient(fileName);
            var response = await blobClient.UploadAsync(localFilePath, true);

            if (response.GetRawResponse() is not null && !IsResponseSuccess(response.GetRawResponse().Status))
            {
                throw new RequestFailedException($"Upload request received failed response status {response.GetRawResponse().Status}");
            }
        }


        public async Task<MemoryStream> DownloadBlobContentsAsMemoryStreamAsync(string containerName, string blobName)
        {
            var memoryStream = new MemoryStream();

            try
            {
                var client = _blobServiceClient.GetBlobContainerClient(containerName);
                if (await client.ExistsAsync())
                {
                    var blobClient = client.GetBlobClient(blobName);

                    if (await blobClient.ExistsAsync())
                    {
                        BlobDownloadInfo download = await blobClient.DownloadAsync();
                        await download.Content.CopyToAsync(memoryStream);
                        memoryStream.Position = 0; // Reset the position to the beginning of the stream
                    }
                    else
                    {
                        throw new RequestFailedException($"blob {blobName} does not exist");
                    }
                }
                else
                {
                    throw new RequestFailedException($"container {containerName} does not exist");
                }
            }
            catch (Exception ex)
            {
                throw new RequestFailedException($"Memory stream download failed due to exception {ex}");
            }

            return memoryStream;
        }

        public async Task<Stream> DownloadBlobContentsAsStreamAsync(string containerName, string blobName)
        {
            var blobContainerClient = await GetBlobContainerClient(containerName);
            var blobClient = blobContainerClient.GetBlobClient(blobName);
            var content = await blobClient.OpenReadAsync();
            return content;
        }

        public async Task<bool> DeleteBlobContentsAsync(string containerName, string blobName)
        {
            var blobContainerClient = await GetBlobContainerClient(containerName);
            var response = await blobContainerClient.DeleteBlobIfExistsAsync(blobName, DeleteSnapshotsOption.IncludeSnapshots);
            if (response.GetRawResponse() is not null && !IsResponseSuccess(response.GetRawResponse().Status))
            {
                throw new RequestFailedException("Delete request received failed response status");

            }
            return response.Value;
        }

        public async Task DeleteContainerAsync(string containerName)
        {
            var container = await GetBlobContainerClient(containerName);

            // Delete the specified container and handle the exception.
            var response = await container.DeleteAsync();
            if (response is not null && !IsResponseSuccess(response.Status) && response.Status != (int)HttpStatusCode.NotFound)
            {
                throw new RequestFailedException($"Blob container delete request received failed response status {response.ToString()}");
            }
        }

        public async Task CopyBlobContentsAsync(Uri sourceBlobUri, string containerName, string blobName)
        {
            var blobContainerClient = await GetBlobContainerClient(containerName);

            var blobClient = blobContainerClient.GetBlobClient(blobName);

            var response = await blobClient.StartCopyFromUriAsync(sourceBlobUri);

            if (response.GetRawResponse() is not null && !IsResponseSuccess(response.GetRawResponse().Status))
            {
                throw new RequestFailedException("Copy request received failed response status");
            }

            await response.WaitForCompletionAsync();
        }

        public async Task CreateContainerIfNotExistAsync(string containerName, PublicAccessType accessType)
        {
            var client = _blobServiceClient.GetBlobContainerClient(containerName);
            await client.CreateIfNotExistsAsync(accessType);
        }

        public async Task<bool> CheckBlobExistsAsync(string containerName, string blobName)
        {
            var blobContainerClient = await GetBlobContainerClient(containerName);
            var blobClient = blobContainerClient.GetBlobClient(blobName);
            return await blobClient.ExistsAsync();
        }

        private async Task<BlobContainerClient> GetBlobContainerClient(string containerName)
        {
            var client = _blobServiceClient.GetBlobContainerClient(containerName);
            if (await client.ExistsAsync())
            {
                return client;
            }

            await client.CreateIfNotExistsAsync();
            return client;
        }

        private static bool IsResponseSuccess(int statusCode)
        {
            return HttpResponseSuccessMin <= statusCode
                    && statusCode <= HttpResponseSucessMax;
        }
    }
}
