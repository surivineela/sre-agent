// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Plugins.Implementation;
using Agent.Plugins.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Agent.Tests.Unit.Plugins.Implementation;

public class ToolOutputRetrieverPluginTests
{
    private readonly Mock<IAgentFileStorageService> _mockStorage;
    private readonly Mock<IChatClientProvider> _mockChatClientProvider;
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<ILogger<ToolOutputRetrieverPlugin>> _mockLogger;
    private readonly Mock<IOptions<ToolOutputSettings>> _mockToolOutputSettings;
    private readonly ToolOutputRetrieverPlugin _plugin;
    private readonly string _testDataPath;

    public ToolOutputRetrieverPluginTests()
    {
        _mockStorage = new Mock<IAgentFileStorageService>();
        _mockChatClientProvider = new Mock<IChatClientProvider>();
        _mockChatClient = new Mock<IChatClient>();
        _mockLogger = new Mock<ILogger<ToolOutputRetrieverPlugin>>();
        _mockToolOutputSettings = new Mock<IOptions<ToolOutputSettings>>();

        _mockToolOutputSettings.Setup(s => s.Value).Returns(new ToolOutputSettings { MaxOutputChars = 16000 });
        _mockChatClientProvider.Setup(p => p.LargeContextModel).Returns(_mockChatClient.Object);
        _plugin = new ToolOutputRetrieverPlugin(_mockStorage.Object, _mockChatClientProvider.Object, _mockLogger.Object, _mockToolOutputSettings.Object);

        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "ToolOutputRetrieve");
    }

    [Fact]
    public async Task RetrieveToolOutputAsync_ReadByLine_ReturnsCorrectLines()
    {
        // Arrange
        var fileKey = "sample-logs.txt";
        var filePath = Path.Combine(_testDataPath, "sample-logs.txt");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "read_by_line",
            LineStart = 1,
            LineEnd = 5
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("<content line_start=\"1\" line_end=\"5\">", result);
        Assert.Contains("VSTS agentless phase log", result);
        Assert.Contains("Rollout Details", result);
    }

    [Fact]
    public async Task RetrieveToolOutputAsync_ReadByOffset_ReturnsCorrectBytes()
    {
        // Arrange
        var fileKey = "sample-logs.txt";
        var filePath = Path.Combine(_testDataPath, "sample-logs.txt");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "read_by_offset",
            OffsetStart = 0,
            OffsetEnd = 50
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("<content offset_start=\"0\" offset_end=\"50\">", result);
        Assert.Contains("</content>", result);
    }

    [Fact]
    public async Task RetrieveToolOutputAsync_Summarize_CallsChatCompletion()
    {
        // Arrange
        var fileKey = "sample-logs.txt";
        var filePath = Path.Combine(_testDataPath, "sample-logs.txt");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var expectedSummary = "This is a summary of the logs.";
        var chatResponse = new ChatResponse(
            new List<ChatMessage> { new ChatMessage(ChatRole.Assistant, expectedSummary) });

        _mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "summarize",
            SummaryPrompt = "Summarize errors"
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("<summary>", result);
        Assert.Contains(expectedSummary, result);
        Assert.Contains("</summary>", result);
        _mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveToolOutputAsync_FilterStructured_Json_ReturnsFilteredData()
    {
        // Arrange
        var fileKey = "sample-data.json";
        var filePath = Path.Combine(_testDataPath, "sample-data.json");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "filter_structured",
            JmesPath = "[0].type"
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("<result>", result);
        Assert.Contains("Container", result);
        Assert.Contains("</result>", result);
    }

    [Fact]
    public async Task RetrieveToolOutputAsync_FilterStructured_Yaml_ReturnsFilteredData()
    {
        // Arrange
        var fileKey = "sample-config.yaml";
        var filePath = Path.Combine(_testDataPath, "sample-config.yaml");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "filter_structured",
            JmesPath = "server.ssl.enabled"
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("<result>", result);
        Assert.Contains("true", result.ToLower());
        Assert.Contains("</result>", result);
    }

    [Fact]
    public async Task RetrieveToolOutputAsync_SearchRegex_ReturnsMatches()
    {
        // Arrange
        var fileKey = "sample-logs.txt";
        var filePath = Path.Combine(_testDataPath, "sample-logs.txt");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "search_regex",
            RegexPattern = "Status: (Running|Accepted|Succeeded)",
            RegexMaxMatches = 10
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("Total matches:", result);
        Assert.Contains("<match", result);
    }

    [Fact]
    public async Task FilterStructured_Json_ComplexJmesPath_ReturnsFilteredArray()
    {
        // Arrange
        var fileKey = "sample-data.json";
        var filePath = Path.Combine(_testDataPath, "sample-data.json");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "filter_structured",
            JmesPath = "[?lineCount > `1000`].{id: id, lines: lineCount, type: type}"
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("<result>", result);
        Assert.Contains("id", result);
        Assert.Contains("lines", result);
        Assert.Contains("type", result);
        Assert.Contains("</result>", result);
    }

    [Fact]
    public async Task FilterStructured_Json_ArrayLength_ReturnsNumber()
    {
        // Arrange
        var fileKey = "sample-data.json";
        var filePath = Path.Combine(_testDataPath, "sample-data.json");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "filter_structured",
            JmesPath = "length([*])"
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        // Should return a number wrapped in result tags
        Assert.Contains("<result>", result);
        Assert.Contains("</result>", result);
        var numberStr = result.Replace("<result>", "").Replace("</result>", "").Trim();
        Assert.True(int.TryParse(numberStr, out var count));
        Assert.True(count > 0);
    }

    [Fact]
    public async Task FilterStructured_Yaml_NestedPath_ReturnsString()
    {
        // Arrange
        var fileKey = "sample-config.yaml";
        var filePath = Path.Combine(_testDataPath, "sample-config.yaml");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "filter_structured",
            JmesPath = "database.primary.host"
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("<result>", result);
        Assert.Contains("prod-sql-01.database.windows.net", result);
        Assert.Contains("</result>", result);
    }

    [Fact]
    public async Task FilterStructured_Yaml_ArrayAccess_ReturnsArrayElement()
    {
        // Arrange
        var fileKey = "sample-config.yaml";
        var filePath = Path.Combine(_testDataPath, "sample-config.yaml");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "filter_structured",
            JmesPath = "server.ssl.protocols[0]"
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("<result>", result);
        Assert.Contains("TLSv1.2", result);
        Assert.Contains("</result>", result);
    }

    [Fact]
    public async Task FilterStructured_Yaml_NumberValue_ReturnsNumber()
    {
        // Arrange
        var fileKey = "sample-config.yaml";
        var filePath = Path.Combine(_testDataPath, "sample-config.yaml");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "filter_structured",
            JmesPath = "server.port"
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("<result>", result);
        Assert.Contains("8080", result);
        Assert.Contains("</result>", result);
    }

    [Fact]
    public async Task FilterStructured_Yaml_ComplexObject_ReturnsYaml()
    {
        // Arrange
        var fileKey = "sample-config.yaml";
        var filePath = Path.Combine(_testDataPath, "sample-config.yaml");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "filter_structured",
            JmesPath = "monitoring.metrics"
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert - When returning complex object, it should be serialized as YAML wrapped in result tags
        Assert.Contains("<result>", result);
        Assert.Contains("</result>", result);
        // Should not contain the error pattern "valueKind"
        Assert.DoesNotContain("valueKind", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FilterStructured_MissingJmesPath_ReturnsError()
    {
        // Arrange
        var fileKey = "sample-data.json";
        var filePath = Path.Combine(_testDataPath, "sample-data.json");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "filter_structured",
            JmesPath = null
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.StartsWith("<error>", result);
        Assert.Contains("jmesPath is required", result);
    }

    [Fact]
    public async Task SearchRegex_WithMaxMatches_RespectsLimit()
    {
        // Arrange
        var fileKey = "sample-logs.txt";
        var filePath = Path.Combine(_testDataPath, "sample-logs.txt");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "search_regex",
            RegexPattern = "Status: (Running|Accepted)",
            RegexMaxMatches = 3
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("Total matches: 3", result);
        Assert.Contains("<match", result);
    }

    [Fact]
    public async Task SearchRegex_WithCaptureGroups_ReturnsGroups()
    {
        // Arrange
        var fileKey = "sample-logs.txt";
        var filePath = Path.Combine(_testDataPath, "sample-logs.txt");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "search_regex",
            RegexPattern = @"ServiceResource: ([^,]+), Action: ([^,]+)",
            RegexMaxMatches = 5
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("Total matches:", result);
        Assert.Contains("<match", result);
        Assert.Contains("column=", result);
    }

    [Fact]
    public async Task SearchRegex_NoMatches_ReturnsZeroMatches()
    {
        // Arrange
        var fileKey = "sample-logs.txt";
        var filePath = Path.Combine(_testDataPath, "sample-logs.txt");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "search_regex",
            RegexPattern = "NONEXISTENT_PATTERN_12345",
            RegexMaxMatches = 10
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.Contains("No matches found", result);
    }

    [Fact]
    public async Task SearchRegex_MissingPattern_ReturnsError()
    {
        // Arrange
        var fileKey = "sample-logs.txt";
        var filePath = Path.Combine(_testDataPath, "sample-logs.txt");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "search_regex",
            RegexPattern = null
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.StartsWith("<error>", result);
        Assert.Contains("</error>", result);
    }

    [Fact]
    public async Task SearchRegex_CaseInsensitive_FindsMatches()
    {
        // Arrange
        var fileKey = "sample-logs.txt";
        var filePath = Path.Combine(_testDataPath, "sample-logs.txt");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "search_regex",
            RegexPattern = "status:",
            RegexFlags = "i",  // Case insensitive flag
            RegexMaxMatches = 5
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert - should find "Status:" (capital S) because of the 'i' flag
        Assert.Contains("Total matches:", result);
        Assert.Contains("<match", result);
    }

    [Fact]
    public async Task RetrieveToolOutputAsync_InvalidOperation_ReturnsError()
    {
        // Arrange
        var fileKey = "sample-logs.txt";
        var filePath = Path.Combine(_testDataPath, "sample-logs.txt");
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(filePath);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "invalid_op"
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.StartsWith("<error>", result);
        Assert.Contains("Unknown operation", result);
    }

    [Fact]
    public async Task RetrieveToolOutputAsync_FileNotFound_ReturnsError()
    {
        // Arrange
        var fileKey = "non-existent.txt";
        _mockStorage.Setup(s => s.GetToolOutputAsync(fileKey, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var options = new ToolOutputRetrieverOptions
        {
            FileKey = fileKey,
            Operation = "read_by_line"
        };

        // Act
        var result = await _plugin.RetrieveToolOutputAsync(options);

        // Assert
        Assert.StartsWith("<error>", result);
        Assert.Contains("not found in storage", result);
    }

}
