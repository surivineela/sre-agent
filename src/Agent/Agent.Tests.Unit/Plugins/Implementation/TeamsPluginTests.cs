// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Plugins.Connector;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace Agent.Tests.Unit.Plugins.Implementation
{
    public class TeamsPluginTests
    {
        private readonly Mock<ILogger<TeamsPlugin>> _mockLogger;
        private readonly Mock<IConnectorResolver> _mockConnectorResolver;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IAuthenticationService> _mockAuthenticationService;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly TeamsApiHubConnector _testConnector;
        private readonly TeamsPlugin _teamsPlugin;

        private const string TestApiBaseUrl = "https://test.teams.api/";
        private const string TestGroupId = "12345678-1234-1234-1234-123456789012";
        private const string TestChannelId = "19:test-channel-id@thread.tacv2";
        private const string TestAccessToken = "test-access-token";

        public TeamsPluginTests()
        {
            _mockLogger = new Mock<ILogger<TeamsPlugin>>();
            _mockConnectorResolver = new Mock<IConnectorResolver>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockAuthenticationService = new Mock<IAuthenticationService>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

            _testConnector = new TeamsApiHubConnector
            {
                ConnectionRuntimeUrl = TestApiBaseUrl,
                GroupId = TestGroupId,
                ChannelId = TestChannelId,
                Auth = new ConnectorAuthSettings
                {
                    AuthenticationType = ConnectorAuthType.ManagedIdentity
                }
            };

            _mockConnectorResolver
                .Setup(x => x.GetConnectorFromSettings<TeamsApiHubConnector>(
                    It.IsAny<string>(),
                    "Teams",
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

            _teamsPlugin = new TeamsPlugin(
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
                new TeamsPlugin(
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
                new TeamsPlugin(
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
                new TeamsPlugin(
                    _mockLogger.Object,
                    _mockConnectorResolver.Object,
                    _mockHttpClientFactory.Object,
                    null!));

            Assert.Equal("authenticationService", exception.ParamName);
        }

        [Fact]
        public async Task PostMessageToChannel_WithValidMessage_ReturnsPostMessageResult()
        {
            // Arrange
            var subject = "Test Subject";
            var message = "<p>Test Message</p>";
            var expectedMessageId = "1234567890";
            var expectedWebUrl = "https://teams.microsoft.com/test";

            var responseMessage = new TeamsChannelMessage(
                Id: expectedMessageId,
                WebUrl: expectedWebUrl,
                Subject: subject,
                From: new From(new User("user-id", "Test User", "user", "tenant-id")),
                MessageType: "message",
                Body: new Body("html", message),
                CreatedDateTime: DateTimeOffset.UtcNow,
                LastModifiedDateTime: DateTimeOffset.UtcNow,
                LastEditedDateTime: null);

            var jsonResponse = JsonSerializer.Serialize(responseMessage, new JsonSerializerOptions(JsonSerializerOptions.Web));

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().Contains($"v3/beta/teams/{TestGroupId}/channels/{TestChannelId}/messages")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent(jsonResponse)
                });

            // Act
            var result = await _teamsPlugin.PostMessageToChannel(subject, message);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedMessageId, result.Id);
            Assert.Equal(expectedWebUrl, result.WebUrl);
        }

        [Fact]
        public async Task PostMessageToChannel_WithEmptyMessage_ThrowsArgumentException()
        {
            // Arrange
            var subject = "Test Subject";
            var message = "";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _teamsPlugin.PostMessageToChannel(subject, message));
        }

        [Fact]
        public async Task PostMessageToChannel_WithWhitespaceMessage_ThrowsArgumentException()
        {
            // Arrange
            var subject = "Test Subject";
            var message = "   ";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _teamsPlugin.PostMessageToChannel(subject, message));
        }

        [Fact]
        public async Task PostMessageToChannel_WhenHttpRequestFails_ThrowsHttpRequestException()
        {
            // Arrange
            var subject = "Test Subject";
            var message = "<p>Test Message</p>";

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

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                _teamsPlugin.PostMessageToChannel(subject, message));

            Assert.Contains("Failed to send message to Teams channel", exception.Message);
            Assert.Contains("BadRequest", exception.Message);
        }

        [Fact]
        public async Task PostMessageToChannel_WhenDeserializationFails_ThrowsJsonException()
        {
            // Arrange
            var subject = "Test Subject";
            var message = "<p>Test Message</p>";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent("invalid json")
                });

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(() =>
                _teamsPlugin.PostMessageToChannel(subject, message));
        }

        [Fact]
        public async Task GetMessagesFromChannel_WithValidResponse_ReturnsMessages()
        {
            // Arrange
            var messages = new List<TeamsChannelMessage>
            {
                new TeamsChannelMessage(
                    Id: "msg-1",
                    WebUrl: "https://teams.microsoft.com/msg1",
                    Subject: "Message 1",
                    From: new From(new User("user-1", "User One", "user", "tenant-id")),
                    MessageType: "message",
                    Body: new Body("html", "<p>First message</p>"),
                    CreatedDateTime: DateTimeOffset.UtcNow.AddHours(-2),
                    LastModifiedDateTime: DateTimeOffset.UtcNow.AddHours(-2),
                    LastEditedDateTime: null),
                new TeamsChannelMessage(
                    Id: "msg-2",
                    WebUrl: "https://teams.microsoft.com/msg2",
                    Subject: "Message 2",
                    From: new From(new User("user-2", "User Two", "user", "tenant-id")),
                    MessageType: "message",
                    Body: new Body("html", "<p>Second message</p>"),
                    CreatedDateTime: DateTimeOffset.UtcNow.AddHours(-1),
                    LastModifiedDateTime: DateTimeOffset.UtcNow.AddHours(-1),
                    LastEditedDateTime: null)
            };

            var response = new { value = messages };
            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerOptions.Web));

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString().Contains($"beta/teams/{TestGroupId}/channels/{TestChannelId}/messages")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });

            // Act
            var result = await _teamsPlugin.GetMessagesFromChannel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("msg-1", result[0].Id);
            Assert.Equal("msg-2", result[1].Id);
        }

        [Fact]
        public async Task GetMessagesFromChannel_WhenNoMessages_ReturnsEmptyList()
        {
            // Arrange
            var response = new { value = new List<TeamsChannelMessage>() };
            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerOptions.Web));

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });

            // Act
            var result = await _teamsPlugin.GetMessagesFromChannel();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMessagesFromChannel_WhenHttpRequestFails_ThrowsHttpRequestException()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Forbidden,
                    Content = new StringContent("Forbidden")
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                _teamsPlugin.GetMessagesFromChannel());

            Assert.Contains("Failed to get messages from Teams channel", exception.Message);
            Assert.Contains("Forbidden", exception.Message);
        }

        [Fact]
        public async Task PostMessageToChannel_IncludesAuthorizationHeader()
        {
            // Arrange
            var subject = "Test Subject";
            var message = "<p>Test Message</p>";
            HttpRequestMessage? capturedRequest = null;

            var responseMessage = new TeamsChannelMessage(
                Id: "test-id",
                WebUrl: "https://teams.microsoft.com/test",
                Subject: subject,
                From: new From(new User("user-id", "Test User", "user", "tenant-id")),
                MessageType: "message",
                Body: new Body("html", message),
                CreatedDateTime: DateTimeOffset.UtcNow,
                LastModifiedDateTime: DateTimeOffset.UtcNow,
                LastEditedDateTime: null);

            var jsonResponse = JsonSerializer.Serialize(responseMessage, new JsonSerializerOptions(JsonSerializerOptions.Web));

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent(jsonResponse)
                });

            // Act
            await _teamsPlugin.PostMessageToChannel(subject, message);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest.Headers.Authorization);
            Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
            Assert.Equal(TestAccessToken, capturedRequest.Headers.Authorization.Parameter);
        }

        [Fact]
        public async Task GetMessagesFromChannel_IncludesAuthorizationHeader()
        {
            // Arrange
            HttpRequestMessage? capturedRequest = null;
            var messages = new List<TeamsChannelMessage>
            {
                new TeamsChannelMessage(
                    Id: "msg-1",
                    WebUrl: "https://teams.microsoft.com/msg1",
                    Subject: "Message 1",
                    From: new From(new User("user-1", "User One", "user", "tenant-id")),
                    MessageType: "message",
                    Body: new Body("html", "<p>First message</p>"),
                    CreatedDateTime: DateTimeOffset.UtcNow,
                    LastModifiedDateTime: DateTimeOffset.UtcNow,
                    LastEditedDateTime: null)
            };

            var response = new { value = messages };
            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerOptions.Web));

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });

            // Act
            await _teamsPlugin.GetMessagesFromChannel();

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest.Headers.Authorization);
            Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
            Assert.Equal(TestAccessToken, capturedRequest.Headers.Authorization.Parameter);
        }

        [Fact]
        public async Task ReplyToTeamsMessageAsync_WithValidInput_ReturnsPostMessageResult()
        {
            // Arrange
            var messageId = "msg-123";
            var subject = "Follow-up";
            var body = "<p>Reply body</p>";
            var expectedReplyId = "reply-456";
            var expectedWebUrl = "https://teams.microsoft.com/reply";

            var responseMessage = new TeamsChannelMessage(
                Id: expectedReplyId,
                WebUrl: expectedWebUrl,
                Subject: subject,
                From: new From(new User("user-id", "Test User", "user", "tenant-id")),
                MessageType: "message",
                Body: new Body("html", body),
                CreatedDateTime: DateTimeOffset.UtcNow,
                LastModifiedDateTime: DateTimeOffset.UtcNow,
                LastEditedDateTime: null);

            var jsonResponse = JsonSerializer.Serialize(responseMessage, new JsonSerializerOptions(JsonSerializerOptions.Web));

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().Contains($"v2/beta/teams/{TestGroupId}/channels/{TestChannelId}/messages/{messageId}/replies")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent(jsonResponse)
                });

            // Act
            var result = await _teamsPlugin.ReplyToTeamsMessageAsync(messageId, body, subject);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedReplyId, result.Id);
            Assert.Equal(expectedWebUrl, result.WebUrl);
        }

        [Fact]
        public async Task ReplyToTeamsMessageAsync_WithEmptyMessageId_ThrowsArgumentException()
        {
            // Arrange
            var body = "<p>Reply body</p>";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _teamsPlugin.ReplyToTeamsMessageAsync(" ", body));
        }

        [Fact]
        public async Task ReplyToTeamsMessageAsync_WithEmptyBody_ThrowsArgumentException()
        {
            // Arrange
            var messageId = "msg-123";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _teamsPlugin.ReplyToTeamsMessageAsync(messageId, ""));
        }

        [Fact]
        public async Task ReplyToTeamsMessageAsync_WhenHttpRequestFails_ThrowsHttpRequestException()
        {
            // Arrange
            var messageId = "msg-123";
            var body = "<p>Reply body</p>";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("Bad Request")
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                _teamsPlugin.ReplyToTeamsMessageAsync(messageId, body));

            Assert.Contains("Failed to reply to Teams message", exception.Message);
            Assert.Contains("BadRequest", exception.Message);
        }

        [Fact]
        public async Task ReplyToTeamsMessageAsync_IncludesAuthorizationHeader()
        {
            // Arrange
            var messageId = "msg-123";
            var body = "<p>Reply body</p>";
            HttpRequestMessage? capturedRequest = null;

            var responseMessage = new TeamsChannelMessage(
                Id: "reply-123",
                WebUrl: "https://teams.microsoft.com/reply",
                Subject: "Follow-up",
                From: new From(new User("user-id", "Test User", "user", "tenant-id")),
                MessageType: "message",
                Body: new Body("html", body),
                CreatedDateTime: DateTimeOffset.UtcNow,
                LastModifiedDateTime: DateTimeOffset.UtcNow,
                LastEditedDateTime: null);

            var jsonResponse = JsonSerializer.Serialize(responseMessage, new JsonSerializerOptions(JsonSerializerOptions.Web));

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent(jsonResponse)
                });

            // Act
            await _teamsPlugin.ReplyToTeamsMessageAsync(messageId, body);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest.Headers.Authorization);
            Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
            Assert.Equal(TestAccessToken, capturedRequest.Headers.Authorization.Parameter);
        }
    }
}
