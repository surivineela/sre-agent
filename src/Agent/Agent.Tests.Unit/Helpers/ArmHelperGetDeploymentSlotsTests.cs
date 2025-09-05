// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Exceptions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shouldly;

namespace Agent.Tests.Unit.Helpers
{
    public class ArmHelperGetDeploymentSlotsTests
    {
        private readonly ArmHelper _armHelper;
        private readonly Mock<ILogger<ArmHelper>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<IArmClientFactory> _mockArmClientFactory;
        private readonly Mock<IAuthenticationService> _mockAuthService;
        private readonly Mock<IHostEnvironment> _mockHostEnvironment;
        private readonly Mock<IChatClient> _mockChatClient;
        private readonly Mock<ICrawlerTriggerService> _mockCrawlerTriggerService;
        private readonly Mock<ISessionPoolService> _mockSessionPoolService;
        private readonly Mock<CustomerLogger> _mockCustomerLogger;
        private readonly HttpClient _httpClient;

        public ArmHelperGetDeploymentSlotsTests()
        {
            // Create all required mocks
            _mockLogger = new Mock<ILogger<ArmHelper>>();
            _mockCustomerLogger = new Mock<CustomerLogger>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockArmClientFactory = new Mock<IArmClientFactory>();
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockHostEnvironment = new Mock<IHostEnvironment>();
            _mockChatClient = new Mock<IChatClient>();
            _mockCrawlerTriggerService = new Mock<ICrawlerTriggerService>();
            _mockSessionPoolService = new Mock<ISessionPoolService>();
            var mockAzureSettings = new AzureSettings();

            // Create HttpClient with mocked message handler
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

            // Create ArmHelper instance with mocked dependencies
            _armHelper = new ArmHelper(
                _mockLogger.Object,
                _mockCustomerLogger.Object,
                _mockHttpClientFactory.Object,
                _mockArmClientFactory.Object,
                _mockAuthService.Object,
                mockAzureSettings,
                _mockHostEnvironment.Object,
                _mockCrawlerTriggerService.Object,
                _mockSessionPoolService.Object,
                _mockChatClient.Object);
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_ValidResourceId_ReturnsSlotResourceIds()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var mockResponseJson = CreateMockSlotsResponse([
                "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/staging",
                "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/dev"
            ]);

            SetupHttpResponse(HttpStatusCode.OK, mockResponseJson);

            // Act
            var result = await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2);
            result[0].ShouldBe("/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/staging");
            result[1].ShouldBe("/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/dev");

            // Verify the correct URL was called
            VerifyHttpRequestMade(HttpMethod.Get, "https://management.azure.com/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots?api-version=2022-03-01");
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_NoSlotsFound_ReturnsEmptyList()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var mockResponseJson = CreateMockSlotsResponse([]);

            SetupHttpResponse(HttpStatusCode.OK, mockResponseJson);

            // Act
            var result = await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(0);
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_EmptyValueArray_ReturnsEmptyList()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var mockResponseJson = @"{""value"":[]}";

            SetupHttpResponse(HttpStatusCode.OK, mockResponseJson);

            // Act
            var result = await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(0);
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_NoValueProperty_ReturnsEmptyList()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var mockResponseJson = @"{""someOtherProperty"":""value""}";

            SetupHttpResponse(HttpStatusCode.OK, mockResponseJson);

            // Act
            var result = await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(0);
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_SlotWithoutId_SkipsSlot()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var mockResponseJson = @"{
                ""value"": [
                    {
                        ""id"": ""/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/staging"",
                        ""name"": ""staging""
                    },
                    {
                        ""name"": ""dev""
                    },
                    {
                        ""id"": ""/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/prod"",
                        ""name"": ""prod""
                    }
                ]
            }";

            SetupHttpResponse(HttpStatusCode.OK, mockResponseJson);

            // Act
            var result = await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2);
            result[0].ShouldBe("/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/staging");
            result[1].ShouldBe("/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/prod");
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_SlotWithEmptyId_SkipsSlot()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var mockResponseJson = @"{
                ""value"": [
                    {
                        ""id"": ""/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/staging"",
                        ""name"": ""staging""
                    },
                    {
                        ""id"": """",
                        ""name"": ""dev""
                    },
                    {
                        ""id"": ""/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/prod"",
                        ""name"": ""prod""
                    }
                ]
            }";

            SetupHttpResponse(HttpStatusCode.OK, mockResponseJson);

            // Act
            var result = await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2);
            result[0].ShouldBe("/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/staging");
            result[1].ShouldBe("/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app/slots/prod");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetDeploymentSlotsResourceIdsAsync_InvalidResourceId_ThrowsArgumentException(string? resourceId)
        {
            // Act & Assert
            var exception = await Should.ThrowAsync<ArgumentException>(
                async () => await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId!));
            
            exception.ParamName.ShouldBe("resourceId");
            exception.Message.ShouldContain("Resource ID is required");
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_MalformedResourceId_ThrowsArgumentException()
        {
            // Arrange
            var resourceId = "invalid-resource-id-format";

            // Act & Assert
            var exception = await Should.ThrowAsync<ArgumentException>(
                async () => await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId));
            
            exception.ParamName.ShouldBe("resourceId");
            exception.Message.ShouldContain("Invalid resource ID format");
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_UnauthorizedAccess_ThrowsToolExecutionUnauthorizedException()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var errorContent = @"{""error"":{""code"":""Unauthorized"",""message"":""Access denied""}}";

            SetupHttpResponse(HttpStatusCode.Unauthorized, errorContent);

            // Act & Assert
            var exception = await Should.ThrowAsync<ToolExecutionUnauthorizedException>(
                async () => await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId));
            
            exception.Message.ShouldContain("Unauthorized access to resource");
            exception.Message.ShouldContain(resourceId);
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_ForbiddenAccess_ThrowsToolExecutionUnauthorizedException()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var errorContent = @"{""error"":{""code"":""Forbidden"",""message"":""Access forbidden""}}";

            SetupHttpResponse(HttpStatusCode.Forbidden, errorContent);

            // Act & Assert
            var exception = await Should.ThrowAsync<ToolExecutionUnauthorizedException>(
                async () => await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId));
            
            exception.Message.ShouldContain("Unauthorized access to resource");
            exception.Message.ShouldContain(resourceId);
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_NotFoundError_ReturnsEmptyList()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var errorContent = @"{""error"":{""code"":""ResourceNotFound"",""message"":""Resource not found""}}";

            SetupHttpResponse(HttpStatusCode.NotFound, errorContent);

            // Act
            var result = await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(0);

            // Verify error was logged
            VerifyLoggedError(resourceId, errorContent);
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_InternalServerError_ReturnsEmptyList()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var errorContent = @"{""error"":{""code"":""InternalServerError"",""message"":""Internal server error""}}";

            SetupHttpResponse(HttpStatusCode.InternalServerError, errorContent);

            // Act
            var result = await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(0);

            // Verify error was logged
            VerifyLoggedError(resourceId, errorContent);
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_HttpException_ThrowsException()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var exceptionMessage = "Network error occurred";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException(exceptionMessage));

            // Act & Assert
            var exception = await Should.ThrowAsync<HttpRequestException>(
                async () => await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId));
            
            exception.Message.ShouldBe(exceptionMessage);

            // Verify error was logged
            VerifyLoggedErrorWithException(resourceId, exceptionMessage);
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_InvalidJsonResponse_ThrowsJsonException()
        {
            // Arrange
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-app";
            var invalidJson = "{ invalid json content";

            SetupHttpResponse(HttpStatusCode.OK, invalidJson);

            // Act & Assert
            var exception = await Should.ThrowAsync<JsonException>(
                async () => await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId));

            // Verify error was logged
            VerifyLoggedErrorWithException(resourceId, exception.Message);
        }

        [Fact]
        public async Task GetDeploymentSlotsResourceIdsAsync_FunctionAppResourceId_ReturnsSlotResourceIds()
        {
            // Arrange - Test with Function App resource ID
            var resourceId = "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app";
            var mockResponseJson = CreateMockSlotsResponse([
                "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app/slots/staging"
            ]);

            SetupHttpResponse(HttpStatusCode.OK, mockResponseJson);

            // Act
            var result = await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);
            result[0].ShouldBe("/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/test-rg/providers/Microsoft.Web/sites/test-function-app/slots/staging");
        }

        #region Helper Methods

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            var response = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private void VerifyHttpRequestMade(HttpMethod expectedMethod, string expectedUrl)
        {
            _mockHttpMessageHandler.Protected()
                .Verify("SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == expectedMethod &&
                        req.RequestUri!.ToString() == expectedUrl),
                    ItExpr.IsAny<CancellationToken>());
        }

        private void VerifyLoggedError(string resourceId, string errorContent)
        {
            _mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Failed to get deployment slots for resource {resourceId}: {errorContent}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        private void VerifyLoggedErrorWithException(string resourceId, string exceptionMessage)
        {
            _mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Failed to get deployment slots for resource {resourceId}: {exceptionMessage}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        private static string CreateMockSlotsResponse(string[] slotIds)
        {
            var slots = slotIds.Select(id => new
            {
                id,
                name = id.Split('/').Last(),
                type = "Microsoft.Web/sites/slots",
                properties = new { }
            }).ToArray();

            var response = new
            {
                value = slots
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        #endregion
    }
}