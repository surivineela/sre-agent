// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Configuration;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Agent.Web.Controllers.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Moq;
using Xunit;

namespace Agent.Tests.Unit.Controllers;

public class McpConnectionControllerTests
{
    private readonly Mock<IMcpConnectionEventManager> _mockConnectionManager;
    private readonly Mock<ILogger<McpConnectionController>> _mockLogger;
    private readonly Mock<IOptions<MCPSettings>> _mockSettings;
    private readonly McpConnectionController _controller;

    public McpConnectionControllerTests()
    {
        _mockConnectionManager = new Mock<IMcpConnectionEventManager>();
        _mockLogger = new Mock<ILogger<McpConnectionController>>();
        _mockSettings = new Mock<IOptions<MCPSettings>>();
        _mockSettings.Setup(s => s.Value).Returns(new MCPSettings { Enabled = true });
        _controller = new McpConnectionController(_mockConnectionManager.Object, _mockLogger.Object, _mockSettings.Object);
    }

    [Fact]
    public void ListConnections_ReturnsAllConnections()
    {
        // Arrange
        var connections = new List<McpConnection>
        {
            CreateTestConnection("connection1", true),
            CreateTestConnection("connection2", false)
        };

        _mockConnectionManager
            .Setup(m => m.GetActiveConnections())
            .Returns(connections);

        // Act
        var result = _controller.ListConnections();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responses = Assert.IsAssignableFrom<IEnumerable<McpConnectionResponse>>(okResult.Value);
        Assert.Equal(2, responses.Count());
    }

    [Fact]
    public void ListConnections_NoConnections_ReturnsEmptyList()
    {
        // Arrange
        _mockConnectionManager
            .Setup(m => m.GetActiveConnections())
            .Returns(new List<McpConnection>());

        // Act
        var result = _controller.ListConnections();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responses = Assert.IsAssignableFrom<IEnumerable<McpConnectionResponse>>(okResult.Value);
        Assert.Empty(responses);
    }

    [Fact]
    public void ListConnections_Exception_ReturnsInternalServerError()
    {
        // Arrange
        _mockConnectionManager
            .Setup(m => m.GetActiveConnections())
            .Throws(new Exception("Internal error"));

        // Act
        var result = _controller.ListConnections();

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetConnection_ExistingConnection_ReturnsOk()
    {
        // Arrange
        var connectionId = "test-connection";
        var connection = CreateTestConnection(connectionId, true);

        _mockConnectionManager
            .Setup(m => m.GetConnectionAsync(connectionId))
            .ReturnsAsync(connection);

        // Act
        var result = await _controller.GetConnection(connectionId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<McpConnectionResponse>(okResult.Value);
        Assert.Equal(connectionId, response.Name);
    }

    [Fact]
    public async Task GetConnection_NonExistentConnection_ReturnsNotFound()
    {
        // Arrange
        var connectionId = "non-existent";

        _mockConnectionManager
            .Setup(m => m.GetConnectionAsync(connectionId))
            .ReturnsAsync((McpConnection?)null);

        // Act
        var result = await _controller.GetConnection(connectionId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task GetConnection_Exception_ReturnsInternalServerError()
    {
        // Arrange
        var connectionId = "test-connection";

        _mockConnectionManager
            .Setup(m => m.GetConnectionAsync(connectionId))
            .ThrowsAsync(new Exception("Internal error"));

        // Act
        var result = await _controller.GetConnection(connectionId);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    #region Test Connection Tests

    [Fact(Skip = "PingAsync is an extension method and cannot be mocked with Moq")]
    public async Task TestConnection_ExistingConnection_ReturnsSuccessResult()
    {
        // Arrange
        var connectionId = "test-connection";
        var mockClient = new Mock<McpClient>();
        mockClient.Setup(c => c.PingAsync(default))
            .Returns(Task.CompletedTask);

        var connection = CreateTestConnection(connectionId, true, client: mockClient.Object);

        _mockConnectionManager
            .Setup(m => m.GetConnectionAsync(connectionId))
            .ReturnsAsync(connection);

        // Act
        var result = await _controller.TestConnection(connectionId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<McpConnectionTestResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Ping successful", response.Message);
        Assert.True(response.ResponseTimeMs >= 0);
    }

    [Fact]
    public async Task TestConnection_NonExistentConnection_ReturnsNotFound()
    {
        // Arrange
        var connectionId = "non-existent";

        _mockConnectionManager
            .Setup(m => m.GetConnectionAsync(connectionId))
            .ReturnsAsync((McpConnection?)null);

        // Act
        var result = await _controller.TestConnection(connectionId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact(Skip = "PingAsync is an extension method and cannot be mocked with Moq")]
    public async Task TestConnection_PingFails_ReturnsFailureResult()
    {
        // Arrange
        var connectionId = "test-connection";
        var mockClient = new Mock<McpClient>();
        mockClient.Setup(c => c.PingAsync(default))
            .ThrowsAsync(new Exception("Connection timeout"));

        var connection = CreateTestConnection(connectionId, true, client: mockClient.Object);

        _mockConnectionManager
            .Setup(m => m.GetConnectionAsync(connectionId))
            .ReturnsAsync(connection);

        // Act
        var result = await _controller.TestConnection(connectionId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<McpConnectionTestResponse>(okResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Ping failed", response.Message);
        Assert.NotNull(response.Error);
    }

    [Fact]
    public async Task TestConnection_ClientNotInitialized_ReturnsFailureResult()
    {
        // Arrange
        var connectionId = "test-connection";
        // Don't provide a client - it will be null by default
        var connection = CreateTestConnection(connectionId, true);

        _mockConnectionManager
            .Setup(m => m.GetConnectionAsync(connectionId))
            .ReturnsAsync(connection);

        // Act
        var result = await _controller.TestConnection(connectionId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<McpConnectionTestResponse>(okResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Connection client is not initialized", response.Message);
    }

    #endregion

    // Helper methods
    private McpConnection CreateTestConnection(string name, bool isSse, IList<AITool>? tools = null, McpClient? client = null, McpAuthenticationConfig? authConfig = null)
    {
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockBackend = new Mock<IMcpConnectable>();

        IClientTransport transport;
        McpConnectionMetadata metadata;

        if (isSse)
        {
            transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri("http://localhost:5000/sse"),
                Name = name,  // Set the name to control the connection ID
                TransportMode = HttpTransportMode.StreamableHttp
            });

            metadata = new McpConnectionMetadata
            {
                Type = "sse",  // Preserve "sse" type for backward compatibility
                Endpoint = "http://localhost:5000/sse"
            };
        }
        else
        {
            transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = "node",
                Arguments = new[] { "server.js" },
                Name = name  // Set the name to control the connection ID
            });

            metadata = new McpConnectionMetadata
            {
                Type = "stdio",
                Command = "node",
                Arguments = new[] { "server.js" }
            };
        }

        var mockLogger = new Mock<ILogger>();
        var connection = new McpConnection(mockLogger.Object, transport)
        {
            Backend = mockBackend.Object,
            Authentication = authConfig,
            Metadata = metadata
        };

        // Use reflection to set Tools if provided (for testing purposes)
        if (tools != null)
        {
            var toolsProperty = typeof(McpConnection).GetProperty("Tools");
            toolsProperty?.SetValue(connection, tools);
        }

        // Use reflection to set Client if provided (for testing purposes)
        if (client != null)
        {
            var clientProperty = typeof(McpConnection).GetProperty("Client");
            clientProperty?.SetValue(connection, client);
        }

        return connection;
    }

    private Microsoft.Extensions.AI.AITool CreateAIFunction(string name, string description)
    {
        return AIFunctionFactory.Create(
            () => "test result",
            name,
            description);
    }
}
