// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.Tools;
using Agent.Framework;
using Agent.Plugins.Connector;
using Agent.Plugins.Kusto;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Plugins.Implementation;

public class KustoPluginExecuteLocalFunctionTests : IDisposable
{
    private readonly string _queriesDirectory;
    private readonly List<string> _createdFiles = new();
    private readonly Mock<IAgentOutboundCommunicationService> _outboundMock;
    private readonly ILogger<KustoPlugin> _logger;

    public KustoPluginExecuteLocalFunctionTests()
    {
        _queriesDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins", "Definitions", "Queries");
        Directory.CreateDirectory(_queriesDirectory);

        _outboundMock = new Mock<IAgentOutboundCommunicationService>();
        _outboundMock
            .Setup(o => o.UpdateThreadWithAgentMessageAsync(
                It.IsAny<Guid?>(),
                It.IsAny<ChatMessage>(),
                It.IsAny<Guid?>(),
                It.IsAny<StreamMessageType?>()))
            .Returns(Task.CompletedTask);
        _outboundMock
            .Setup(o => o.HandleAgentTaskKustoResult(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _logger = Mock.Of<ILogger<KustoPlugin>>();
        Agent.Core.ToolStatic.AsyncLocalThreadId.Value = Guid.Empty;
    }

    public void Dispose()
    {
        foreach (var file in _createdFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup failures to avoid impacting unrelated tests.
            }
        }
    }

    [Fact]
    public async Task ExecuteLocalFunctionOnClusterAsync_UsesKqlFileWhenPresent()
    {
        // Arrange
        var functionName = $"FileBackedFunction_{Guid.NewGuid():N}";
        var filePath = CreateKqlFile(functionName, "print ##Value##");

        using var plugin = CreatePlugin();
        var args = new Dictionary<string, string> { ["Value"] = "42" };
        Agent.Core.ToolStatic.AsyncLocalThreadId.Value = Guid.NewGuid();

        // Act
        var result = await plugin.ExecuteLocalFunctionOnClusterAsync(
            functionName,
            "testCluster",
            "testDb",
            args,
            toolDefinition: new KustoToolDefinition
            {
                Name = "Test Tool",
                Function = functionName,
                Query = "print fallback ##Value##"
            });

        // Assert
        // Result now contains the rich formatted message (tool name + query result message)
        Assert.Contains("query-result", result);
        Assert.Contains(functionName, result); // function name is included
        Assert.Equal("print 42", plugin.CapturedQuery);
        Assert.Equal("testCluster", plugin.CapturedCluster);
        Assert.Equal("testDb", plugin.CapturedDatabase);
        _outboundMock.Verify(o => o.UpdateThreadWithAgentMessageAsync(
            It.IsAny<Guid?>(),
            It.IsAny<ChatMessage>(),
            It.IsAny<Guid?>(),
            It.IsAny<StreamMessageType?>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteLocalFunctionOnClusterAsync_FallsBackToInlineQueryWhenFileMissing()
    {
        // Arrange
        var functionName = $"MissingFunction_{Guid.NewGuid():N}";
        using var plugin = CreatePlugin();
        var args = new Dictionary<string, string> { ["Value"] = "99" };
        var toolDefinition = new KustoToolDefinition
        {
            Name = "Inline Tool",
            Function = functionName,
            Query = "print inline ##Value##"
        };
        Agent.Core.ToolStatic.AsyncLocalThreadId.Value = Guid.NewGuid();

        // Act
        var result = await plugin.ExecuteLocalFunctionOnClusterAsync(
            functionName,
            "cluster",
            "database",
            args,
            toolDefinition: toolDefinition);

        // Assert
        // Result now contains the rich formatted message (tool name + query result message)
        Assert.Contains("query-result", result);
        Assert.Contains(functionName, result); // function name is included
        Assert.Equal("print inline 99", plugin.CapturedQuery);
        _outboundMock.Verify(o => o.UpdateThreadWithAgentMessageAsync(
            It.IsAny<Guid?>(),
            It.IsAny<ChatMessage>(),
            It.IsAny<Guid?>(),
            It.IsAny<StreamMessageType?>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteLocalFunctionOnClusterAsync_ThrowsWhenFileMissingAndFallbackNotProvided()
    {
        // Arrange
        var functionName = $"MissingNoFallback_{Guid.NewGuid():N}";
        using var plugin = CreatePlugin();
        Agent.Core.ToolStatic.AsyncLocalThreadId.Value = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => plugin.ExecuteLocalFunctionOnClusterAsync(
            functionName,
            "cluster",
            "database",
            new Dictionary<string, string>(),
            toolDefinition: new KustoToolDefinition
            {
                Function = functionName
            }));

        Assert.Contains(functionName, ex.Message);
        Assert.Null(plugin.CapturedQuery);
        _outboundMock.Verify(o => o.UpdateThreadWithAgentMessageAsync(
            It.IsAny<Guid?>(),
            It.IsAny<ChatMessage>(),
            It.IsAny<Guid?>(),
            It.IsAny<StreamMessageType?>()), Times.Never);
    }

    private string CreateKqlFile(string functionName, string content)
    {
        var filePath = Path.Combine(_queriesDirectory, $"{functionName}.kql");
        File.WriteAllText(filePath, content);
        _createdFiles.Add(filePath);
        return filePath;
    }

    private TestKustoPlugin CreatePlugin()
    {
        var kustoClient = new KustoClient(
            Mock.Of<ILogger<KustoClient>>(),
            new KustoConnector(),
            Mock.Of<IAuthenticationService>());

        return new TestKustoPlugin(_logger, kustoClient, _outboundMock.Object);
    }

    private sealed class TestKustoPlugin : KustoPlugin, IDisposable
    {
        public string? CapturedQuery { get; private set; }
        public string? CapturedCluster { get; private set; }
        public string? CapturedDatabase { get; private set; }

        public TestKustoPlugin(ILogger<KustoPlugin> logger, KustoClient kustoClient, IAgentOutboundCommunicationService agentOutboundCommunicationService)
            : base(logger, kustoClient, agentOutboundCommunicationService)
        {
        }

        public override Task<KustoQueryResult> ExecuteClusterKustoQueryInternal(string cluster, string database, string fullQuery)
        {
            CapturedCluster = cluster;
            CapturedDatabase = database;
            CapturedQuery = fullQuery;

            var message = new ChatMessage(ChatRole.Tool, "query-result");
            return Task.FromResult(new KustoQueryResult(1, fullQuery, "QUERY_RESULT", message));
        }

        public void Dispose()
        {
            // No unmanaged resources, but support using pattern for symmetry with test setup.
        }
    }
}
