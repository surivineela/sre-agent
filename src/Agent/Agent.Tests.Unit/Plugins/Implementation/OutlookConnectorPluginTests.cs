// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Plugins.Connector;
using Agent.Plugins.Implementation;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class OutlookConnectorPluginTests
    {
        private readonly Mock<ILogger<OutlookConnectorPlugin>> _mockLogger;
        private readonly Mock<IConnectorResolver> _mockConnectorResolver;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IAuthenticationService> _mockAuthenticationService;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly OutlookConnector _testConnector;
        private readonly OutlookConnectorPlugin _outlookConnectorPlugin;

        private const string TestApiBaseUrl = "https://test.outlook.api/";
        private const string TestAccessToken = "test-access-token";

        public OutlookConnectorPluginTests()
        {
            _mockLogger = new Mock<ILogger<OutlookConnectorPlugin>>();
            _mockConnectorResolver = new Mock<IConnectorResolver>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockAuthenticationService = new Mock<IAuthenticationService>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

            _testConnector = new OutlookConnector
            {
                ConnectionRuntimeUrl = TestApiBaseUrl,
                Auth = new ConnectorAuthSettings
                {
                    AuthenticationType = ConnectorAuthType.ManagedIdentity
                }
            };

            _mockConnectorResolver
                .Setup(x => x.GetConnectorFromSettings<OutlookConnector>(
                    It.IsAny<string>(),
                    "Outlook",
                    It.IsAny<string>()))
                .Returns(_testConnector);

            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockHttpClientFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            var mockCredential = new Mock<TokenCredential>();
            mockCredential
                .Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccessToken(TestAccessToken, DateTimeOffset.UtcNow.AddHours(1)));

            _mockAuthenticationService
                .Setup(x => x.GetDataConnectorCredential(It.IsAny<ConnectorAuthSettings>()))
                .Returns(mockCredential.Object);

            _outlookConnectorPlugin = new OutlookConnectorPlugin(
                _mockLogger.Object,
                _mockConnectorResolver.Object,
                _mockHttpClientFactory.Object,
                _mockAuthenticationService.Object);
        }

        [Fact]
        public void Constructor_WithNullConnectorResolver_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new OutlookConnectorPlugin(
                    _mockLogger.Object,
                    null!,
                    _mockHttpClientFactory.Object,
                    _mockAuthenticationService.Object));

            Assert.Equal("connectorResolver", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new OutlookConnectorPlugin(
                    _mockLogger.Object,
                    _mockConnectorResolver.Object,
                    null!,
                    _mockAuthenticationService.Object));

            Assert.Equal("httpClientFactory", exception.ParamName);
        }

        [Fact]
        public void Constructor_WithNullAuthenticationService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new OutlookConnectorPlugin(
                    _mockLogger.Object,
                    _mockConnectorResolver.Object,
                    _mockHttpClientFactory.Object,
                    null!));

            Assert.Equal("authenticationService", exception.ParamName);
        }

        [Fact]
        public async Task SendEmailAsync_WithValidEmail_ReturnsSuccessResult()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "<p>Test Body</p>";
            var bodyType = "HTML";
            var importance = "Normal";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().Contains("v2/Mail")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Email sent successfully")
                });

            // Act
            var result = await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, bodyType, importance, null, null);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal("Email sent successfully.", result.Message);
        }

        [Fact]
        public async Task SendEmailAsync_WithNullTo_ThrowsArgumentNullException()
        {
            // Arrange
            var subject = "Test Subject";
            var body = "Test Body";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _outlookConnectorPlugin.SendEmailAsync(null!, subject, body, "HTML", "Normal", null, null));
        }

        [Fact]
        public async Task SendEmailAsync_WithNullSubject_ThrowsArgumentNullException()
        {
            // Arrange
            var to = "test@example.com";
            var body = "Test Body";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _outlookConnectorPlugin.SendEmailAsync(to, null!, body, "HTML", "Normal", null, null));
        }

        [Fact]
        public async Task SendEmailAsync_WithNullBody_ThrowsArgumentNullException()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _outlookConnectorPlugin.SendEmailAsync(to, subject, null!, "HTML", "Normal", null, null));
        }

        [Fact]
        public async Task SendEmailAsync_WithEmptyTo_ReturnsFailureResult()
        {
            // Arrange
            var to = "";
            var subject = "Test Subject";
            var body = "Test Body";

            // Act
            var result = await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "Normal", null, null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("Recipient, subject, and body must all be provided.", result.Message);
        }

        [Fact]
        public async Task SendEmailAsync_WithWhitespaceTo_ReturnsFailureResult()
        {
            // Arrange
            var to = "   ";
            var subject = "Test Subject";
            var body = "Test Body";

            // Act
            var result = await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "Normal", null, null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("Recipient, subject, and body must all be provided.", result.Message);
        }

        [Fact]
        public async Task SendEmailAsync_WithEmptySubject_ReturnsFailureResult()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "";
            var body = "Test Body";

            // Act
            var result = await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "Normal", null, null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("Recipient, subject, and body must all be provided.", result.Message);
        }

        [Fact]
        public async Task SendEmailAsync_WithEmptyBody_ReturnsFailureResult()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "";

            // Act
            var result = await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "Normal", null, null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("Recipient, subject, and body must all be provided.", result.Message);
        }

        [Fact]
        public async Task SendEmailAsync_WithCcAndBcc_SendsEmailSuccessfully()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Test Body";
            var cc = "cc@example.com";
            var bcc = "bcc@example.com";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Email sent successfully")
                });

            // Act
            var result = await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "Normal", cc, bcc);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task SendEmailAsync_WithHighImportance_SendsEmailSuccessfully()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Urgent: Test Subject";
            var body = "Test Body";
            var importance = "High";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Email sent successfully")
                });

            // Act
            var result = await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", importance, null, null);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task SendEmailAsync_WithTextBodyType_SendsEmailSuccessfully()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Plain text body";
            var bodyType = "Text";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Email sent successfully")
                });

            // Act
            var result = await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, bodyType, "Normal", null, null);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task SendEmailAsync_WhenHttpRequestFails_ReturnsFailureResult()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Test Body";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("Bad Request Error")
                });

            // Act
            var result = await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "Normal", null, null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("Email send request failed.", result.Message);
        }

        [Fact]
        public async Task SendEmailAsync_WhenUnauthorized_ReturnsFailureResult()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Test Body";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Unauthorized,
                    Content = new StringContent("Unauthorized")
                });

            // Act
            var result = await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "Normal", null, null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal(401, result.StatusCode);
            Assert.Equal("Email send request failed.", result.Message);
        }

        [Fact]
        public async Task SendEmailAsync_IncludesAuthorizationHeader()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Test Body";
            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Email sent successfully")
                });

            // Act
            await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "Normal", null, null);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest.Headers.Authorization);
            Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
            Assert.Equal(TestAccessToken, capturedRequest.Headers.Authorization.Parameter);
        }

        [Fact]
        public async Task SendEmailAsync_SendsCorrectEndpoint()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Test Body";
            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Email sent successfully")
                });

            // Act
            await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "Normal", null, null);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Post, capturedRequest.Method);
            Assert.Contains("v2/Mail", capturedRequest.RequestUri!.ToString());
        }

        [Fact]
        public async Task SendEmailAsync_SendsCorrectPayload()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Test Body";
            var cc = "cc@example.com";
            var bcc = "bcc@example.com";
            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Email sent successfully")
                });

            // Act
            await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "High", cc, bcc);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest.Content);

            var requestBody = await capturedRequest.Content.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<JsonElement>(requestBody);

            Assert.Equal(to, payload.GetProperty("To").GetString());
            Assert.Equal(subject, payload.GetProperty("Subject").GetString());
            Assert.Equal(body, payload.GetProperty("Body").GetString());
            Assert.Equal("High", payload.GetProperty("Importance").GetString());
            Assert.True(payload.GetProperty("IsHtml").GetBoolean());
            Assert.Equal(cc, payload.GetProperty("Cc").GetString());
            Assert.Equal(bcc, payload.GetProperty("Bcc").GetString());
        }

        [Fact]
        public async Task SendEmailAsync_WithCancellationToken_PropagatesCancellation()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Test Body";
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "Normal", null, null, cts.Token));
        }

        [Fact]
        public async Task SendEmailAsync_WithHttpException_ReturnsErrorResult()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Test Body";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", "Normal", null, null);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            Assert.Equal("Unexpected error while sending email.", result.Message);
        }

        [Fact]
        public async Task SendEmailAsync_NormalizesLowImportance()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Test Body";
            var importance = "low";
            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Email sent successfully")
                });

            // Act
            await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", importance, null, null);

            // Assert
            Assert.NotNull(capturedRequest);
            var requestBody = await capturedRequest.Content!.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<JsonElement>(requestBody);
            Assert.Equal("Low", payload.GetProperty("Importance").GetString());
        }

        [Fact]
        public async Task SendEmailAsync_NormalizesHighImportance()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Test Body";
            var importance = "high";
            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Email sent successfully")
                });

            // Act
            await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", importance, null, null);

            // Assert
            Assert.NotNull(capturedRequest);
            var requestBody = await capturedRequest.Content!.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<JsonElement>(requestBody);
            Assert.Equal("High", payload.GetProperty("Importance").GetString());
        }

        [Fact]
        public async Task SendEmailAsync_DefaultsToNormalImportance()
        {
            // Arrange
            var to = "test@example.com";
            var subject = "Test Subject";
            var body = "Test Body";
            var importance = "invalid";
            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("Email sent successfully")
                });

            // Act
            await _outlookConnectorPlugin.SendEmailAsync(to, subject, body, "HTML", importance, null, null);

            // Assert
            Assert.NotNull(capturedRequest);
            var requestBody = await capturedRequest.Content!.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<JsonElement>(requestBody);
            Assert.Equal("Normal", payload.GetProperty("Importance").GetString());
        }
    }
}
