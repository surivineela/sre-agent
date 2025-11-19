// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Services.Mcp;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Services.Mcp;

/// <summary>
/// Unit tests for SessionWebsocketClientTransport.
/// These tests verify the transport logic without requiring a real MCP proxy server.
/// </summary>
public class SessionWebocketClientTests
{
    private readonly Mock<ILogger> _mockLogger;

    public SessionWebocketClientTests()
    {
        _mockLogger = new Mock<ILogger>();
    }

    private static SessionWebsocketClientOptions CreateValidOptions() => new()
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
        var transport = new SessionWebsocketClientTransport(options, _mockLogger.Object);

        // Assert
        Assert.NotNull(transport);
        Assert.Equal("SessionPool-npx", transport.Name);
    }

    [Theory]
    [InlineData(null, "npx", new[] { "arg" }, "options")]
    [InlineData("ws://localhost/run", null, new[] { "arg" }, "options")]
    [InlineData("ws://localhost/run", "npx", null, "options")]
    public void Constructor_WithInvalidOptions_ThrowsArgumentException(
        string? serverUrl, string? command, string[]? args, string expectedParamName)
    {
        // Arrange
        var options = new SessionWebsocketClientOptions
        {
            ServerUrl = serverUrl!,
            Command = command!,
            Arguments = args!
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new SessionWebsocketClientTransport(options, _mockLogger.Object));
        Assert.Equal(expectedParamName, exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SessionWebsocketClientTransport(options, null!));
        Assert.Equal("logger", exception.ParamName);
    }

    [Theory]
    [InlineData("npx", null, "SessionPool-npx")]
    [InlineData("uvx", "CustomName", "CustomName")]
    public void Name_WithOrWithoutCustomName_ReturnsCorrectValue(
        string command, string? customName, string expectedName)
    {
        // Arrange
        var options = new SessionWebsocketClientOptions
        {
            ServerUrl = "ws://localhost:5000/run",
            Command = command,
            Arguments = new[] { "test" },
            Name = customName
        };

        // Act
        var transport = new SessionWebsocketClientTransport(options, _mockLogger.Object);

        // Assert
        Assert.Equal(expectedName, transport.Name);
    }

    [Fact]
    public void Constructor_WithAzureCredential_Succeeds()
    {
        // Arrange
        var mockCredential = new Mock<TokenCredential>();
        var options = new SessionWebsocketClientOptions
        {
            ServerUrl = "wss://session-pool.azure.com/run",
            Command = "npx",
            Arguments = new[] { "test" },
            Credential = mockCredential.Object
        };

        // Act
        var transport = new SessionWebsocketClientTransport(options, _mockLogger.Object);

        // Assert
        Assert.NotNull(transport);
    }
}
