// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Runtime.DataConnectors;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Moq;

namespace Agent.Tests.Unit.DataConnectors;

public class McpDataConnectorTests
{
    private readonly Mock<ILogger<McpDataConnector>> _loggerMock = new();
    private readonly Mock<IMcpConnectionEventManager> _connectionManagerMock = new();

    private McpDataConnector CreateConnector()
    {
        return new McpDataConnector(_loggerMock.Object, _connectionManagerMock.Object);
    }

    [Fact]
    public async Task InitAsync_WithStructuredDataSource_ParsesAllFields()
    {
        // Arrange
        var settings = new DataConnectorInstanceSettings
        {
            Name = "structured-mcp",
            DataConnectorType = "Mcp",
            DataSource = "Endpoint=https://api.example.com/mcp;AuthType=BearerToken;BearerToken=secret;ServiceType=ExampleService;Custom-Header=Value"
        };

        string? capturedName = null;
        string? capturedType = null;
        string? capturedEndpoint = null;
        McpAuthenticationConfig? capturedAuth = null;
        Dictionary<string, string>? capturedHeaders = null;
        string? capturedDescription = null;
        string? capturedServiceType = null;

        _connectionManagerMock
            .Setup(m => m.CreateAndAddConnectionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<McpAuthenticationConfig?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Callback((string name,
                       string type,
                       string? endpoint,
                       string? command,
                       string[]? arguments,
                       string? workingDirectory,
                       McpAuthenticationConfig? authConfig,
                       Dictionary<string, string>? headers,
                       string? description,
                       string? serviceType) =>
            {
                capturedName = name;
                capturedType = type;
                capturedEndpoint = endpoint;
                capturedAuth = authConfig;
                capturedHeaders = headers;
                capturedDescription = description;
                capturedServiceType = serviceType;
            })
            .ReturnsAsync(CreateConnection());

        McpDataConnector connector = CreateConnector();

        // Act
        await connector.InitAsync(settings, CancellationToken.None);

        // Assert
        Assert.Equal(settings.Name, capturedName);
        Assert.Equal("http", capturedType);
        Assert.Equal("https://api.example.com/mcp", capturedEndpoint);

        Assert.NotNull(capturedAuth);
        Assert.Equal(McpAuthenticationType.Bearer, capturedAuth!.Type);
        Assert.Equal("secret", capturedAuth.BearerToken);

        Assert.NotNull(capturedHeaders);
        Assert.True(capturedHeaders!.TryGetValue("Custom-Header", out string? headerValue));
        Assert.Equal("Value", headerValue);

        //Assert.Equal("Example connection", capturedDescription);
        //Assert.Equal("ExampleService", capturedServiceType);
    }

    [Fact]
    public async Task InitAsync_WithBearerAuthMissingToken_Throws()
    {
        // Arrange
        var settings = new DataConnectorInstanceSettings
        {
            Name = "invalid-mcp",
            DataConnectorType = "Mcp",
            DataSource = "Endpoint=https://api.example.com;AuthType=BearerToken"
        };

        McpDataConnector connector = CreateConnector();

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => connector.InitAsync(settings, CancellationToken.None));
    }

    [Fact]
    public async Task InitAsync_WithUnsupportedAuthType_Throws()
    {
        // Arrange
        var settings = new DataConnectorInstanceSettings
        {
            Name = "unsupported-auth-mcp",
            DataConnectorType = "Mcp",
            DataSource = "Endpoint=https://api.example.com;AuthType=Basic"
        };

        McpDataConnector connector = CreateConnector();

        // Act / Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => connector.InitAsync(settings, CancellationToken.None));
    }

    private static McpConnection CreateConnection()
    {
        var transportMock = new Mock<IClientTransport>();
        transportMock.SetupGet(t => t.Name).Returns("test-transport");

        var mockLogger = new Mock<ILogger>();
        return new McpConnection(mockLogger.Object, transportMock.Object)
        {
            Backend = Mock.Of<IMcpConnectable>()
        };
    }
}
