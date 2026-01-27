// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Agent.Runtime.Services.Mcp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Moq;

namespace Agent.Tests.Unit.Services;

/// <summary>
/// Test implementation of AIFunction that allows controlling the return value.
/// </summary>
file class TestAIFunction : AIFunction
{
    private readonly string _name;
    private readonly string _description;
    private object? _returnValue;

    public TestAIFunction(string name, string description)
    {
        _name = name;
        _description = description;
    }

    public override string Name => _name;
    public override string Description => _description;

    public void SetReturnValue(object? value)
    {
        _returnValue = value;
    }

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return new ValueTask<object?>(_returnValue);
    }
}

public class McpToolAIFunctionTests
{
    private readonly Mock<IMcpConnectionHealthService> _healthServiceMock = new();

    [Fact]
    public async Task InvokeCoreAsync_WithSingleTextContent_ReturnsText()
    {
        // Arrange
        var expectedText = "Hello, world!";
        var textContent = new TextContent(expectedText);

        var originalTool = new TestAIFunction("original_tool", "Test tool");
        originalTool.SetReturnValue(textContent);

        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool);
        var args = new AIFunctionArguments();

        // Act
        var result = await mcpTool.InvokeAsync(args, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<string>(result);
        Assert.Equal(expectedText, result);
    }

    [Fact]
    public async Task InvokeCoreAsync_WithMultipleTextContents_ReturnsJoinedText()
    {
        // Arrange
        var contents = new List<AIContent>
        {
            new TextContent("Line 1"),
            new TextContent("Line 2"),
            new TextContent("Line 3")
        };

        var originalTool = new TestAIFunction("original_tool", "Test tool");
        originalTool.SetReturnValue(contents);

        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool);
        var args = new AIFunctionArguments();

        // Act
        var result = await mcpTool.InvokeAsync(args, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<string>(result);
        Assert.Equal("Line 1\n------\nLine 2\n------\nLine 3", result);
    }

    [Fact]
    public async Task InvokeCoreAsync_WithMixedAIContentTypes_HandlesUnsupportedTypes()
    {
        // Arrange
        // DataContent requires a proper data URI format: data:[<mediatype>][;base64],<data>
        var dataUri = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var contents = new List<AIContent>
        {
            new TextContent("Text content"),
            new DataContent(dataUri, "image/png"),
            new TextContent("More text")
        };

        var originalTool = new TestAIFunction("original_tool", "Test tool");
        originalTool.SetReturnValue(contents);

        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool);
        var args = new AIFunctionArguments();

        // Act
        var result = await mcpTool.InvokeAsync(args, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<string>(result);
        var resultString = result as string;
        Assert.NotNull(resultString);
        Assert.Contains("Text content", resultString);
        Assert.Contains("<Unsupported", resultString); // Verify unsupported content marker (DataContent)
        Assert.Contains("More text", resultString);
    }

    [Fact]
    public async Task InvokeCoreAsync_WithJsonElementResult_ReturnsAsIs()
    {
        // Arrange
        var jsonElement = JsonDocument.Parse("{\"key\": \"value\"}").RootElement;

        var originalTool = new TestAIFunction("original_tool", "Test tool");
        originalTool.SetReturnValue(jsonElement);

        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool);
        var args = new AIFunctionArguments();

        // Act
        var result = await mcpTool.InvokeAsync(args, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<JsonElement>(result);
        var jsonResult = (JsonElement)result;
        Assert.Equal("value", jsonResult.GetProperty("key").GetString());
    }

    [Fact]
    public async Task InvokeCoreAsync_WithCallToolResultAsJsonElement_ReturnsAsIs()
    {
        // Arrange
        // Create a CallToolResult with both Content and StructuredContent
        var callToolResult = new CallToolResult
        {
            Content =
            [
                new TextContentBlock { Text = "Result text content" },
                new TextContentBlock { Text = "Additional content" }
            ],
            StructuredContent = JsonNode.Parse("{\"status\": \"success\", \"data\": {\"count\": 42}}")
        };

        // Serialize CallToolResult to JsonElement (simulating what MCP SDK might return)
        var json = JsonSerializer.Serialize(callToolResult);
        var jsonElement = JsonDocument.Parse(json).RootElement;

        var originalTool = new TestAIFunction("original_tool", "Test tool");
        originalTool.SetReturnValue(jsonElement);

        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool);
        var args = new AIFunctionArguments();

        // Act
        var result = await mcpTool.InvokeAsync(args, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<JsonElement>(result);
        var jsonResult = (JsonElement)result;

        // Verify the CallToolResult structure is preserved
        Assert.True(jsonResult.TryGetProperty("content", out var contentProperty));
        Assert.Equal(JsonValueKind.Array, contentProperty.ValueKind);
        Assert.Equal(2, contentProperty.GetArrayLength());

        Assert.True(jsonResult.TryGetProperty("structuredContent", out var structuredProperty));
        var structuredData = structuredProperty.GetProperty("data");
        Assert.Equal(42, structuredData.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task InvokeCoreAsync_WithCallToolResultWithErrorFlag_ReturnsAsIs()
    {
        // Arrange
        // Create a CallToolResult with IsError flag set
        var callToolResult = new CallToolResult
        {
            Content =
            [
                new TextContentBlock { Text = "Error: Operation failed" }
            ],
            IsError = true,
            StructuredContent = JsonNode.Parse("{\"errorCode\": \"INVALID_INPUT\", \"message\": \"Invalid parameter\"}")
        };

        // Serialize CallToolResult to JsonElement
        var json = JsonSerializer.Serialize(callToolResult);
        var jsonElement = JsonDocument.Parse(json).RootElement;

        var originalTool = new TestAIFunction("original_tool", "Test tool");
        originalTool.SetReturnValue(jsonElement);

        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool);
        var args = new AIFunctionArguments();

        // Act
        var result = await mcpTool.InvokeAsync(args, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<JsonElement>(result);
        var jsonResult = (JsonElement)result;

        // Verify the error structure is preserved
        Assert.True(jsonResult.TryGetProperty("isError", out var isErrorProperty));
        Assert.True(isErrorProperty.GetBoolean());

        Assert.True(jsonResult.TryGetProperty("structuredContent", out var structuredProperty));
        Assert.Equal("INVALID_INPUT", structuredProperty.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task InvokeCoreAsync_WithoutHealthService_InvokesOriginalTool()
    {
        // Arrange
        var expectedText = "Success";
        var textContent = new TextContent(expectedText);

        var originalTool = new TestAIFunction("original_tool", "Test tool");
        originalTool.SetReturnValue(textContent);

        // Create without health service
        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool, healthService: null);
        var args = new AIFunctionArguments();

        // Act
        var result = await mcpTool.InvokeAsync(args, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedText, result);
    }

    [Fact]
    public async Task InvokeCoreAsync_WithHealthyConnection_UsesOriginalTool()
    {
        // Arrange
        var expectedText = "Success";
        var textContent = new TextContent(expectedText);
        var connection = CreateMockConnection("test-connection");

        var originalTool = new TestAIFunction("original_tool", "Test tool");
        originalTool.SetReturnValue(textContent);

        _healthServiceMock
            .Setup(h => h.FindConnectionByToolSignature("connection_original_tool"))
            .Returns(connection);

        _healthServiceMock
            .Setup(h => h.ValidateConnectionHealthAsync(connection, "connection_original_tool"))
            .ReturnsAsync(connection); // Same connection returned = no reconnection

        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool, _healthServiceMock.Object);
        var args = new AIFunctionArguments();

        // Act
        var result = await mcpTool.InvokeAsync(args, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedText, result);
        _healthServiceMock.Verify(h => h.FindConnectionByToolSignature("connection_original_tool"), Times.Once);
        _healthServiceMock.Verify(h => h.ValidateConnectionHealthAsync(connection, "connection_original_tool"), Times.Once);
    }

    [Fact]
    public async Task InvokeCoreAsync_WithReconnectedConnection_UsesRefreshedTool()
    {
        // Arrange
        var expectedText = "Success after reconnection";
        var textContent = new TextContent(expectedText);

        var originalConnection = CreateMockConnection("test-connection");
        var reconnectedConnection = CreateMockConnection("test-connection");

        // Create refreshed tool
        var refreshedTool = new TestAIFunction("original_tool", "Refreshed test tool");
        refreshedTool.SetReturnValue(textContent);

        // Setup original tool (should not be invoked because it will be replaced)
        var originalTool = new TestAIFunction("original_tool", "Original test tool");
        originalTool.SetReturnValue(new TextContent("Should not see this"));

        // Setup reconnected connection with refreshed tool (using reflection since Tools has private setter)
        SetConnectionTools(reconnectedConnection, new List<AITool> { refreshedTool });

        _healthServiceMock
            .Setup(h => h.FindConnectionByToolSignature("connection_original_tool"))
            .Returns(originalConnection);

        _healthServiceMock
            .Setup(h => h.ValidateConnectionHealthAsync(originalConnection, "connection_original_tool"))
            .ReturnsAsync(reconnectedConnection); // Different connection returned = reconnection happened

        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool, _healthServiceMock.Object);
        var args = new AIFunctionArguments();

        // Act
        var result = await mcpTool.InvokeAsync(args, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedText, result);
        _healthServiceMock.Verify(h => h.FindConnectionByToolSignature("connection_original_tool"), Times.Once);
        _healthServiceMock.Verify(h => h.ValidateConnectionHealthAsync(originalConnection, "connection_original_tool"), Times.Once);
    }

    [Fact]
    public async Task InvokeCoreAsync_WithReconnectionButNoMatchingTool_UsesOriginalTool()
    {
        // Arrange
        var expectedText = "Fallback to original tool";
        var textContent = new TextContent(expectedText);

        var originalConnection = CreateMockConnection("test-connection");
        var reconnectedConnection = CreateMockConnection("test-connection");

        var originalTool = new TestAIFunction("original_tool", "Original test tool");
        originalTool.SetReturnValue(textContent);

        // Setup reconnected connection with no matching tools (using reflection since Tools has private setter)
        SetConnectionTools(reconnectedConnection, new List<AITool>());

        _healthServiceMock
            .Setup(h => h.FindConnectionByToolSignature("connection_original_tool"))
            .Returns(originalConnection);

        _healthServiceMock
            .Setup(h => h.ValidateConnectionHealthAsync(originalConnection, "connection_original_tool"))
            .ReturnsAsync(reconnectedConnection);

        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool, _healthServiceMock.Object);
        var args = new AIFunctionArguments();

        // Act
        var result = await mcpTool.InvokeAsync(args, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedText, result);
    }

    [Fact]
    public async Task InvokeCoreAsync_WithConnectionNotFound_UsesOriginalTool()
    {
        // Arrange
        var expectedText = "Success without health check";
        var textContent = new TextContent(expectedText);

        var originalTool = new TestAIFunction("original_tool", "Test tool");
        originalTool.SetReturnValue(textContent);

        _healthServiceMock
            .Setup(h => h.FindConnectionByToolSignature("connection_original_tool"))
            .Returns((McpConnection?)null); // Connection not found

        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool, _healthServiceMock.Object);
        var args = new AIFunctionArguments();

        // Act
        var result = await mcpTool.InvokeAsync(args, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedText, result);
        _healthServiceMock.Verify(h => h.FindConnectionByToolSignature("connection_original_tool"), Times.Once);
        _healthServiceMock.Verify(h => h.ValidateConnectionHealthAsync(It.IsAny<McpConnection>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Name_ReturnsNewName()
    {
        // Arrange
        var originalTool = new TestAIFunction("original_tool", "Test tool");
        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool);

        // Act & Assert
        Assert.Equal("connection_original_tool", mcpTool.Name);
    }

    [Fact]
    public void Description_ReturnsOriginalDescription()
    {
        // Arrange
        var expectedDescription = "This is a test tool";
        var originalTool = new TestAIFunction("original_tool", expectedDescription);
        var mcpTool = new McpToolAIFunction("connection_original_tool", originalTool);

        // Act & Assert
        Assert.Equal(expectedDescription, mcpTool.Description);
    }

    private static McpConnection CreateMockConnection(string connectionId)
    {
        var transportMock = new Mock<IClientTransport>();
        transportMock.SetupGet(t => t.Name).Returns(connectionId);

        var mockLogger = new Mock<ILogger>();
        return new McpConnection(mockLogger.Object, transportMock.Object)
        {
            Backend = Mock.Of<IMcpConnectable>()
        };
    }

    /// <summary>
    /// Helper method to set the Tools property on McpConnection using reflection,
    /// since it has a private setter.
    /// </summary>
    private static void SetConnectionTools(McpConnection connection, IList<AITool> tools)
    {
        var property = typeof(McpConnection).GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance);
        if (property != null)
        {
            property.SetValue(connection, tools);
        }
    }
}
