// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Agent.Runtime.Services.Mcp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Agent.Tests.Unit.Services;

/// <summary>
/// Unit tests for McpConnectionEventManager.
/// </summary>
public class McpConnectionEventManagerTests
{

    private readonly Mock<ILogger<McpConnectionEventManager>> _mockLogger;
    private readonly Mock<IMcpConnectable> _mockBackend;
    private readonly Mock<IMcpAuthenticationService> _mockAuthService;
    private readonly Mock<IAuthenticationService> _mockCoreAuthService;
    private readonly Mock<IOptions<MCPSettings>> _mockMcpSettings;
    private readonly McpConnectionEventManager _eventManager;
    private readonly Mock<ISessionTransportFactory> _mockSessionTransportFactory;

    public McpConnectionEventManagerTests()
    {
        // Set required environment variable for tests
        Environment.SetEnvironmentVariable("AGENT_NAME", "test-agent");

        _mockLogger = new Mock<ILogger<McpConnectionEventManager>>();
        _mockBackend = new Mock<IMcpConnectable>();
        _mockAuthService = new Mock<IMcpAuthenticationService>();
        _mockCoreAuthService = new Mock<IAuthenticationService>();
        _mockMcpSettings = new Mock<IOptions<MCPSettings>>();
        _mockMcpSettings.Setup(s => s.Value).Returns(new MCPSettings
        {
            Enabled = true,
            PingIntervalInSeconds = 60,
            PingTimeoutInSeconds = 5
        });
        _mockSessionTransportFactory = new Mock<ISessionTransportFactory>();

        _eventManager = new McpConnectionEventManager(
            _mockBackend.Object,
            _mockAuthService.Object,
            _mockCoreAuthService.Object,
            _mockSessionTransportFactory.Object,
            _mockMcpSettings.Object,
            _mockLogger.Object);
    }

    #region Basic Validation Tests - These don't initialize connections

    [Fact]
    public async Task CreateAndAddConnectionAsync_HttpMissingEndpoint_ThrowsArgumentException()
    {
        // Arrange
        var name = "test-connection";

        // Act & Assert - Http transport requires endpoint
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _eventManager.CreateAndAddConnectionAsync(
                name,
                McpTransportType.Http,
                null, // Missing endpoint
                null,
                null,
                null));
    }


    [Fact]
    public async Task CreateAndAddConnectionAsync_StdioMissingCommand_ThrowsArgumentException()
    {
        // Arrange
        var name = "test-connection";

        // Act & Assert - Stdio transport requires command
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _eventManager.CreateAndAddConnectionAsync(
                name,
                McpTransportType.Stdio,
                null,
                null, // Missing command
                null,
                null));
    }

    [Fact]
    public async Task GetConnectionAsync_NonExistentConnection_ReturnsNull()
    {
        // Act
        var connection = await _eventManager.GetConnectionAsync("non-existent");

        // Assert
        Assert.Null(connection);
    }

    [Fact]
    public async Task RemoveConnectionAsync_NonExistentConnection_ReturnsFalse()
    {
        // Act
        var result = await _eventManager.RemoveConnectionAsync("non-existent");

        // Assert
        Assert.False(result);
    }

    #endregion
}

