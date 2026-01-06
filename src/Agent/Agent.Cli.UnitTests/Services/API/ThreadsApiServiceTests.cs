// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Agent.Cli.Services;
using Agent.Core.Models.Api.v1;
using Moq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Cli.UnitTests.Services;

public class ThreadsApiServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ThreadsApiServiceTests(ITestOutputHelper output)
    {
        _output = output;

        var mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(mockHttpHandler.Object);
    }

    // CreateThreadAsync Tests
    [Fact]
    public async Task CreateThreadAsync_InvalidConfig_Test()
    {
        var invalidConfigPath = Path.Join(Path.GetTempPath(), "non-existent-path");
        var configService = new TestCliConfigurationService(invalidConfigPath);

        var apiService = new ApiService(_httpClient, configService, new Mock<ITokenService>().Object);

        (var thread, var error) = await apiService.CreateThreadAsync("Test Thread", "This is a test thread.", "User");

        _output.WriteLine(error);

        thread.ShouldBeNull();
        error.ShouldNotBeNull();
        error.ShouldContain("Configuration not found. Please run 'srectl init' first.");
    }

    [Fact]
    public async Task CreateThreadAsync_ValidConfig_Test()
    {
        // Arrange
        var id = Guid.NewGuid();
        var message = "This is a test thread.";
        var userId = "userId";
        var displayName = "displayName";
        var statusCode = HttpStatusCode.OK;

        var thread = new Thread(
            Id: id,
            Title: "TestTitle",
            StartMessage: new Message(
                    Id: Guid.NewGuid(),
                    Author: new Author(
                        Role: Role.User,
                        UserId: "userId",
                        DisplayName: "displayName"
                        ),
                    Text: "hello",
                    TimeStamp: DateTime.UtcNow
                ),
            LastMessage: null,
            CreatedTimestamp: DateTime.UtcNow,
            ModifiedTimestamp: DateTime.UtcNow,
            FeatureConfig: null);

        var handler = TestHelpers.CreateMockHttpMessageHandler(statusCode, JsonSerializer.Serialize(thread, _jsonOptions));
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(handler);

        (var resultThread, var error) = await apiService.CreateThreadAsync(message, userId, displayName);

        _output.WriteLine(error ?? "Success");

        resultThread.ShouldNotBeNull();
        resultThread.Id.ShouldBe(thread.Id.ToString());
        error.ShouldBeNull();
    }

    [Fact]
    public async Task CreateThreadAsync_HttpError_Test()
    {
        // Arrange
        var statusCode = HttpStatusCode.InternalServerError;
        var handler = TestHelpers.CreateMockHttpMessageHandler(statusCode, string.Empty);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(handler);

        (var thread, var error) = await apiService.CreateThreadAsync("Test Thread", "This is a test thread.", "User");

        thread.ShouldBeNull();
        error.ShouldNotBeNull();
        error.ShouldContain($"{statusCode}");
    }

    // SendMessageAsync Tests
    [Fact]
    public async Task SendMessageAsync_InvalidConfig_Test()
    {
        var invalidConfigPath = Path.Join(Path.GetTempPath(), "non-existent-path");
        var configService = new TestCliConfigurationService(invalidConfigPath);

        var apiService = new ApiService(_httpClient, configService, new Mock<ITokenService>().Object);

        (var threadMessage, var error) = await apiService.SendThreadMessageAsync(
                    threadId: "1234",
                    message: "This is a test thread.",
                    userId: "UserId",
                    displayName: "User");

        threadMessage.ShouldBeNull();
        error.ShouldNotBeNull();
        error.ShouldContain("Configuration not found. Please run 'srectl init' first.");
    }

    [Fact]
    public async Task SendMessageAsync_ValidConfig_Test()
    {
        var messageId = Guid.NewGuid();
        var statusCode = HttpStatusCode.OK;
        var message = new Message(
                Id: messageId,
                Author: new Author(
                    Role: Role.User,
                    UserId: "UserId",
                    DisplayName: "User"
                ),
                Text: "This is a test message.",
                TimeStamp: DateTime.UtcNow
        );

        var handler = TestHelpers.CreateMockHttpMessageHandler(statusCode, JsonSerializer.Serialize(message, _jsonOptions));
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(handler);

        var (threadMessage, error) = await apiService.SendThreadMessageAsync(
                    threadId: "1234",
                    message: "This is a test thread.",
                    userId: "UserId",
                    displayName: "User");

        threadMessage.ShouldNotBeNull();
        threadMessage.Id.ShouldBe(messageId.ToString());
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "")]
    [InlineData(HttpStatusCode.InternalServerError, "The request cannot be processed at this time.")]
    public async Task SendMessageAsync_InvalidHttpResponse_Test(HttpStatusCode statusCode, string responseContent)
    {
        var handler = TestHelpers.CreateMockHttpMessageHandler(statusCode, responseContent);
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(handler);

        var (threadMessage, error) = await apiService.SendThreadMessageAsync(
                    threadId: "1234",
                    message: "This is a test thread.",
                    userId: "UserId",
                    displayName: "User"
            );

        threadMessage.ShouldBeNull();
        error.ShouldNotBeNull();
        // The generic MakeHttpRequestAsync returns "Request failed" or "Unexpected response format" for error cases
        (error.Contains("Request failed") || error.Contains("Unexpected response format")).ShouldBeTrue();
    }

    // TrackThreadAsync Tests
    [Fact]
    public async Task TrackThreadAsync_InvalidConfig_Test()
    {
        var invalidConfigPath = Path.Join(Path.GetTempPath(), "non-existent-path");
        var configService = new TestCliConfigurationService(invalidConfigPath);

        var apiService = new ApiService(_httpClient, configService, new Mock<ITokenService>().Object);

        (var success, var messages, var response) = await apiService.TrackThreadAsync(
                    threadId: "1234");

        success.ShouldBeFalse();
        messages.ShouldBeEmpty();
        response.ShouldContain("Configuration not found. Please run 'srectl init' first.");
    }

    [Fact]
    public async Task TrackThreadAsync_ValidConfig_Test()
    {
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var statusCode = HttpStatusCode.OK;
        var userMessage = new Message(
                Id: messageId,
                Author: new Author(
                    Role: Role.User,
                    UserId: "UserId",
                    DisplayName: "User"
                ),
                Text: "This is a test message.",
                TimeStamp: DateTime.UtcNow
        );

        var sreagentMessage = new Message(
                Id: messageId,
                Author: new Author(
                    Role: Role.SREAgent,
                    UserId: "sreagent",
                    DisplayName: "SREAgent"
                ),
                Text: "Hi",
                TimeStamp: DateTime.UtcNow
        );

        var messages = new PagedResponseWithState<Message, ContextStateEnum?>(
            [userMessage, sreagentMessage]);

        var handler = TestHelpers.CreateMockHttpMessageHandler(statusCode, JsonSerializer.Serialize(messages, _jsonOptions));
        var apiService = TestHelpers.CreateApiServiceWithMockedHttp(handler);

        var (Success, Messages, Response) = await apiService.TrackThreadAsync(threadId.ToString());

        _output.WriteLine(Response);

        Success.ShouldBeTrue();
        Messages.Count.ShouldBe(2);

        Response.ShouldBe($"Thread tracking complete");
    }
}
