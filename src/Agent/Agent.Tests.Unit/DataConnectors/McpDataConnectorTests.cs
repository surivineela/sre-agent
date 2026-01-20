// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
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
    private readonly Mock<IToolFactory<AgentContext>> _toolFactoryMock = new();

    private McpDataConnector CreateConnector()
    {
        return new McpDataConnector(_loggerMock.Object, _connectionManagerMock.Object, _toolFactoryMock.Object);
    }

    [Fact]
    public async Task InitAsync_WithStructuredDataSource_ParsesAllFields()
    {
        // Arrange
        var settings = new DataConnectorInstanceSettings
        {
            Name = "structured-mcp",
            DataConnectorType = "Mcp",
            DataSource = "Endpoint=https://api.example.com/mcp;AuthType=BearerToken;BearerToken=secret;ServiceType=ExampleService"
        };

        McpAuthenticationConfig? capturedAuth = null;

        _connectionManagerMock
            .Setup(m => m.CreateAndAddConnectionAsync(
                It.IsAny<string>(),
                It.IsAny<McpTransportType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<McpAuthenticationConfig?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()))
            .Callback<string, McpTransportType, string?, string?, string[]?, string?, McpAuthenticationConfig?, Dictionary<string, string>?, string?, string?, Dictionary<string, string>?, string?, bool>((name, type, endpoint, command, arguments, workingDirectory, authConfig, headers, description, serviceType, envVars, identity, useLocalStdio) =>
            {
                capturedAuth = authConfig;
            })
            .ReturnsAsync(CreateConnection());

        McpDataConnector connector = CreateConnector();

        // Act
        await connector.InitAsync(settings, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAuth);
        Assert.Equal(McpAuthenticationType.Bearer, capturedAuth!.Type);
        Assert.Equal("secret", capturedAuth.BearerToken);
    }

    [Fact]
    public async Task InitAsync_WithCustomHeadersAuthType_ParsesCustomHeaders()
    {
        // Arrange
        var settings = new DataConnectorInstanceSettings
        {
            Name = "custom-headers-mcp",
            DataConnectorType = "Mcp",
            DataSource = "Endpoint=https://api.example.com/mcp;AuthType=CustomHeaders;Custom-Header=Value;X-API-Key=secret123"
        };

        McpAuthenticationConfig? capturedAuth = null;

        _connectionManagerMock
            .Setup(m => m.CreateAndAddConnectionAsync(
                It.IsAny<string>(),
                It.IsAny<McpTransportType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<McpAuthenticationConfig?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()))
            .Callback<string, McpTransportType, string?, string?, string[]?, string?, McpAuthenticationConfig?, Dictionary<string, string>?, string?, string?, Dictionary<string, string>?, string?, bool>((name, type, endpoint, command, arguments, workingDirectory, authConfig, headers, description, serviceType, envVars, identity, useLocalStdio) =>
            {
                capturedAuth = authConfig;
            })
            .ReturnsAsync(CreateConnection());

        McpDataConnector connector = CreateConnector();

        // Act
        await connector.InitAsync(settings, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAuth);
        Assert.Equal(McpAuthenticationType.CustomHeaders, capturedAuth!.Type);
        Assert.NotNull(capturedAuth.CustomHeaders);
        Assert.Equal(2, capturedAuth.CustomHeaders.Count);
        Assert.True(capturedAuth.CustomHeaders.TryGetValue("Custom-Header", out string? customHeaderValue));
        Assert.Equal("Value", customHeaderValue);
        Assert.True(capturedAuth.CustomHeaders.TryGetValue("X-API-Key", out string? apiKeyValue));
        Assert.Equal("secret123", apiKeyValue);
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

    [Fact]
    public async Task InitAsync_WithExtendedProperties_HttpTransport_Success()
    {
        // Arrange
        var extendedProperties = CreateExtendedProperties(new
        {
            Type = "Http",
            Endpoint = "https://api.example.com/mcp",
            AuthType = "BearerToken",
            BearerToken = "test-token-123"
        });

        var settings = new DataConnectorInstanceSettings
        {
            Name = "extended-http-mcp",
            DataConnectorType = "Mcp",
            DataSource = "placeholder",
            ExtendedProperties = extendedProperties
        };

        string? capturedEndpoint = null;
        McpAuthenticationConfig? capturedAuth = null;

        _connectionManagerMock
            .Setup(m => m.CreateAndAddConnectionAsync(
                It.IsAny<string>(),
                It.IsAny<McpTransportType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<McpAuthenticationConfig?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()))
            .Callback<string, McpTransportType, string?, string?, string[]?, string?, McpAuthenticationConfig?, Dictionary<string, string>?, string?, string?, Dictionary<string, string>?, string?, bool>((name, type, endpoint, command, arguments, workingDirectory, authConfig, headers, description, serviceType, envVars, identity, useLocalStdio) =>
            {
                capturedEndpoint = endpoint;
                capturedAuth = authConfig;
            })
            .ReturnsAsync(CreateConnection());

        McpDataConnector connector = CreateConnector();

        // Act
        await connector.InitAsync(settings, CancellationToken.None);

        // Assert
        Assert.Equal("https://api.example.com/mcp", capturedEndpoint);
        Assert.NotNull(capturedAuth);
        Assert.Equal(McpAuthenticationType.Bearer, capturedAuth!.Type);
        Assert.Equal("test-token-123", capturedAuth.BearerToken);
    }

    [Fact]
    public async Task InitAsync_WithExtendedProperties_StdioTransport_Success()
    {
        // Arrange
        var extendedProperties = CreateExtendedProperties(new
        {
            Type = "Stdio",
            Command = "npx",
            Args = new[] { "mcp-server", "--port", "3000" },
            Envs = new Dictionary<string, string> { ["NODE_ENV"] = "production" }
        });

        var settings = new DataConnectorInstanceSettings
        {
            Name = "extended-stdio-mcp",
            DataConnectorType = "Mcp",
            DataSource = "placeholder",
            ExtendedProperties = extendedProperties
        };

        string? capturedCommand = null;
        string[]? capturedArguments = null;
        Dictionary<string, string>? capturedEnvVars = null;

        _connectionManagerMock
            .Setup(m => m.CreateAndAddConnectionAsync(
                It.IsAny<string>(),
                It.IsAny<McpTransportType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<McpAuthenticationConfig?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()))
            .Callback<string, McpTransportType, string?, string?, string[]?, string?, McpAuthenticationConfig?, Dictionary<string, string>?, string?, string?, Dictionary<string, string>?, string?, bool>((name, type, endpoint, command, arguments, workingDirectory, authConfig, headers, description, serviceType, envVars, identity, useLocalStdio) =>
            {
                capturedCommand = command;
                capturedArguments = arguments;
                capturedEnvVars = envVars;
            })
            .ReturnsAsync(CreateConnection());

        McpDataConnector connector = CreateConnector();

        // Act
        await connector.InitAsync(settings, CancellationToken.None);

        // Assert
        Assert.Equal("npx", capturedCommand);
        Assert.NotNull(capturedArguments);
        Assert.Equal(3, capturedArguments!.Length);
        Assert.Equal("mcp-server", capturedArguments[0]);
        Assert.Equal("--port", capturedArguments[1]);
        Assert.Equal("3000", capturedArguments[2]);
        Assert.NotNull(capturedEnvVars);
        Assert.Single(capturedEnvVars!);
        Assert.Equal("production", capturedEnvVars["NODE_ENV"]);
    }

    [Fact]
    public async Task InitAsync_WithExtendedProperties_TakesPrecedenceOverDataSource()
    {
        // Arrange
        var extendedProperties = CreateExtendedProperties(new
        {
            type = "http",
            endpoint = "https://extended.example.com/mcp"
        });

        var settings = new DataConnectorInstanceSettings
        {
            Name = "precedence-test-mcp",
            DataConnectorType = "Mcp",
            DataSource = "Endpoint=https://datasource.example.com/mcp",
            ExtendedProperties = extendedProperties
        };

        string? capturedEndpoint = null;

        _connectionManagerMock
            .Setup(m => m.CreateAndAddConnectionAsync(
                It.IsAny<string>(),
                It.IsAny<McpTransportType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<McpAuthenticationConfig?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()))
            .Callback<string, McpTransportType, string?, string?, string[]?, string?, McpAuthenticationConfig?, Dictionary<string, string>?, string?, string?, Dictionary<string, string>?, string?, bool>((name, type, endpoint, command, arguments, workingDirectory, authConfig, headers, description, serviceType, envVars, identity, useLocalStdio) =>
            {
                capturedEndpoint = endpoint;
            })
            .ReturnsAsync(CreateConnection());

        McpDataConnector connector = CreateConnector();

        // Act
        await connector.InitAsync(settings, CancellationToken.None);

        // Assert - ExtendedProperties should be used, not DataSource
        Assert.Equal("https://extended.example.com/mcp", capturedEndpoint);
    }

    [Fact]
    public async Task InitAsync_WithExtendedPropertiesJson_HttpTransport_Success()
    {
        // Arrange
        var json = """
            {
                "type": "http",
                "endpoint": "https://api.example.com/mcp",
                "authType": "BearerToken",
                "bearerToken": "json-token-456"
            }
            """;

        var settings = new DataConnectorInstanceSettings
        {
            Name = "json-http-mcp",
            DataConnectorType = "Mcp",
            DataSource = "placeholder",
            ExtendedPropertiesJson = json,
            // Simulate what RegisterDataConnectors does - parse JSON to ExtendedProperties
            ExtendedProperties = ParseJsonToExtendedProperties(json)
        };

        string? capturedEndpoint = null;
        McpAuthenticationConfig? capturedAuth = null;

        _connectionManagerMock
            .Setup(m => m.CreateAndAddConnectionAsync(
                It.IsAny<string>(),
                It.IsAny<McpTransportType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<McpAuthenticationConfig?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()))
            .Callback<string, McpTransportType, string?, string?, string[]?, string?, McpAuthenticationConfig?, Dictionary<string, string>?, string?, string?, Dictionary<string, string>?, string?, bool>((name, type, endpoint, command, arguments, workingDirectory, authConfig, headers, description, serviceType, envVars, identity, useLocalStdio) =>
            {
                capturedEndpoint = endpoint;
                capturedAuth = authConfig;
            })
            .ReturnsAsync(CreateConnection());

        McpDataConnector connector = CreateConnector();

        // Act
        await connector.InitAsync(settings, CancellationToken.None);

        // Assert
        Assert.Equal("https://api.example.com/mcp", capturedEndpoint);
        Assert.NotNull(capturedAuth);
        Assert.Equal(McpAuthenticationType.Bearer, capturedAuth!.Type);
        Assert.Equal("json-token-456", capturedAuth.BearerToken);
    }

    [Fact]
    public async Task InitAsync_WithExtendedPropertiesJson_StdioTransport_Success()
    {
        // Arrange
        var json = """
            {
                "type": "stdio",
                "command": "node",
                "args": ["server.js", "--verbose"],
                "envs": {
                    "NODE_ENV": "development",
                    "DEBUG": "true"
                }
            }
            """;

        var settings = new DataConnectorInstanceSettings
        {
            Name = "json-stdio-mcp",
            DataConnectorType = "Mcp",
            DataSource = "placeholder",
            ExtendedPropertiesJson = json,
            // Simulate what RegisterDataConnectors does - parse JSON to ExtendedProperties
            ExtendedProperties = ParseJsonToExtendedProperties(json)
        };

        string? capturedCommand = null;
        string[]? capturedArguments = null;
        Dictionary<string, string>? capturedEnvVars = null;

        _connectionManagerMock
            .Setup(m => m.CreateAndAddConnectionAsync(
                It.IsAny<string>(),
                It.IsAny<McpTransportType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<McpAuthenticationConfig?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()))
            .Callback<string, McpTransportType, string?, string?, string[]?, string?, McpAuthenticationConfig?, Dictionary<string, string>?, string?, string?, Dictionary<string, string>?, string?, bool>((name, type, endpoint, command, arguments, workingDirectory, authConfig, headers, description, serviceType, envVars, identity, useLocalStdio) =>
            {
                capturedCommand = command;
                capturedArguments = arguments;
                capturedEnvVars = envVars;
            })
            .ReturnsAsync(CreateConnection());

        McpDataConnector connector = CreateConnector();

        // Act
        await connector.InitAsync(settings, CancellationToken.None);

        // Assert
        Assert.Equal("node", capturedCommand);
        Assert.NotNull(capturedArguments);
        Assert.Equal(2, capturedArguments!.Length);
        Assert.Equal("server.js", capturedArguments[0]);
        Assert.Equal("--verbose", capturedArguments[1]);
        Assert.NotNull(capturedEnvVars);
        Assert.Equal(2, capturedEnvVars!.Count);
        Assert.Equal("development", capturedEnvVars["NODE_ENV"]);
        Assert.Equal("true", capturedEnvVars["DEBUG"]);
    }

    [Fact]
    public async Task InitAsync_WithExtendedPropertiesJson_TakesPrecedenceOverExtendedProperties()
    {
        // Arrange
        var extendedProperties = CreateExtendedProperties(new
        {
            type = "http",
            endpoint = "https://properties.example.com/mcp"
        });

        var json = """
            {
                "type": "http",
                "endpoint": "https://json.example.com/mcp"
            }
            """;

        var settings = new DataConnectorInstanceSettings
        {
            Name = "json-precedence-test-mcp",
            DataConnectorType = "Mcp",
            DataSource = "Endpoint=https://datasource.example.com/mcp",
            ExtendedPropertiesJson = json,
            // Simulate what RegisterDataConnectors does - parse JSON to ExtendedProperties
            // This will overwrite the extendedProperties set above, demonstrating the precedence
            ExtendedProperties = ParseJsonToExtendedProperties(json)
        };

        string? capturedEndpoint = null;

        _connectionManagerMock
            .Setup(m => m.CreateAndAddConnectionAsync(
                It.IsAny<string>(),
                It.IsAny<McpTransportType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<McpAuthenticationConfig?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()))
            .Callback<string, McpTransportType, string?, string?, string[]?, string?, McpAuthenticationConfig?, Dictionary<string, string>?, string?, string?, Dictionary<string, string>?, string?, bool>((name, type, endpoint, command, arguments, workingDirectory, authConfig, headers, description, serviceType, envVars, identity, useLocalStdio) =>
            {
                capturedEndpoint = endpoint;
            })
            .ReturnsAsync(CreateConnection());

        McpDataConnector connector = CreateConnector();

        // Act
        await connector.InitAsync(settings, CancellationToken.None);

        // Assert - ExtendedPropertiesJson should be used, not ExtendedProperties or DataSource
        Assert.Equal("https://json.example.com/mcp", capturedEndpoint);
    }

    [Fact]
    public async Task InitAsync_WithExtendedPropertiesJson_WithSpecialCharactersInKeys_Success()
    {
        // Arrange
        var json = """
            {
                "type": "http",
                "endpoint": "https://api.example.com/mcp",
                "authType": "CustomHeaders",
                "X-API-Key": "secret123",
                "Custom-Header": "custom-value"
            }
            """;

        var settings = new DataConnectorInstanceSettings
        {
            Name = "special-chars-mcp",
            DataConnectorType = "Mcp",
            DataSource = "placeholder",
            ExtendedPropertiesJson = json,
            // Simulate what RegisterDataConnectors does - parse JSON to ExtendedProperties
            ExtendedProperties = ParseJsonToExtendedProperties(json)
        };

        McpAuthenticationConfig? capturedAuth = null;

        _connectionManagerMock
            .Setup(m => m.CreateAndAddConnectionAsync(
                It.IsAny<string>(),
                It.IsAny<McpTransportType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<McpAuthenticationConfig?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>()))
            .Callback<string, McpTransportType, string?, string?, string[]?, string?, McpAuthenticationConfig?, Dictionary<string, string>?, string?, string?, Dictionary<string, string>?, string?, bool>((name, type, endpoint, command, arguments, workingDirectory, authConfig, headers, description, serviceType, envVars, identity, useLocalStdio) =>
            {
                capturedAuth = authConfig;
            })
            .ReturnsAsync(CreateConnection());

        McpDataConnector connector = CreateConnector();

        // Act
        await connector.InitAsync(settings, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedAuth);
        Assert.Equal(McpAuthenticationType.CustomHeaders, capturedAuth!.Type);
        Assert.NotNull(capturedAuth.CustomHeaders);
        Assert.Equal(2, capturedAuth.CustomHeaders.Count);
        Assert.True(capturedAuth.CustomHeaders.ContainsKey("X-API-Key"));
        Assert.Equal("secret123", capturedAuth.CustomHeaders["X-API-Key"]);
        Assert.True(capturedAuth.CustomHeaders.ContainsKey("Custom-Header"));
        Assert.Equal("custom-value", capturedAuth.CustomHeaders["Custom-Header"]);
    }

    private static Dictionary<string, JsonElement> CreateExtendedProperties(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, JsonElement>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.Clone();
        }
        return result;
    }

    /// <summary>
    /// Simulates what DataConnectorRegistrationExtensions.ParseJsonToExtendedProperties does.
    /// </summary>
    private static Dictionary<string, JsonElement> ParseJsonToExtendedProperties(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, JsonElement>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.Clone();
        }
        return result;
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
