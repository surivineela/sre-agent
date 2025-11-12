// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Graph.Crawler.ARM;
using Agent.Logging;
using Agent.Plugins.Implementation;
using Agent.Plugins.Models;
using Gremlin.Net.Driver;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class FunctionAppsPluginTests : IDisposable
    {
        private readonly Mock<IGraphDatabaseClient> _mockGraphDatabaseClient;
        private readonly Mock<ILogger<FunctionAppsPlugin>> _mockLogger;

        // ArmHelper related mocks
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<IArmClientFactory> _mockArmClientFactory;
        private readonly Mock<IAuthenticationService> _mockAuthService;
        private readonly Mock<IHostEnvironment> _mockHostEnvironment;
        private readonly Mock<IChatClientProvider> _mockChatClientProvider;
        private readonly Mock<ICrawlerTriggerService> _mockCrawlerTriggerService;
        private readonly Mock<ISessionPoolService> _mockSessionPoolService;
        private readonly ArmHelper _armHelper;

        private readonly FunctionAppsPlugin _plugin;

        private const string TestResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app";
        private const string TestFunctionName = "TestFunction";
        private const string TestMasterKey = "test-master-key-123";

        public FunctionAppsPluginTests()
        {
            _mockGraphDatabaseClient = new Mock<IGraphDatabaseClient>();
            _mockLogger = new Mock<ILogger<FunctionAppsPlugin>>();

            // Create ArmHelper mocks
            var mockArmHelperLogger = new Mock<ILogger<ArmHelper>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockArmClientFactory = new Mock<IArmClientFactory>();
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockHostEnvironment = new Mock<IHostEnvironment>();
            _mockChatClientProvider = new Mock<IChatClientProvider>();
            _mockCrawlerTriggerService = new Mock<ICrawlerTriggerService>();
            _mockSessionPoolService = new Mock<ISessionPoolService>();
            var mockCustomerLogger = new Mock<CustomerLogger>();
            var mockAzureSettings = new AzureSettings();

            // Create HttpClient with mocked message handler (return new instance each time to avoid disposal issues)
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(_mockHttpMessageHandler.Object));

            // Create ArmHelper instance with mocked dependencies
            _armHelper = new ArmHelper(
                mockArmHelperLogger.Object,
                mockCustomerLogger.Object,
                _mockHttpClientFactory.Object,
                _mockArmClientFactory.Object,
                _mockAuthService.Object,
                mockAzureSettings,
                _mockHostEnvironment.Object,
                _mockCrawlerTriggerService.Object,
                _mockSessionPoolService.Object,
                _mockChatClientProvider.Object);

            _plugin = new FunctionAppsPlugin(
                _mockGraphDatabaseClient.Object,
                _mockLogger.Object,
                _armHelper,
                _mockHttpClientFactory.Object);
        }

        [Fact]
        public async Task TriggerTimerFunctionAsync_WithValidInputs_ReturnsSuccess()
        {
            // Arrange
            var functionAppDescriptor = new FunctionAppDescriptor(
                ResourceId: TestResourceId,
                Name: "test-function-app",
                Kind: "functionapp",
                Location: "eastus",
                Sku: "Standard",
                State: "Running",
                ResourceGroup: "test-rg",
                VnetId: null,
                StackVersion: null,
                PlanType: null,
                MinTlsVersion: null,
                WebSocketEnabled: null,
                NumberOfWorkers: null,
                AutoHealEnabled: null,
                AlwaysOn: null,
                HealthCheckEnabled: null);

            // Mock GetFunctionAppInfoAsync
            SetupMockForGetFunctionAppInfo(functionAppDescriptor);

            // Mock master key retrieval
            var batchResponse = new
            {
                responses = new[]
                {
                    new
                    {
                        httpStatusCode = 200,
                        content = new
                        {
                            masterKey = TestMasterKey,
                            functionKeys = new { },
                            systemKeys = new { }
                        }
                    }
                }
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri != null &&
                        req.RequestUri.ToString() == "https://management.azure.com/batch?api-version=2020-06-01"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(batchResponse))
                });

            // Mock HTTP response for function metadata validation (TimerTrigger check)
            var metadataResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"bindings\":[{\"type\":\"timerTrigger\",\"name\":\"myTimer\",\"schedule\":\"0 */5 * * * *\"}]}")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString() == "https://test-function-app.azurewebsites.net/admin/functions/TestFunction"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(metadataResponse);

            // Mock HTTP response for function trigger
            var expectedResponse = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{\"status\":\"Triggered\"}")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString() == "https://test-function-app.azurewebsites.net/admin/functions/TestFunction" &&
                        req.Headers.Contains("x-functions-key")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _plugin.TriggerTimerFunctionAsync(TestResourceId, TestFunctionName);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Accepted", result.StatusCode);
            Assert.Equal("{\"status\":\"Triggered\"}", result.ResponseContent);
            Assert.Null(result.ErrorMessage);
            Assert.True(result.Duration > TimeSpan.Zero);
        }

        [Fact]
        public async Task TriggerTimerFunctionAsync_RetrievesKeyAndSucceeds()
        {
            // Arrange
            var functionAppDescriptor = new FunctionAppDescriptor(
                ResourceId: TestResourceId,
                Name: "test-function-app",
                Kind: "functionapp",
                Location: "eastus",
                Sku: "Standard",
                State: "Running",
                ResourceGroup: "test-rg",
                VnetId: null,
                StackVersion: null,
                PlanType: null,
                MinTlsVersion: null,
                WebSocketEnabled: null,
                NumberOfWorkers: null,
                AutoHealEnabled: null,
                AlwaysOn: null,
                HealthCheckEnabled: null);

            SetupMockForGetFunctionAppInfo(functionAppDescriptor);

            // Mock master key retrieval
            var batchResponse = new
            {
                responses = new[]
                {
                    new
                    {
                        httpStatusCode = 200,
                        content = new
                        {
                            masterKey = TestMasterKey,
                            functionKeys = new { },
                            systemKeys = new { }
                        }
                    }
                }
            };

            // Mock HTTP response for function metadata validation (TimerTrigger check)
            var metadataResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"bindings\":[{\"type\":\"timerTrigger\",\"name\":\"myTimer\",\"schedule\":\"0 */5 * * * *\"}]}")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString() == "https://test-function-app.azurewebsites.net/admin/functions/TestFunction"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(metadataResponse);

            var batchResponseJson = JsonSerializer.Serialize(batchResponse);
            var triggerResponseJson = "{\"status\":\"Triggered\"}";

            // Setup HTTP responses for batch API call (master key) and function trigger
            SetupHttpResponseForBatchAndTrigger(batchResponseJson, triggerResponseJson);

            // Act
            var result = await _plugin.TriggerTimerFunctionAsync(TestResourceId, TestFunctionName);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Accepted", result.StatusCode);
            Assert.Equal("{\"status\":\"Triggered\"}", result.ResponseContent);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task TriggerTimerFunctionAsync_WithEmptyResourceId_ReturnsError()
        {
            // Act
            var result = await _plugin.TriggerTimerFunctionAsync("", TestFunctionName);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Function app resource ID cannot be empty", result.ErrorMessage);
        }

        [Fact]
        public async Task TriggerTimerFunctionAsync_WithEmptyFunctionName_ReturnsError()
        {
            // Act
            var result = await _plugin.TriggerTimerFunctionAsync(TestResourceId, "");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Function name cannot be empty", result.ErrorMessage);
        }

        [Fact]
        public async Task TriggerTimerFunctionAsync_WithNonExistentFunctionApp_ReturnsError()
        {
            // Arrange
            var emptyResultSet = new ResultSet<dynamic>(new List<Dictionary<string, object>>(), new Dictionary<string, object>());
            _mockGraphDatabaseClient
                .Setup(x => x.Query(It.IsAny<string>()))
                .ReturnsAsync(emptyResultSet);

            // Act
            var result = await _plugin.TriggerTimerFunctionAsync(TestResourceId, TestFunctionName);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.ErrorMessage);
        }

        [Fact]
        public async Task TriggerTimerFunctionAsync_WithHttpError_ReturnsFailure()
        {
            // Arrange
            var functionAppDescriptor = new FunctionAppDescriptor(
                ResourceId: TestResourceId,
                Name: "test-function-app",
                Kind: "functionapp",
                Location: "eastus",
                Sku: "Standard",
                State: "Running",
                ResourceGroup: "test-rg",
                VnetId: null,
                StackVersion: null,
                PlanType: null,
                MinTlsVersion: null,
                WebSocketEnabled: null,
                NumberOfWorkers: null,
                AutoHealEnabled: null,
                AlwaysOn: null,
                HealthCheckEnabled: null);

            SetupMockForGetFunctionAppInfo(functionAppDescriptor);

            // Mock master key retrieval
            var batchResponse = new
            {
                responses = new[]
                {
                    new
                    {
                        httpStatusCode = 200,
                        content = new
                        {
                            masterKey = TestMasterKey,
                            functionKeys = new { },
                            systemKeys = new { }
                        }
                    }
                }
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri != null &&
                        req.RequestUri.ToString() == "https://management.azure.com/batch?api-version=2020-06-01"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(batchResponse))
                });

            // Mock HTTP response for function metadata validation (TimerTrigger check)
            var metadataResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"bindings\":[{\"type\":\"timerTrigger\",\"name\":\"myTimer\",\"schedule\":\"0 */5 * * * *\"}]}")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString() == "https://test-function-app.azurewebsites.net/admin/functions/TestFunction"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(metadataResponse);

            // Mock HTTP error response for function trigger
            var errorResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"Function not found\"}")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString() == "https://test-function-app.azurewebsites.net/admin/functions/TestFunction" &&
                        req.Headers.Contains("x-functions-key")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(errorResponse);

            // Act
            var result = await _plugin.TriggerTimerFunctionAsync(TestResourceId, TestFunctionName);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("NotFound", result.StatusCode);
            Assert.Equal("{\"error\":\"Function not found\"}", result.ResponseContent);
            Assert.Contains("Function trigger failed with status NotFound", result.ErrorMessage);
        }



        [Fact]
        public async Task TriggerTimerFunctionAsync_WithValidTimerFunction_RetrievesKeyAndTriggers()
        {
            // Arrange
            var functionAppDescriptor = new FunctionAppDescriptor(
                ResourceId: TestResourceId,
                Name: "test-function-app",
                Kind: "functionapp",
                Location: "eastus",
                Sku: "Standard",
                State: "Running",
                ResourceGroup: "test-rg",
                VnetId: null,
                StackVersion: null,
                PlanType: null,
                MinTlsVersion: null,
                WebSocketEnabled: null,
                NumberOfWorkers: null,
                AutoHealEnabled: null,
                AlwaysOn: null,
                HealthCheckEnabled: null);

            SetupMockForGetFunctionAppInfo(functionAppDescriptor);

            // Mock master key retrieval
            var batchResponse = new
            {
                responses = new[]
                {
                    new
                    {
                        httpStatusCode = 200,
                        content = new
                        {
                            masterKey = TestMasterKey,
                            functionKeys = new { },
                            systemKeys = new { }
                        }
                    }
                }
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri != null &&
                        req.RequestUri.ToString() == "https://management.azure.com/batch?api-version=2020-06-01"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(batchResponse))
                });

            // Mock HTTP response for function metadata validation (TimerTrigger check)
            var metadataResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"bindings\":[{\"type\":\"timerTrigger\",\"name\":\"myTimer\",\"schedule\":\"0 */5 * * * *\"}]}")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString() == "https://test-function-app.azurewebsites.net/admin/functions/TestFunction"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(metadataResponse);

            var triggerResponseJson = "{\"status\":\"success\"}";

            // Setup HTTP responses for batch API call (master key) and function trigger
            SetupHttpResponseForBatchAndTrigger(JsonSerializer.Serialize(batchResponse), triggerResponseJson);

            // Act
            var result = await _plugin.TriggerTimerFunctionAsync(TestResourceId, TestFunctionName);

            // Assert
            Assert.True(result.Success);
            // Verify that the function was called successfully, indicating master key was retrieved and TimerTrigger validation passed
        }

        #region Helper Methods for HTTP Mocking

        private void SetupHttpResponse(HttpStatusCode statusCode, string responseContent)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent)
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private void SetupHttpResponseForBatchAndTrigger(string batchResponse, string triggerResponse)
        {
            // Setup response for ARM batch API call (to get master key)
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri != null &&
                        req.RequestUri.ToString() == "https://management.azure.com/batch?api-version=2020-06-01"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(batchResponse)
                });

            // Setup response for function trigger call
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri != null &&
                        req.RequestUri.ToString().StartsWith("https://test-function-app.azurewebsites.net/admin/functions/")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent(triggerResponse)
                });
        }

        #endregion

        private void SetupMockForGetFunctionAppInfo(FunctionAppDescriptor functionAppDescriptor)
        {
            var mockResult = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["id"] = TestResourceId.Replace("/", "_"),
                    ["name"] = functionAppDescriptor.Name,
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["kind"] = new List<string> { functionAppDescriptor.Kind },
                        ["location"] = new List<string> { functionAppDescriptor.Location },
                        ["sku"] = new List<string> { functionAppDescriptor.Sku },
                        ["state"] = new List<string> { functionAppDescriptor.State },
                        ["resourceGroupName"] = new List<string> { functionAppDescriptor.ResourceGroup }
                    }
                }
            };

            var resultSet = new ResultSet<dynamic>(mockResult, new Dictionary<string, object>());

            _mockGraphDatabaseClient
                .Setup(x => x.Query(It.IsAny<string>()))
                .ReturnsAsync(resultSet);
        }

        protected virtual void Dispose(bool disposing)
        {
            // HttpClient instances are created by the factory and should not be disposed manually
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
