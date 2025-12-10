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
}
