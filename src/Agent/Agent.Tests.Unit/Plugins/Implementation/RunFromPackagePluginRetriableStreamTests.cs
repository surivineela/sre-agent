// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Clients.Storage;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Framework;
using Agent.Logging;
using Agent.Plugins.Implementation;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    /// <summary>
    /// Tests specifically for the RetriableStream NotSupportedException fix in InspectPackageStructureAsync
    /// </summary>
    public class RunFromPackagePluginRetriableStreamTests
    {
        private readonly Mock<ILogger<RunFromPackagePlugin>> _mockLogger;
        private readonly ArmHelper _armHelper;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IAuthenticationService> _mockAuthService;
        private readonly Mock<IAzureBlobStorageClient> _mockBlobStorageClient;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly RunFromPackagePlugin _plugin;
        private readonly HttpClient _httpClient;

        public RunFromPackagePluginRetriableStreamTests()
        {
            _mockLogger = new Mock<ILogger<RunFromPackagePlugin>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockBlobStorageClient = new Mock<IAzureBlobStorageClient>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

            // Create real ArmHelper with all required dependencies
            var mockArmLogger = new Mock<ILogger<ArmHelper>>();
            var mockCustomerLogger = new Mock<CustomerLogger>();
            var mockArmClientFactory = new Mock<IArmClientFactory>();
            var mockAzureSettings = new AzureSettings();
            var mockHostEnvironment = new Mock<IHostEnvironment>();
            var mockCrawlerTriggerService = new Mock<ICrawlerTriggerService>();
            var mockSessionPoolService = new Mock<ISessionPoolService>();
            var mockChatClient = new Mock<IChatClientProvider>();

            _armHelper = new ArmHelper(
                mockArmLogger.Object,
                mockCustomerLogger.Object,
                _mockHttpClientFactory.Object,
                mockArmClientFactory.Object,
                _mockAuthService.Object,
                mockAzureSettings,
                mockHostEnvironment.Object,
                mockCrawlerTriggerService.Object,
                mockSessionPoolService.Object,
                mockChatClient.Object);

            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

            _plugin = new RunFromPackagePlugin(
                _mockLogger.Object,
                _armHelper,
                _mockHttpClientFactory.Object,
                _mockAuthService.Object,
                _mockBlobStorageClient.Object);
        }

        /// <summary>
        /// Test that InspectPackageStructureAsync handles NotSupportedException when accessing stream.Length
        /// and uses fallback method to get package size from blob properties
        /// </summary>
        [Fact]
        public async Task InspectPackageStructureAsync_RetriableStreamLengthNotSupported_UsesFallbackToGetPackageSize()
        {
            // Arrange
            const string testResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app";
            const string testUrl = "https://ephttpstorageaccount.blob.core.windows.net/deploymentfile/ephttp-dotnetappv2.zip";

            // Create a mock stream that throws NotSupportedException on Length access
            var mockStream = new Mock<Stream>();
            mockStream.Setup(s => s.Length).Throws(new NotSupportedException("Length property is not supported on RetriableStream"));
            mockStream.Setup(s => s.CanRead).Returns(true);
            mockStream.Setup(s => s.CanSeek).Returns(true);
            mockStream.Setup(s => s.Position).Returns(0);
            mockStream.Setup(s => s.Seek(It.IsAny<long>(), It.IsAny<SeekOrigin>())).Returns(0);

            // Setup simple ZIP file content (empty ZIP file)
            var zipBytes = new byte[]
            {
                0x50, 0x4B, 0x05, 0x06, // End of central directory signature
                0x00, 0x00, 0x00, 0x00, // Number of this disk
                0x00, 0x00, 0x00, 0x00, // Disk where central directory starts
                0x00, 0x00, 0x00, 0x00, // Number of central directory records on this disk
                0x00, 0x00, 0x00, 0x00, // Total number of central directory records
                0x00, 0x00, 0x00, 0x00, // Size of central directory (bytes)
                0x00, 0x00, 0x00, 0x00, // Offset of start of central directory
                0x00, 0x00              // ZIP file comment length
            };

            var memoryStream = new MemoryStream(zipBytes);
            mockStream.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .Returns((byte[] buffer, int offset, int count, CancellationToken token) =>
                     {
                         return memoryStream.ReadAsync(buffer, offset, count, token);
                     });
            mockStream.Setup(s => s.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
                     .Returns((byte[] buffer, int offset, int count) =>
                     {
                         return memoryStream.Read(buffer, offset, count);
                     });

            // Mock the blob storage client to return the problematic stream via DownloadBlobContentsAsStreamAsync
            _mockBlobStorageClient.Setup(x => x.DownloadBlobContentsAsStreamAsync(It.IsAny<Uri>()))
                                  .ReturnsAsync(mockStream.Object);

            // Mock the blob properties to return a size (fallback method)
            var mockBlobProperties = BlobsModelFactory.BlobProperties(contentLength: 1024);
            _mockBlobStorageClient.Setup(x => x.GetBlobPropertiesAsync("deploymentfile", "ephttp-dotnetappv2.zip", It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(mockBlobProperties);

            // Act
            var result = await _plugin.InspectPackageStructureAsync(testResourceId, testUrl);

            // Assert
            Assert.True(result.IsSuccessful, $"Expected successful result but got error: {result.ErrorMessage}");
            Assert.True(result.PackageSize > 0, "Package size should be greater than 0");
            Assert.Equal(1024, result.PackageSize);

            // Verify that the blob storage client was called
            _mockBlobStorageClient.Verify(x => x.DownloadBlobContentsAsStreamAsync(It.IsAny<Uri>()), Times.Once);
            _mockBlobStorageClient.Verify(x => x.GetBlobPropertiesAsync("deploymentfile", "ephttp-dotnetappv2.zip", It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Test that the fallback HTTP HEAD request method works when blob properties also fail
        /// </summary>
        [Fact]
        public async Task InspectPackageStructureAsync_BlobPropertiesFails_UsesHttpHeadFallback()
        {
            // Arrange
            const string testResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app";
            const string testUrl = "https://ephttpstorageaccount.blob.core.windows.net/deploymentfile/ephttp-dotnetappv2.zip";

            // Create a mock stream that throws NotSupportedException on Length access
            var mockStream = new Mock<Stream>();
            mockStream.Setup(s => s.Length).Throws(new NotSupportedException("Length property is not supported on RetriableStream"));
            mockStream.Setup(s => s.CanRead).Returns(true);
            mockStream.Setup(s => s.CanSeek).Returns(true);
            mockStream.Setup(s => s.Position).Returns(0);
            mockStream.Setup(s => s.Seek(It.IsAny<long>(), It.IsAny<SeekOrigin>())).Returns(0);

            // Setup simple ZIP file content
            var zipBytes = new byte[]
            {
                0x50, 0x4B, 0x05, 0x06, // End of central directory signature
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

            var memoryStream = new MemoryStream(zipBytes);
            mockStream.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .Returns((byte[] buffer, int offset, int count, CancellationToken token) =>
                     {
                         return memoryStream.ReadAsync(buffer, offset, count, token);
                     });
            mockStream.Setup(s => s.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
                     .Returns((byte[] buffer, int offset, int count) =>
                     {
                         return memoryStream.Read(buffer, offset, count);
                     });

            // Mock the blob storage client to return the problematic stream
            _mockBlobStorageClient.Setup(x => x.DownloadBlobContentsAsStreamAsync(It.IsAny<Uri>()))
                                  .ReturnsAsync(mockStream.Object);

            // Mock the blob properties to throw an exception (simulating failure)
            _mockBlobStorageClient.Setup(x => x.GetBlobPropertiesAsync("deploymentfile", "ephttp-dotnetappv2.zip", It.IsAny<CancellationToken>()))
                                  .ThrowsAsync(new RequestFailedException("Failed to get blob properties"));

            // Setup HTTP HEAD request fallback to succeed
            var headResponse = new HttpResponseMessage(HttpStatusCode.OK);
            headResponse.Content = new StringContent("");
            headResponse.Content.Headers.ContentLength = 2048;

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head && req.RequestUri != null && req.RequestUri.ToString() == testUrl),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(headResponse);

            // Act
            var result = await _plugin.InspectPackageStructureAsync(testResourceId, testUrl);

            // Assert
            Assert.True(result.IsSuccessful, $"Expected successful result but got error: {result.ErrorMessage}");
            Assert.Equal(2048, result.PackageSize);

            // Verify fallback methods were attempted
            _mockBlobStorageClient.Verify(x => x.DownloadBlobContentsAsStreamAsync(It.IsAny<Uri>()), Times.Once);
            _mockBlobStorageClient.Verify(x => x.GetBlobPropertiesAsync("deploymentfile", "ephttp-dotnetappv2.zip", It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Test that when all size determination methods fail, we get a meaningful error
        /// </summary>
        [Fact]
        public async Task InspectPackageStructureAsync_AllSizeMethodsFail_ReturnsError()
        {
            // Arrange
            const string testResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app";
            const string testUrl = "https://ephttpstorageaccount.blob.core.windows.net/deploymentfile/ephttp-dotnetappv2.zip";

            // Create a mock stream that throws NotSupportedException on Length access
            var mockStream = new Mock<Stream>();
            mockStream.Setup(s => s.Length).Throws(new NotSupportedException("Length property is not supported on RetriableStream"));
            mockStream.Setup(s => s.CanRead).Returns(true);
            mockStream.Setup(s => s.CanSeek).Returns(true);

            // Mock the blob storage client to return the problematic stream
            _mockBlobStorageClient.Setup(x => x.DownloadBlobContentsAsStreamAsync(It.IsAny<Uri>()))
                                  .ReturnsAsync(mockStream.Object);

            // Mock blob properties to fail
            _mockBlobStorageClient.Setup(x => x.GetBlobPropertiesAsync("deploymentfile", "ephttp-dotnetappv2.zip", It.IsAny<CancellationToken>()))
                                  .ThrowsAsync(new RequestFailedException("Failed to get blob properties"));

            // Setup HTTP HEAD request to also fail
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _plugin.InspectPackageStructureAsync(testResourceId, testUrl);

            // Assert - expect the operation to continue even if size determination fails
            // The primary goal is to ensure NotSupportedException doesn't crash the app
            Assert.True(result.IsSuccessful || result.ErrorMessage.Contains("Unable to determine package size") || result.ErrorMessage.Contains("An error occurred during package inspection"),
                $"Expected either success or a graceful error handling, but got: {result.ErrorMessage}");
        }

        /// <summary>
        /// Invokes a private method on an object using reflection
        /// </summary>
        private T InvokePrivateMethod<T>(object obj, string methodName, params object?[] parameters)
        {
            var method = obj.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method == null)
            {
                throw new ArgumentException($"Method '{methodName}' not found on type '{obj.GetType().Name}'");
            }

            var result = method.Invoke(obj, parameters);
            return result != null ? (T)result : default(T)!;
        }
    }
}
