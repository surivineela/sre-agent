// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Services.Mcp;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Agent.Tests.Unit.Services.Mcp;

/// <summary>
/// Unit tests for McpSessionWebsocketClient.
/// These tests verify the transport logic without requiring a real MCP proxy server.
/// </summary>
public class McpSessionWebsocketClientTests
{
    private readonly Mock<ILogger<McpSessionWebsocketClient>> _mockLogger;

    public McpSessionWebsocketClientTests()
    {
        _mockLogger = new Mock<ILogger<McpSessionWebsocketClient>>();
    }

    private static McpSessionWebsocketClientOptions CreateValidOptions() => new()
    {
        ServerUrl = "ws://localhost:5000/run",
        Command = "npx",
        Arguments = new[] { "-y", "@modelcontextprotocol/server-everything" }
    };

    [Fact]
    public void Constructor_WithValidOptions_InitializesCorrectly()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        var transport = new McpSessionWebsocketClient(options, _mockLogger.Object);

        // Assert
        Assert.NotNull(transport);
        Assert.Equal("SessionPool-npx", transport.Name);
        Assert.Null(transport.SessionId);
        Assert.NotNull(transport.MessageReader);
    }

    [Theory]
    [InlineData(null, "npx", new[] { "arg" }, "options")]
    [InlineData("ws://localhost/run", null, new[] { "arg" }, "options")]
    [InlineData("ws://localhost/run", "npx", null, "options")]
    public void Constructor_WithInvalidOptions_ThrowsArgumentException(
        string? serverUrl, string? command, string[]? args, string expectedParamName)
    {
        // Arrange
        var options = new McpSessionWebsocketClientOptions
        {
            ServerUrl = serverUrl!,
            Command = command!,
            Arguments = args!
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new McpSessionWebsocketClient(options, _mockLogger.Object));
        Assert.Equal(expectedParamName, exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new McpSessionWebsocketClient(options, null!));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public async Task SendAsync_BeforeConnect_ThrowsInvalidOperationException()
    {
        // Arrange
        var transport = new McpSessionWebsocketClient(CreateValidOptions(), _mockLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.SendAsync("test message"));
        Assert.Contains("not connected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_BeforeConnect_ThrowsInvalidOperationException()
    {
        // Arrange
        var transport = new McpSessionWebsocketClient(CreateValidOptions(), _mockLogger.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.StartAsync(_ => { }));
        Assert.Contains("not connected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var transport = new McpSessionWebsocketClient(CreateValidOptions(), _mockLogger.Object);

        // Act & Assert - should not throw
        transport.Dispose();
        transport.Dispose();
        await transport.DisposeAsync();
    }

    [Fact]
    public async Task AfterDispose_SendAsyncThrowsException()
    {
        // Arrange
        var transport = new McpSessionWebsocketClient(CreateValidOptions(), _mockLogger.Object);

        // Act
        await transport.DisposeAsync();

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.SendAsync("test"));
    }

    [Theory]
    [InlineData("npx", null, "SessionPool-npx")]
    [InlineData("uvx", "CustomName", "CustomName")]
    public void Name_WithOrWithoutCustomName_ReturnsCorrectValue(
        string command, string? customName, string expectedName)
    {
        // Arrange
        var options = new McpSessionWebsocketClientOptions
        {
            ServerUrl = "ws://localhost:5000/run",
            Command = command,
            Arguments = new[] { "test" },
            Name = customName
        };

        // Act
        var transport = new McpSessionWebsocketClient(options, _mockLogger.Object);

        // Assert
        Assert.Equal(expectedName, transport.Name);
    }

    [Fact]
    public void Constructor_WithAzureCredential_Succeeds()
    {
        // Arrange
        var mockCredential = new Mock<TokenCredential>();
        var options = new McpSessionWebsocketClientOptions
        {
            ServerUrl = "wss://session-pool.azure.com/run",
            Command = "npx",
            Arguments = new[] { "test" },
            Credential = mockCredential.Object
        };

        // Act
        var transport = new McpSessionWebsocketClient(options, _mockLogger.Object);

        // Assert
        Assert.NotNull(transport);
    }
}
