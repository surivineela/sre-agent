// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models.Charts;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Logging;
using Agent.Framework;
using Microsoft.Extensions.Hosting;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Linq;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class FunctionAppExecutionFailuresPluginTests
    {
        private readonly Mock<IArmPlugin> _mockArmPlugin;
        private readonly Mock<IAppCodeAnalysisPlugin> _mockAppCodeAnalysisPlugin;
        private readonly Mock<IAppInsightsPlugin> _mockAppInsightsPlugin;
        private readonly Mock<ILogger<FunctionAppExecutionFailuresPlugin>> _mockLogger;
        private readonly FunctionAppExecutionFailuresPlugin _plugin;
        private readonly FunctionAppExecutionFailuresPlugin _pluginWithArmHelper;

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
        private readonly HttpClient _httpClient;

        private const string TestResourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app";
        private const string TestResourceName = "test-function-app";

        public FunctionAppExecutionFailuresPluginTests()
        {
            _mockArmPlugin = new Mock<IArmPlugin>();
            _mockAppCodeAnalysisPlugin = new Mock<IAppCodeAnalysisPlugin>();
            _mockAppInsightsPlugin = new Mock<IAppInsightsPlugin>();
            _mockLogger = new Mock<ILogger<FunctionAppExecutionFailuresPlugin>>();

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

            // Create HttpClient with mocked message handler
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

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

            // Create plugin instance without ArmHelper for existing tests
            _plugin = new FunctionAppExecutionFailuresPlugin(
                _mockArmPlugin.Object,
                null!, // ArmHelper is not used in GetFailedFunctionInvocations and other existing tests
                _mockAppCodeAnalysisPlugin.Object,
                _mockAppInsightsPlugin.Object,
                _mockLogger.Object);

            // Create plugin instance with ArmHelper for GetFunctionAppExecutionFailures tests
            _pluginWithArmHelper = new FunctionAppExecutionFailuresPlugin(
                _mockArmPlugin.Object,
                _armHelper,
                _mockAppCodeAnalysisPlugin.Object,
                _mockAppInsightsPlugin.Object,
                _mockLogger.Object);
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

        private void VerifyHttpRequestMade(HttpMethod expectedMethod, string expectedUrl)
        {
            _mockHttpMessageHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == expectedMethod &&
                        req.RequestUri!.ToString() == expectedUrl),
                    ItExpr.IsAny<CancellationToken>());
        }

        #endregion

        #region GetFunctionAppExecutionFailures Tests

        [Fact]
        public async Task GetFunctionAppExecutionFailures_WithValidResourceId_ReturnsDetectorResponse()
        {
            // Arrange
            var mockDetectorResponse = CreateMockDetectorResponse();
            SetupHttpResponse(HttpStatusCode.OK, mockDetectorResponse);

            // Act
            var result = await _pluginWithArmHelper.GetFunctionAppExecutionFailures(TestResourceId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(mockDetectorResponse, result);

            // Verify information logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[get_function_app_execution_failures] Invoked with resourceId")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppExecutionFailures_WithNullResourceId_ReturnsInvalidMessage()
        {
            // Act
            var result = await _pluginWithArmHelper.GetFunctionAppExecutionFailures(null!);

            // Assert
            Assert.Equal("Invalid resource ID.", result);

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resource ID is null or empty")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppExecutionFailures_WithEmptyResourceId_ReturnsInvalidMessage()
        {
            // Act
            var result = await _pluginWithArmHelper.GetFunctionAppExecutionFailures(string.Empty);

            // Assert
            Assert.Equal("Invalid resource ID.", result);

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resource ID is null or empty")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppExecutionFailures_WithLargeResponse_ExtractsCriticalFailures()
        {
            // Arrange
            var largeResponse = CreateMockLargeDetectorResponseWithCriticalFailures();
            SetupHttpResponse(HttpStatusCode.OK, largeResponse);

            // Act
            var result = await _pluginWithArmHelper.GetFunctionAppExecutionFailures(TestResourceId);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Critical", result);
            Assert.Contains("Detected function(s) having execution failure rate more than 1%", result);

            // Verify information logging about large response
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Response size") && v.ToString()!.Contains("bytes exceeds threshold")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Verify successful extraction logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully extracted critical failure data from large response")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppExecutionFailures_WithLargeResponseNoCriticalFailures_ReturnsFullResponse()
        {
            // Arrange
            var largeResponse = CreateMockLargeDetectorResponseWithoutCriticalFailures();
            SetupHttpResponse(HttpStatusCode.OK, largeResponse);

            // Act
            var result = await _pluginWithArmHelper.GetFunctionAppExecutionFailures(TestResourceId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(largeResponse, result);

            // Verify warning logging about failed extraction
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to extract critical failures table from large response")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppExecutionFailures_WithMalformedLargeResponse_ReturnsFullResponse()
        {
            // Arrange
            var malformedResponse = CreateMockMalformedLargeResponse();
            SetupHttpResponse(HttpStatusCode.OK, malformedResponse);

            // Act
            var result = await _pluginWithArmHelper.GetFunctionAppExecutionFailures(TestResourceId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(malformedResponse, result);

            // Verify warning logging about parsing error
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error while attempting to extract critical failures from large response")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppExecutionFailures_WithHttpException_ReturnsErrorMessage()
        {
            // Arrange
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _pluginWithArmHelper.GetFunctionAppExecutionFailures(TestResourceId);

            // Assert
            Assert.NotNull(result);
            Assert.StartsWith("Failed to retrieve execution failures:", result);

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting function app execution failures")),
                    It.IsAny<HttpRequestException>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppExecutionFailures_WithSmallResponse_ReturnsDirectly()
        {
            // Arrange
            var smallResponse = CreateMockSmallDetectorResponse();
            SetupHttpResponse(HttpStatusCode.OK, smallResponse);

            // Act
            var result = await _pluginWithArmHelper.GetFunctionAppExecutionFailures(TestResourceId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(smallResponse, result);

            // Verify no large response processing occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Response size") && v.ToString()!.Contains("exceeds threshold")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        #endregion

        #region Helper Methods for GetFunctionAppExecutionFailures Tests

        private static string CreateMockDetectorResponse()
        {
            var response = new JObject
            {
                ["id"] = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app",
                ["name"] = "functionExecutionErrors",
                ["type"] = "Microsoft.Web/sites/detectors",
                ["location"] = "East US",
                ["properties"] = new JObject
                {
                    ["metadata"] = new JObject
                    {
                        ["id"] = "functionExecutionErrors",
                        ["name"] = "Function Execution Errors",
                        ["description"] = "Analyzes function execution errors"
                    },
                    ["dataset"] = new JArray
                    {
                        new JObject
                        {
                            ["table"] = new JObject
                            {
                                ["tableName"] = "Function Execution Errors",
                                ["columns"] = new JArray
                                {
                                    new JObject { ["columnName"] = "FunctionName", ["dataType"] = "String" },
                                    new JObject { ["columnName"] = "ErrorCount", ["dataType"] = "Int64" }
                                },
                                ["rows"] = new JArray
                                {
                                    new JArray { "Function1", 5 },
                                    new JArray { "Function2", 3 }
                                }
                            }
                        }
                    }
                }
            };
            return response.ToString();
        }

        private static string CreateMockSmallDetectorResponse()
        {
            var response = new JObject
            {
                ["id"] = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app",
                ["name"] = "functionExecutionErrors",
                ["properties"] = new JObject
                {
                    ["dataset"] = new JArray
                    {
                        new JObject
                        {
                            ["table"] = new JObject
                            {
                                ["rows"] = new JArray { new JArray { "Info", "No critical errors detected" } }
                            }
                        }
                    }
                }
            };
            return response.ToString();
        }

        private static string CreateMockLargeDetectorResponseWithCriticalFailures()
        {
            // Create a response larger than 50KB with critical failures
            var response = new JObject
            {
                ["id"] = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app",
                ["name"] = "functionExecutionErrors",
                ["type"] = "Microsoft.Web/sites/detectors",
                ["location"] = "East US",
                ["properties"] = new JObject
                {
                    ["metadata"] = new JObject
                    {
                        ["id"] = "functionExecutionErrors",
                        ["name"] = "Function Execution Errors"
                    },
                    ["dataset"] = new JArray
                    {
                        new JObject
                        {
                            ["table"] = new JObject
                            {
                                ["tableName"] = "Critical Failures",
                                ["rows"] = new JArray
                                {
                                    new JArray { "Critical", "Detected function(s) having execution failure rate more than 1%.", "Details about the failure" }
                                }
                            }
                        },
                        // Add padding to make it large
                        new JObject
                        {
                            ["table"] = new JObject
                            {
                                ["tableName"] = "Padding",
                                ["rows"] = new JArray(Enumerable.Range(0, 1000).Select(i => new JArray { $"Row{i}", $"Data{i}", new string('x', 100) }))
                            }
                        }
                    }
                }
            };
            return response.ToString();
        }

        private static string CreateMockLargeDetectorResponseWithoutCriticalFailures()
        {
            // Create a large response without critical failures
            var response = new JObject
            {
                ["id"] = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app",
                ["name"] = "functionExecutionErrors",
                ["properties"] = new JObject
                {
                    ["dataset"] = new JArray
                    {
                        new JObject
                        {
                            ["table"] = new JObject
                            {
                                ["rows"] = new JArray(Enumerable.Range(0, 1000).Select(i => new JArray { "Info", $"Information {i}", new string('x', 100) }))
                            }
                        }
                    }
                }
            };
            return response.ToString();
        }

        private static string CreateMockMalformedLargeResponse()
        {
            // Create a large malformed JSON that will cause parsing errors
            return "{ \"invalid\": \"json\"" + new string('x', 52000); // Missing closing brace and padding
        }

        #endregion

        [Fact]
        public async Task GetFailedFunctionInvocations_WithValidResourceId_ReturnsExpectedData()
        {
            // Arrange
            var mockAppInsightsResponse = CreateMockAppInsightsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockAppInsightsResponse);

            // Act
            var result = await _plugin.GetFailedFunctionInvocations(TestResourceId, 60);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);

            // Verify the data is correctly parsed and sorted by timestamp
            var firstDataPoint = result[0];
            Assert.Equal("Function1", firstDataPoint.FunctionName);
            Assert.Equal(new DateTime(2023, 11, 15, 10, 0, 0, DateTimeKind.Utc), firstDataPoint.TimeStamp);
            Assert.Equal(5.0, firstDataPoint.FailedCount);

            var secondDataPoint = result[1];
            Assert.Equal("Function2", secondDataPoint.FunctionName);
            Assert.Equal(new DateTime(2023, 11, 15, 10, 5, 0, DateTimeKind.Utc), secondDataPoint.TimeStamp);
            Assert.Equal(3.0, secondDataPoint.FailedCount);

            var thirdDataPoint = result[2];
            Assert.Equal("Function1", thirdDataPoint.FunctionName);
            Assert.Equal(new DateTime(2023, 11, 15, 10, 10, 0, DateTimeKind.Utc), thirdDataPoint.TimeStamp);
            Assert.Equal(2.0, thirdDataPoint.FailedCount);
        }

        [Fact]
        public async Task GetFailedFunctionInvocations_WithDefaultMinutes_Uses60Minutes()
        {
            // Arrange
            var mockAppInsightsResponse = CreateMockAppInsightsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockAppInsightsResponse);

            // Act
            var result = await _plugin.GetFailedFunctionInvocations(TestResourceId);

            // Assert
            Assert.NotNull(result);

            // Verify that the query was called with the resource ID and a query that includes the expected time range
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query => query.Contains("let timeGrain=5m") && query.Contains(TestResourceName))),
                Times.Once);
        }

        [Fact]
        public async Task GetFailedFunctionInvocations_WithLongTimeRange_UsesCorrectTimeGrain()
        {
            // Arrange
            var mockAppInsightsResponse = CreateMockAppInsightsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockAppInsightsResponse);

            // Act - Use 25 hours to trigger the daily grain
            var result = await _plugin.GetFailedFunctionInvocations(TestResourceId, 25 * 60); // 25 hours in minutes

            // Assert
            Assert.NotNull(result);

            // Verify that the query uses 1d time grain for long time ranges
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query => query.Contains("let timeGrain=1d"))),
                Times.Once);
        }

        [Fact]
        public async Task GetFailedFunctionInvocations_WithMediumTimeRange_Uses10MinuteGrain()
        {
            // Arrange
            var mockAppInsightsResponse = CreateMockAppInsightsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockAppInsightsResponse);

            // Act - Use 12 hours to trigger the 10m grain
            var result = await _plugin.GetFailedFunctionInvocations(TestResourceId, 12 * 60); // 12 hours in minutes

            // Assert
            Assert.NotNull(result);

            // Verify that the query uses 10m time grain for medium time ranges
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query => query.Contains("let timeGrain=10m"))),
                Times.Once);
        }

        [Fact]
        public async Task GetFailedFunctionInvocations_WithResourceIdContainingSlashes_ExtractsCorrectResourceName()
        {
            // Arrange
            var mockAppInsightsResponse = CreateMockAppInsightsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockAppInsightsResponse);

            // Act
            var result = await _plugin.GetFailedFunctionInvocations(TestResourceId, 60);

            // Assert
            Assert.NotNull(result);

            // Verify that the query uses the extracted resource name (last part after the slash)
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query => query.Contains($"cloud_RoleName =~ \"{TestResourceName}\""))),
                Times.Once);
        }

        [Fact]
        public async Task GetFailedFunctionInvocations_WithSimpleResourceName_UsesResourceNameAsIs()
        {
            // Arrange
            const string simpleResourceName = "simple-function-app";
            var mockAppInsightsResponse = CreateMockAppInsightsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockAppInsightsResponse);

            // Act
            var result = await _plugin.GetFailedFunctionInvocations(simpleResourceName, 60);

            // Assert
            Assert.NotNull(result);

            // Verify that the query uses the simple resource name
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    simpleResourceName,
                    It.Is<string>(query => query.Contains($"cloud_RoleName =~ \"{simpleResourceName}\""))),
                Times.Once);
        }

        [Fact]
        public async Task GetFailedFunctionInvocations_WithEmptyAppInsightsResponse_ReturnsEmptyList()
        {
            // Arrange
            var emptyResponse = CreateEmptyAppInsightsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(emptyResponse);

            // Act
            var result = await _plugin.GetFailedFunctionInvocations(TestResourceId, 60);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFailedFunctionInvocations_WithInvalidJson_ReturnsEmptyListAndLogsError()
        {
            // Arrange
            const string invalidJson = "invalid json response";
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(invalidJson);

            // Act
            var result = await _plugin.GetFailedFunctionInvocations(TestResourceId, 60);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);

            // Verify that an error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error parsing failed requests data")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFailedFunctionInvocations_WithMissingColumns_ReturnsEmptyList()
        {
            // Arrange
            var responseWithMissingColumns = CreateAppInsightsResponseWithMissingColumns();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(responseWithMissingColumns);

            // Act
            var result = await _plugin.GetFailedFunctionInvocations(TestResourceId, 60);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFailedFunctionInvocations_WithNullValues_HandlesGracefully()
        {
            // Arrange
            var responseWithNullValues = CreateAppInsightsResponseWithNullValues();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(responseWithNullValues);

            // Act
            var result = await _plugin.GetFailedFunctionInvocations(TestResourceId, 60);

            // Assert
            Assert.NotNull(result);
            // Note: With null values, the JSON parsing may skip the row, so we expect an empty result
            // This is actually the correct behavior - null values should be gracefully ignored
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFailedFunctionInvocations_VerifyQueryStructure_ContainsExpectedElements()
        {
            // Arrange
            var mockAppInsightsResponse = CreateMockAppInsightsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockAppInsightsResponse);

            // Act
            await _plugin.GetFailedFunctionInvocations(TestResourceId, 60);

            // Assert - Verify the query contains all expected elements
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query =>
                        query.Contains("requests") &&
                        query.Contains("success == false") &&
                        query.Contains("client_Type != \"Browser\"") &&
                        query.Contains("summarize FailedCount=sumif(itemCount, success == false)") &&
                        query.Contains("by name, bin(timestamp, timeGrain)"))),
                Times.Once);
        }

        private static string CreateMockAppInsightsResponse()
        {
            var response = new JObject
            {
                ["tables"] = new JArray
                {
                    new JObject
                    {
                        ["columns"] = new JArray
                        {
                            new JObject { ["name"] = "name" },
                            new JObject { ["name"] = "timestamp" },
                            new JObject { ["name"] = "FailedCount" } // This will be converted to lowercase for matching
                        },
                        ["rows"] = new JArray
                        {
                            new JArray { "Function1", "2023-11-15T10:00:00Z", 5.0 },
                            new JArray { "Function2", "2023-11-15T10:05:00Z", 3.0 },
                            new JArray { "Function1", "2023-11-15T10:10:00Z", 2.0 }
                        }
                    }
                }
            };
            return response.ToString();
        }

        private static string CreateEmptyAppInsightsResponse()
        {
            var response = new JObject
            {
                ["tables"] = new JArray
                {
                    new JObject
                    {
                        ["columns"] = new JArray
                        {
                            new JObject { ["name"] = "name" },
                            new JObject { ["name"] = "timestamp" },
                            new JObject { ["name"] = "FailedCount" } // This will be converted to lowercase for matching
                        },
                        ["rows"] = new JArray()
                    }
                }
            };
            return response.ToString();
        }

        private static string CreateAppInsightsResponseWithMissingColumns()
        {
            var response = new JObject
            {
                ["tables"] = new JArray
                {
                    new JObject
                    {
                        ["columns"] = new JArray
                        {
                            new JObject { ["name"] = "somethingElse" }
                        },
                        ["rows"] = new JArray
                        {
                            new JArray { "Function1", "2023-11-15T10:00:00Z", 5.0 }
                        }
                    }
                }
            };
            return response.ToString();
        }

        private static string CreateAppInsightsResponseWithNullValues()
        {
            var response = new JObject
            {
                ["tables"] = new JArray
                {
                    new JObject
                    {
                        ["columns"] = new JArray
                        {
                            new JObject { ["name"] = "name" },
                            new JObject { ["name"] = "timestamp" },
                            new JObject { ["name"] = "failedcount" } // Note: lowercase to match the implementation's expectation
                        },
                        ["rows"] = new JArray
                        {
                            new JArray { JValue.CreateNull(), JValue.CreateNull(), JValue.CreateNull() }
                        }
                    }
                }
            };
            return response.ToString();
        }

        #region GetFunctionAppCallStacks Tests

        [Fact]
        public async Task GetFunctionAppCallStacks_WithValidResourceId_ReturnsCallStacks()
        {
            // Arrange
            const string expectedCallStacks = @"{
                ""callStacks"": [
                    {
                        ""functionName"": ""Function1"",
                        ""stackTrace"": ""at Function1.Run() in Function1.cs:line 25\nat Microsoft.Azure.WebJobs.Host.Executors.FunctionExecutor.TryExecuteAsync""
                    },
                    {
                        ""functionName"": ""Function2"",
                        ""stackTrace"": ""at Function2.Run() in Function2.cs:line 15\nat Microsoft.Azure.WebJobs.Host.Executors.FunctionExecutor.TryExecuteAsync""
                    }
                ]
            }";

            _mockAppCodeAnalysisPlugin
                .Setup(x => x.GetCallStackForApp(TestResourceId))
                .ReturnsAsync(expectedCallStacks);

            // Act
            var result = await _plugin.GetFunctionAppCallStacks(TestResourceId);

            // Assert
            Assert.Equal(expectedCallStacks, result);

            // Verify the method was called with correct parameter
            _mockAppCodeAnalysisPlugin.Verify(
                x => x.GetCallStackForApp(TestResourceId),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppCallStacks_WithEmptyCallStacks_ReturnsEmptyResult()
        {
            // Arrange
            const string emptyCallStacks = @"{""callStacks"":[]}";

            _mockAppCodeAnalysisPlugin
                .Setup(x => x.GetCallStackForApp(TestResourceId))
                .ReturnsAsync(emptyCallStacks);

            // Act
            var result = await _plugin.GetFunctionAppCallStacks(TestResourceId);

            // Assert
            Assert.Equal(emptyCallStacks, result);

            // Verify the method was called
            _mockAppCodeAnalysisPlugin.Verify(
                x => x.GetCallStackForApp(TestResourceId),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppCallStacks_WithNullOrEmptyResult_ReturnsResult()
        {
            // Arrange
            const string nullResult = "";

            _mockAppCodeAnalysisPlugin
                .Setup(x => x.GetCallStackForApp(TestResourceId))
                .ReturnsAsync(nullResult);

            // Act
            var result = await _plugin.GetFunctionAppCallStacks(TestResourceId);

            // Assert
            Assert.Equal(nullResult, result);

            // Verify the method was called
            _mockAppCodeAnalysisPlugin.Verify(
                x => x.GetCallStackForApp(TestResourceId),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppCallStacks_WhenAppCodeAnalysisPluginThrowsException_ReturnsErrorMessage()
        {
            // Arrange
            const string exceptionMessage = "Unable to analyze code for the specified resource";
            var exception = new InvalidOperationException(exceptionMessage);

            _mockAppCodeAnalysisPlugin
                .Setup(x => x.GetCallStackForApp(TestResourceId))
                .ThrowsAsync(exception);

            // Act
            var result = await _plugin.GetFunctionAppCallStacks(TestResourceId);

            // Assert
            Assert.Equal($"Failed to retrieve call stacks: {exceptionMessage}", result);

            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting function app call stacks")),
                    It.Is<Exception>(ex => ex == exception),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppCallStacks_WhenAppCodeAnalysisPluginThrowsHttpException_ReturnsErrorMessage()
        {
            // Arrange
            const string httpExceptionMessage = "HTTP request failed with status 404";
            var httpException = new HttpRequestException(httpExceptionMessage);

            _mockAppCodeAnalysisPlugin
                .Setup(x => x.GetCallStackForApp(TestResourceId))
                .ThrowsAsync(httpException);

            // Act
            var result = await _plugin.GetFunctionAppCallStacks(TestResourceId);

            // Assert
            Assert.Equal($"Failed to retrieve call stacks: {httpExceptionMessage}", result);

            // Verify error was logged with the HTTP exception
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting function app call stacks")),
                    It.Is<Exception>(ex => ex == httpException),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppCallStacks_WhenAppCodeAnalysisPluginThrowsTimeoutException_ReturnsErrorMessage()
        {
            // Arrange
            const string timeoutMessage = "The operation timed out";
            var timeoutException = new TimeoutException(timeoutMessage);

            _mockAppCodeAnalysisPlugin
                .Setup(x => x.GetCallStackForApp(TestResourceId))
                .ThrowsAsync(timeoutException);

            // Act
            var result = await _plugin.GetFunctionAppCallStacks(TestResourceId);

            // Assert
            Assert.Equal($"Failed to retrieve call stacks: {timeoutMessage}", result);

            // Verify error was logged with the timeout exception
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting function app call stacks")),
                    It.Is<Exception>(ex => ex == timeoutException),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppCallStacks_LogsInformationWithResourceId()
        {
            // Arrange
            const string callStacksResult = @"{""callStacks"":[]}";

            _mockAppCodeAnalysisPlugin
                .Setup(x => x.GetCallStackForApp(TestResourceId))
                .ReturnsAsync(callStacksResult);

            // Act
            await _plugin.GetFunctionAppCallStacks(TestResourceId);

            // Assert - Verify information logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[get_function_app_call_stacks] Invoked with resourceId")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppCallStacks_WithDifferentResourceIdFormats_CallsPluginCorrectly()
        {
            // Arrange
            const string differentResourceId = "/subscriptions/different-sub/resourceGroups/different-rg/providers/Microsoft.Web/sites/different-app";
            const string callStacksResult = @"{""callStacks"":[{""functionName"":""TestFunction""}]}";

            _mockAppCodeAnalysisPlugin
                .Setup(x => x.GetCallStackForApp(differentResourceId))
                .ReturnsAsync(callStacksResult);

            // Act
            var result = await _plugin.GetFunctionAppCallStacks(differentResourceId);

            // Assert
            Assert.Equal(callStacksResult, result);

            // Verify the method was called with the different resource ID
            _mockAppCodeAnalysisPlugin.Verify(
                x => x.GetCallStackForApp(differentResourceId),
                Times.Once);
        }

        [Fact]
        public async Task GetFunctionAppCallStacks_WithComplexCallStackData_ReturnsCompleteResult()
        {
            // Arrange
            const string complexCallStacks = @"{
                ""callStacks"": [
                    {
                        ""functionName"": ""HttpTriggerFunction"",
                        ""stackTrace"": ""at HttpTriggerFunction.Run(HttpRequestData req, FunctionContext executionContext) in C:\\home\\site\\wwwroot\\HttpTriggerFunction.cs:line 23\nat Microsoft.Azure.Functions.Worker.Invocation.DefaultFunctionExecutor.ExecuteAsync(FunctionContext context) in C:\\agent\\_work\\1\\s\\src\\DotNetWorker\\src\\DotNetWorker.Core\\Invocation\\DefaultFunctionExecutor.cs:line 42"",
                        ""exceptionType"": ""System.ArgumentException"",
                        ""exceptionMessage"": ""Invalid parameter value""
                    },
                    {
                        ""functionName"": ""TimerTriggerFunction"",
                        ""stackTrace"": ""at TimerTriggerFunction.Run(TimerInfo timer, FunctionContext context) in C:\\home\\site\\wwwroot\\TimerTriggerFunction.cs:line 15\nat Microsoft.Azure.Functions.Worker.Invocation.DefaultFunctionExecutor.ExecuteAsync(FunctionContext context)"",
                        ""exceptionType"": ""System.NullReferenceException"",
                        ""exceptionMessage"": ""Object reference not set to an instance of an object""
                    }
                ],
                ""metadata"": {
                    ""timestamp"": ""2023-11-15T10:30:00Z"",
                    ""resourceId"": """ + TestResourceId + @"""
                }
            }";

            _mockAppCodeAnalysisPlugin
                .Setup(x => x.GetCallStackForApp(TestResourceId))
                .ReturnsAsync(complexCallStacks);

            // Act
            var result = await _plugin.GetFunctionAppCallStacks(TestResourceId);

            // Assert
            Assert.Equal(complexCallStacks, result);

            // Verify the method was called
            _mockAppCodeAnalysisPlugin.Verify(
                x => x.GetCallStackForApp(TestResourceId),
                Times.Once);
        }

        #endregion

        #region GetTop3ExceptionsPerFunction Tests

        [Fact]
        public async Task GetTop3ExceptionsPerFunction_WithValidResourceId_ReturnsExceptionsData()
        {
            // Arrange
            var mockResponse = CreateMockTop3ExceptionsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            var result = await _plugin.GetTop3ExceptionsPerFunction(TestResourceId);

            // Assert
            Assert.Equal(mockResponse, result);

            // Verify the plugin was called with correct parameters
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query =>
                        query.Contains("exceptions") &&
                        query.Contains("client_Type != \"Browser\"") &&
                        query.Contains($"cloud_RoleName =~ \"{TestResourceName}\"") &&
                        query.Contains("top 3 by _count") &&
                        query.Contains("project ExceptionType = ExceptionOrType, ExceptionMessage, StackTrace, Count = _count"))),
                Times.Once);
        }

        [Fact]
        public async Task GetTop3ExceptionsPerFunction_WithCustomTimeRange_UsesProvidedTimes()
        {
            // Arrange
            var startTime = new DateTime(2023, 11, 15, 9, 0, 0, DateTimeKind.Utc);
            var endTime = new DateTime(2023, 11, 15, 10, 0, 0, DateTimeKind.Utc);
            var mockResponse = CreateMockTop3ExceptionsResponse();

            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            var result = await _plugin.GetTop3ExceptionsPerFunction(TestResourceId, startTime, endTime);

            // Assert
            Assert.Equal(mockResponse, result);

            // Verify the query contains the custom time range
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query =>
                        query.Contains($"start=datetime({startTime:O})") &&
                        query.Contains($"end=datetime({endTime:O})") &&
                        query.Contains("let timeGrain=5m"))), // 1 hour range should use 5m grain
                Times.Once);
        }

        [Fact]
        public async Task GetTop3ExceptionsPerFunction_WithDefaultTimes_UsesCorrectDefaults()
        {
            // Arrange
            var mockResponse = CreateMockTop3ExceptionsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            await _plugin.GetTop3ExceptionsPerFunction(TestResourceId);

            // Assert - Verify default time range logic (1 hour back, ending 15 minutes ago)
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query =>
                        query.Contains("exceptions") &&
                        query.Contains("let timeGrain=5m"))), // Default 1 hour range should use 5m grain
                Times.Once);

            // Verify information logging with parameters
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[GetTop3ExceptionsPerFunction] Invoked with resourceId")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetTop3ExceptionsPerFunction_WithLongTimeRange_UsesCorrectTimeGrain()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddDays(-2);
            var endTime = DateTime.UtcNow;
            var mockResponse = CreateMockTop3ExceptionsResponse();

            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            await _plugin.GetTop3ExceptionsPerFunction(TestResourceId, startTime, endTime);

            // Assert - Verify 2 day range uses daily grain
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query => query.Contains("let timeGrain=1d"))),
                Times.Once);
        }

        [Fact]
        public async Task GetTop3ExceptionsPerFunction_WithResourceIdContainingSlashes_ExtractsResourceName()
        {
            // Arrange
            var mockResponse = CreateMockTop3ExceptionsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            await _plugin.GetTop3ExceptionsPerFunction(TestResourceId);

            // Assert - Verify resource name extraction
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query => query.Contains($"cloud_RoleName =~ \"{TestResourceName}\""))),
                Times.Once);
        }

        [Fact]
        public async Task GetTop3ExceptionsPerFunction_VerifyQueryStructure_ContainsExpectedElements()
        {
            // Arrange
            var mockResponse = CreateMockTop3ExceptionsResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            await _plugin.GetTop3ExceptionsPerFunction(TestResourceId);

            // Assert - Verify the query contains all expected elements
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query =>
                        query.Contains("exceptions") &&
                        query.Contains("FunctionName = iif(outerMessage has \"Result: Function\"") &&
                        query.Contains("parse outerMessage with * \"Exception: \" ExceptionType \":\" ExceptionMessage") &&
                        query.Contains("extend ExceptionOrType = iif(isempty(ExceptionType), type, ExceptionType)") &&
                        query.Contains("summarize _count = sum(itemCount)") &&
                        query.Contains("sort by _count desc"))),
                Times.Once);
        }

        #endregion

        #region GetTop3ExceptionsWithStackTraces Tests

        [Fact]
        public async Task GetTop3ExceptionsWithStackTraces_WithValidResourceId_ReturnsExceptionsWithStackTraces()
        {
            // Arrange
            var mockResponse = CreateMockTop3ExceptionsWithStackTracesResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            var result = await _plugin.GetTop3ExceptionsWithStackTraces(TestResourceId);

            // Assert
            Assert.Equal(mockResponse, result);

            // Verify the plugin was called with correct parameters
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query =>
                        query.Contains("exceptions") &&
                        query.Contains("client_Type != \"Browser\"") &&
                        query.Contains($"cloud_RoleName =~ \"{TestResourceName}\"") &&
                        query.Contains("top 3 by _count") &&
                        query.Contains("make_list(FullExceptionMessage, 3)") &&
                        query.Contains("make_list(FullStackTrace, 3)") &&
                        query.Contains("make_list(FunctionName, 3)") &&
                        query.Contains("project ExceptionType = ExceptionOrType, ExceptionMessages, StackTraces, FunctionNames, Count = _count"))),
                Times.Once);
        }

        [Fact]
        public async Task GetTop3ExceptionsWithStackTraces_WithCustomTimeRange_UsesProvidedTimes()
        {
            // Arrange
            var startTime = new DateTime(2023, 11, 15, 8, 0, 0, DateTimeKind.Utc);
            var endTime = new DateTime(2023, 11, 15, 20, 0, 0, DateTimeKind.Utc);
            var mockResponse = CreateMockTop3ExceptionsWithStackTracesResponse();

            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            var result = await _plugin.GetTop3ExceptionsWithStackTraces(TestResourceId, startTime, endTime);

            // Assert
            Assert.Equal(mockResponse, result);

            // Verify the query contains the custom time range (12 hours should use 10m grain)
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query =>
                        query.Contains($"start=datetime({startTime:O})") &&
                        query.Contains($"end=datetime({endTime:O})") &&
                        query.Contains("let timeGrain=10m"))), // 12 hour range should use 10m grain
                Times.Once);
        }

        [Fact]
        public async Task GetTop3ExceptionsWithStackTraces_WithDefaultTimes_UsesCorrectDefaults()
        {
            // Arrange
            var mockResponse = CreateMockTop3ExceptionsWithStackTracesResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            await _plugin.GetTop3ExceptionsWithStackTraces(TestResourceId);

            // Assert - Verify default time range logic
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query =>
                        query.Contains("exceptions") &&
                        query.Contains("let timeGrain=5m"))), // Default 1 hour range should use 5m grain
                Times.Once);

            // Verify information logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[GetTop3ExceptionsWithStackTraces] Invoked with resourceId")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetTop3ExceptionsWithStackTraces_WithResourceIdContainingSlashes_ExtractsResourceName()
        {
            // Arrange
            var mockResponse = CreateMockTop3ExceptionsWithStackTracesResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            await _plugin.GetTop3ExceptionsWithStackTraces(TestResourceId);

            // Assert - Verify resource name extraction
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query => query.Contains($"cloud_RoleName =~ \"{TestResourceName}\""))),
                Times.Once);
        }

        [Fact]
        public async Task GetTop3ExceptionsWithStackTraces_VerifyQueryStructure_ContainsExpectedElements()
        {
            // Arrange
            var mockResponse = CreateMockTop3ExceptionsWithStackTracesResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            await _plugin.GetTop3ExceptionsWithStackTraces(TestResourceId);

            // Assert - Verify the query contains all expected elements
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query =>
                        query.Contains("exceptions") &&
                        query.Contains("extend FullExceptionMessage = iif(isempty(ExceptionMessage), message, ExceptionMessage)") &&
                        query.Contains("extend FullStackTrace = iif(isempty(StackTrace), details, StackTrace)") &&
                        query.Contains("ExceptionMessages = make_list(FullExceptionMessage, 3)") &&
                        query.Contains("StackTraces = make_list(FullStackTrace, 3)") &&
                        query.Contains("FunctionNames = make_list(FunctionName, 3)"))),
                Times.Once);
        }

        [Fact]
        public async Task GetTop3ExceptionsWithStackTraces_WithLongTimeRange_UsesCorrectTimeGrain()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddDays(-3);
            var endTime = DateTime.UtcNow;
            var mockResponse = CreateMockTop3ExceptionsWithStackTracesResponse();

            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            await _plugin.GetTop3ExceptionsWithStackTraces(TestResourceId, startTime, endTime);

            // Assert - Verify 3 day range uses daily grain
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    TestResourceId,
                    It.Is<string>(query => query.Contains("let timeGrain=1d"))),
                Times.Once);
        }

        [Fact]
        public async Task GetTop3ExceptionsWithStackTraces_WithSimpleResourceName_UsesNameDirectly()
        {
            // Arrange
            const string simpleResourceName = "simple-function-app";
            var mockResponse = CreateMockTop3ExceptionsWithStackTracesResponse();
            _mockAppInsightsPlugin
                .Setup(x => x.QueryAppInsightsByWebAppSettings(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(mockResponse);

            // Act
            await _plugin.GetTop3ExceptionsWithStackTraces(simpleResourceName);

            // Assert - Verify simple resource name is used directly
            _mockAppInsightsPlugin.Verify(
                x => x.QueryAppInsightsByWebAppSettings(
                    simpleResourceName,
                    It.Is<string>(query => query.Contains($"cloud_RoleName =~ \"{simpleResourceName}\""))),
                Times.Once);
        }

        #endregion

        #region Helper Methods for Exception Tests

        private static string CreateMockTop3ExceptionsResponse()
        {
            var response = new JObject
            {
                ["tables"] = new JArray
                {
                    new JObject
                    {
                        ["columns"] = new JArray
                        {
                            new JObject { ["name"] = "ExceptionType" },
                            new JObject { ["name"] = "ExceptionMessage" },
                            new JObject { ["name"] = "StackTrace" },
                            new JObject { ["name"] = "Count" }
                        },
                        ["rows"] = new JArray
                        {
                            new JArray
                            {
                                "System.ArgumentException",
                                "Invalid parameter provided",
                                "at Function1.Run() in Function1.cs:line 25",
                                15
                            },
                            new JArray
                            {
                                "System.NullReferenceException",
                                "Object reference not set to an instance of an object",
                                "at Function2.Run() in Function2.cs:line 12",
                                8
                            },
                            new JArray
                            {
                                "System.InvalidOperationException",
                                "Operation not valid for current state",
                                "at Function3.Run() in Function3.cs:line 18",
                                3
                            }
                        }
                    }
                }
            };
            return response.ToString();
        }

        private static string CreateMockTop3ExceptionsWithStackTracesResponse()
        {
            var response = new JObject
            {
                ["tables"] = new JArray
                {
                    new JObject
                    {
                        ["columns"] = new JArray
                        {
                            new JObject { ["name"] = "ExceptionType" },
                            new JObject { ["name"] = "ExceptionMessages" },
                            new JObject { ["name"] = "StackTraces" },
                            new JObject { ["name"] = "FunctionNames" },
                            new JObject { ["name"] = "Count" }
                        },
                        ["rows"] = new JArray
                        {
                            new JArray
                            {
                                "System.ArgumentException",
                                new JArray { "Invalid parameter provided", "Null argument passed", "Missing required parameter" },
                                new JArray
                                {
                                    "at Function1.Run() in Function1.cs:line 25",
                                    "at Function1.Run() in Function1.cs:line 30",
                                    "at Function1.Run() in Function1.cs:line 35"
                                },
                                new JArray { "Function1", "Function1", "Function1" },
                                20
                            },
                            new JArray
                            {
                                "System.NullReferenceException",
                                new JArray { "Object reference not set", "Null object access" },
                                new JArray
                                {
                                    "at Function2.Run() in Function2.cs:line 12",
                                    "at Function2.Run() in Function2.cs:line 20"
                                },
                                new JArray { "Function2", "Function2" },
                                12
                            },
                            new JArray
                            {
                                "System.InvalidOperationException",
                                new JArray { "Operation not valid for current state" },
                                new JArray { "at Function3.Run() in Function3.cs:line 18" },
                                new JArray { "Function3" },
                                5
                            }
                        }
                    }
                }
            };
            return response.ToString();
        }

        #endregion

        #region GetHostRuntimeErrorEvents Tests

        [Fact]
        public async Task GetHostRuntimeErrorEvents_WithNullResourceId_ReturnsInvalidMessage()
        {
            // Act
            var result = await _pluginWithArmHelper.GetHostRuntimeErrorEvents(null!);

            // Assert
            Assert.Equal("Invalid resource ID.", result);

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resource ID is null or empty")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetHostRuntimeErrorEvents_WithEmptyResourceId_ReturnsInvalidMessage()
        {
            // Act
            var result = await _pluginWithArmHelper.GetHostRuntimeErrorEvents(string.Empty);

            // Assert
            Assert.Equal("Invalid resource ID.", result);
        }

        [Fact]
        public async Task GetHostRuntimeErrorEvents_WithInvalidResourceIdFormat_ReturnsInvalidFormat()
        {
            // Arrange
            var invalidResourceId = "/subscriptions/12345";

            // Act
            var result = await _pluginWithArmHelper.GetHostRuntimeErrorEvents(invalidResourceId);

            // Assert
            Assert.Equal("Invalid resource ID format.", result);
        }

        #endregion

        #region IsFunctionApp Tests

        [Fact]
        public async Task IsFunctionApp_WithValidFunctionAppResource_ReturnsTrue()
        {
            // Arrange
            var mockResourceJson = CreateMockFunctionAppResourceJson();
            _mockArmPlugin.Setup(x => x.GetArmResourceAsJson(TestResourceId))
                .ReturnsAsync(mockResourceJson);

            // Act
            var result = await _plugin.IsFunctionApp(TestResourceId);

            // Assert
            Assert.True(result);

            // Verify information logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[is_function_app] Invoked with resourceId")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task IsFunctionApp_WithFunctionAppLinuxResource_ReturnsTrue()
        {
            // Arrange
            var mockResourceJson = CreateMockFunctionAppLinuxResourceJson();
            _mockArmPlugin.Setup(x => x.GetArmResourceAsJson(TestResourceId))
                .ReturnsAsync(mockResourceJson);

            // Act
            var result = await _plugin.IsFunctionApp(TestResourceId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsFunctionApp_WithKindAsArray_ReturnsTrue()
        {
            // Arrange
            var mockResourceJson = CreateMockFunctionAppResourceJsonWithArrayKind();
            _mockArmPlugin.Setup(x => x.GetArmResourceAsJson(TestResourceId))
                .ReturnsAsync(mockResourceJson);

            // Act
            var result = await _plugin.IsFunctionApp(TestResourceId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsFunctionApp_WithWebAppResource_ReturnsFalse()
        {
            // Arrange
            var mockResourceJson = CreateMockWebAppResourceJson();
            _mockArmPlugin.Setup(x => x.GetArmResourceAsJson(TestResourceId))
                .ReturnsAsync(mockResourceJson);

            // Act
            var result = await _plugin.IsFunctionApp(TestResourceId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsFunctionApp_WithResourceWithoutKind_ReturnsFalse()
        {
            // Arrange
            var mockResourceJson = CreateMockResourceJsonWithoutKind();
            _mockArmPlugin.Setup(x => x.GetArmResourceAsJson(TestResourceId))
                .ReturnsAsync(mockResourceJson);

            // Act
            var result = await _plugin.IsFunctionApp(TestResourceId);

            // Assert
            Assert.False(result);

            // Verify warning logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resource does not have a 'kind' property")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task IsFunctionApp_WithNullResourceId_ReturnsFalse()
        {
            // Act
            var result = await _plugin.IsFunctionApp(null!);

            // Assert
            Assert.False(result);

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resource ID is null or empty")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task IsFunctionApp_WithEmptyResourceJson_ReturnsFalse()
        {
            // Arrange
            _mockArmPlugin.Setup(x => x.GetArmResourceAsJson(TestResourceId))
                .ReturnsAsync(string.Empty);

            // Act
            var result = await _plugin.IsFunctionApp(TestResourceId);

            // Assert
            Assert.False(result);

            // Verify warning logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No resource details found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task IsFunctionApp_WithException_ReturnsFalse()
        {
            // Arrange
            _mockArmPlugin.Setup(x => x.GetArmResourceAsJson(TestResourceId))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _plugin.IsFunctionApp(TestResourceId);

            // Assert
            Assert.False(result);

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error checking if resource is a function app")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region HasHostRuntimeErrors Tests

        [Fact]
        public async Task HasHostRuntimeErrors_WithNullResourceId_ReturnsFalse()
        {
            // Act
            var result = await _pluginWithArmHelper.HasHostRuntimeErrors(null!);

            // Assert
            Assert.False(result);

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resource ID is null or empty")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task HasHostRuntimeErrors_WithEmptyResourceId_ReturnsFalse()
        {
            // Act
            var result = await _pluginWithArmHelper.HasHostRuntimeErrors(string.Empty);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region TriggerFunctionAppSync Tests

        [Fact]
        public async Task TriggerFunctionAppSync_WithNullResourceId_ReturnsInvalidMessage()
        {
            // Act
            var result = await _pluginWithArmHelper.TriggerFunctionAppSync(null!);

            // Assert
            Assert.Equal("Invalid resource ID.", result);

            // Verify error logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resource ID is null or empty")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task TriggerFunctionAppSync_WithEmptyResourceId_ReturnsInvalidMessage()
        {
            // Act
            var result = await _pluginWithArmHelper.TriggerFunctionAppSync(string.Empty);

            // Assert
            Assert.Equal("Invalid resource ID.", result);
        }

        #endregion

        #region Helper Methods for Missing Method Tests

        private static string CreateMockFunctionAppResourceJson()
        {
            var resource = new JObject
            {
                ["id"] = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app",
                ["name"] = "test-function-app",
                ["type"] = "Microsoft.Web/sites",
                ["kind"] = "functionapp",
                ["location"] = "East US",
                ["properties"] = new JObject
                {
                    ["state"] = "Running",
                    ["hostNames"] = new JArray { "test-function-app.azurewebsites.net" }
                }
            };
            return resource.ToString();
        }

        private static string CreateMockFunctionAppLinuxResourceJson()
        {
            var resource = new JObject
            {
                ["id"] = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app",
                ["name"] = "test-function-app",
                ["type"] = "Microsoft.Web/sites",
                ["kind"] = "functionapp,linux",
                ["location"] = "East US"
            };
            return resource.ToString();
        }

        private static string CreateMockFunctionAppResourceJsonWithArrayKind()
        {
            var resource = new JObject
            {
                ["id"] = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app",
                ["name"] = "test-function-app",
                ["type"] = "Microsoft.Web/sites",
                ["kind"] = new JArray { "functionapp", "linux" },
                ["location"] = "East US"
            };
            return resource.ToString();
        }

        private static string CreateMockWebAppResourceJson()
        {
            var resource = new JObject
            {
                ["id"] = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-web-app",
                ["name"] = "test-web-app",
                ["type"] = "Microsoft.Web/sites",
                ["kind"] = "app",
                ["location"] = "East US"
            };
            return resource.ToString();
        }

        private static string CreateMockResourceJsonWithoutKind()
        {
            var resource = new JObject
            {
                ["id"] = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app",
                ["name"] = "test-app",
                ["type"] = "Microsoft.Web/sites",
                ["location"] = "East US"
            };
            return resource.ToString();
        }

        #endregion

    }
}
