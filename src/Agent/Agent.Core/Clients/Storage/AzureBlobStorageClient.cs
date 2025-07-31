// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Agent.Core.Clients.Storage
{
    public class AzureBlobStorageClient : IAzureBlobStorageClient
    {
        private const int HttpResponseSuccessMin = 200;
        private const int HttpResponseSucessMax = 299;

        private IAuthenticationService _authService;
        private readonly BlobServiceClient _blobServiceClient;

        public AzureBlobStorageClient(
            IAuthenticationService authService,
            IOptions<StorageSettings> storageSettings)
        {
            _authService = authService;

            TokenCredential credential = _authService.GetStorageCredential();
            var blobEndpoint = storageSettings.Value.BlobEndpoint;
            if( string.IsNullOrEmpty(blobEndpoint))
            {
                blobEndpoint = "https://dummy-sre-blob.blob.core.windows.net/";
            }

            _blobServiceClient = new BlobServiceClient(new Uri(blobEndpoint), credential);
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

        public async Task<Stream> DownloadBlobContentsAsStreamAsync(string containerName, string blobName)
        {
            var blobContainerClient = await GetBlobContainerClient(containerName);
            var blobClient = blobContainerClient.GetBlobClient(blobName);
            var content = await blobClient.OpenReadAsync();
            return content;
        }

        public async Task<Stream> DownloadBlobContentsAsStreamAsync(Uri blobUrl)
        {
            BlobClient blobClient = new BlobClient(blobUrl, _authService.GetStorageCredential());

            Response<BlobDownloadStreamingResult> download = await blobClient.DownloadStreamingAsync();
            return download.Value.Content;
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
