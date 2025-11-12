// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Agent.Runtime.Services.Mcp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using Moq;
using Xunit;

namespace Agent.Tests.Unit.Services;

public class McpAgentManagementServiceTests
{
    private readonly Mock<IMcpConnectionEventManager> _mockConnectionManager;
    private readonly Mock<IToolFactory<AgentContext>> _mockToolFactory;
    private readonly Mock<IAgentFactory<AgentContext>> _mockAgentFactory;
    private readonly Mock<IMcpConnectable> _mockMcpToolsRepository;
    private readonly Mock<IOptions<MCPSettings>> _mockSettings;
    private readonly Mock<ILogger<McpAgentManagementService>> _mockLogger;
    private readonly MCPSettings _settings;

    public McpAgentManagementServiceTests()
    {
        _mockConnectionManager = new Mock<IMcpConnectionEventManager>();
        _mockToolFactory = new Mock<IToolFactory<AgentContext>>();
        _mockAgentFactory = new Mock<IAgentFactory<AgentContext>>();
        _mockMcpToolsRepository = new Mock<IMcpConnectable>();
        _settings = new MCPSettings { Enabled = true, PingIntervalInSeconds = 60 };
        _mockSettings = new Mock<IOptions<MCPSettings>>();
        _mockSettings.Setup(s => s.Value).Returns(_settings);
        _mockLogger = new Mock<ILogger<McpAgentManagementService>>();
    }

    [Fact]
    public async Task StartAsync_SubscribesToEvents()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _mockConnectionManager.VerifyAdd(m => m.ConnectionAdded += It.IsAny<Func<McpConnection, Task>>(), Times.Once);
        _mockConnectionManager.VerifyAdd(m => m.ConnectionRemoved += It.IsAny<Func<string, Task>>(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_McpDisabled_DoesNotSubscribeToEvents()
    {
        // Arrange
        var disabledSettings = new MCPSettings { Enabled = false, PingIntervalInSeconds = 60 };
        var mockDisabledSettings = new Mock<IOptions<MCPSettings>>();
        mockDisabledSettings.Setup(s => s.Value).Returns(disabledSettings);

        var service = new McpAgentManagementService(
            _mockConnectionManager.Object,
            _mockToolFactory.Object,
            _mockAgentFactory.Object,
            _mockMcpToolsRepository.Object,
            mockDisabledSettings.Object,
            _mockLogger.Object);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _mockConnectionManager.VerifyAdd(m => m.ConnectionAdded += It.IsAny<Func<McpConnection, Task>>(), Times.Never);
        _mockConnectionManager.VerifyAdd(m => m.ConnectionRemoved += It.IsAny<Func<string, Task>>(), Times.Never);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromEvents()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockConnectionManager.VerifyRemove(m => m.ConnectionAdded -= It.IsAny<Func<McpConnection, Task>>(), Times.Once);
        _mockConnectionManager.VerifyRemove(m => m.ConnectionRemoved -= It.IsAny<Func<string, Task>>(), Times.Once);
    }

    [Fact]
    public async Task OnConnectionAdded_RefreshesToolFactory()
    {
        // Arrange
        var service = CreateService();
        var connection = CreateTestConnection("test-server");

        _mockToolFactory
            .Setup(f => f.RefreshMcpToolsAsync())
            .Returns(Task.CompletedTask);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Trigger the event by invoking the subscribed handler
        var connectionAddedEvent = GetConnectionAddedHandler(service);
        await connectionAddedEvent(connection);

        // Assert
        _mockToolFactory.Verify(f => f.RefreshMcpToolsAsync(), Times.Once);
    }

    [Fact]
    public async Task OnConnectionRemoved_RefreshesToolFactory()
    {
        // Arrange
        var service = CreateService();
        var connectionId = "test-server";

        _mockToolFactory
            .Setup(f => f.RefreshMcpToolsAsync())
            .Returns(Task.CompletedTask);

        // Act
        await service.StartAsync(CancellationToken.None);

        var connectionRemovedEvent = GetConnectionRemovedHandler(service);
        await connectionRemovedEvent(connectionId);

        // Assert
        _mockToolFactory.Verify(f => f.RefreshMcpToolsAsync(), Times.Once);
    }

    [Fact]
    public async Task OnConnectionAdded_WithNullTools_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        var connection = CreateTestConnection("test-server");
        // Tools will be null by default since we don't set them

        _mockToolFactory
            .Setup(f => f.RefreshMcpToolsAsync())
            .Returns(Task.CompletedTask);

        // Act
        await service.StartAsync(CancellationToken.None);

        var connectionAddedEvent = GetConnectionAddedHandler(service);

        // Should not throw
        await connectionAddedEvent(connection);

        // Assert
        _mockToolFactory.Verify(f => f.RefreshMcpToolsAsync(), Times.Once);
    }

    [Fact]
    public async Task MultipleConnections_RefreshesToolsMultipleTimes()
    {
        // Arrange
        var service = CreateService();
        var connection1 = CreateTestConnection("server1");
        var connection2 = CreateTestConnection("server2");

        _mockToolFactory
            .Setup(f => f.RefreshMcpToolsAsync())
            .Returns(Task.CompletedTask);

        // Act
        await service.StartAsync(CancellationToken.None);

        var connectionAddedEvent = GetConnectionAddedHandler(service);
        await connectionAddedEvent(connection1);
        await connectionAddedEvent(connection2);

        // Assert
        _mockToolFactory.Verify(f => f.RefreshMcpToolsAsync(), Times.Exactly(2));
    }

    // Helper methods
    private McpAgentManagementService CreateService()
    {
        return new McpAgentManagementService(
            _mockConnectionManager.Object,
            _mockToolFactory.Object,
            _mockAgentFactory.Object,
            _mockMcpToolsRepository.Object,
            _mockSettings.Object,
            _mockLogger.Object);
    }

    private McpConnection CreateTestConnection(string name)
    {
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockBackend = new Mock<IMcpConnectable>();

        var httpOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:5000/sse"),
            TransportMode = HttpTransportMode.StreamableHttp
        };

        var transport = new HttpClientTransport(httpOptions);

        var mockLogger = new Mock<ILogger>();
        return new McpConnection(mockLogger.Object, transport)
        {
            Backend = mockBackend.Object
        };
    }

    private Func<McpConnection, Task> GetConnectionAddedHandler(McpAgentManagementService service)
    {
        // This is a simplified approach - in real tests you might need reflection
        // or make the handlers testable through dependency injection
        Func<McpConnection, Task>? handler = null;
        _mockConnectionManager
            .SetupAdd(m => m.ConnectionAdded += It.IsAny<Func<McpConnection, Task>>())
            .Callback<Func<McpConnection, Task>>(h => handler = h);

        service.StartAsync(CancellationToken.None).Wait();
        return handler ?? throw new InvalidOperationException("Handler not set");
    }

    private Func<string, Task> GetConnectionRemovedHandler(McpAgentManagementService service)
    {
        Func<string, Task>? handler = null;
        _mockConnectionManager
            .SetupAdd(m => m.ConnectionRemoved += It.IsAny<Func<string, Task>>())
            .Callback<Func<string, Task>>(h => handler = h);

        service.StartAsync(CancellationToken.None).Wait();
        return handler ?? throw new InvalidOperationException("Handler not set");
    }
}
