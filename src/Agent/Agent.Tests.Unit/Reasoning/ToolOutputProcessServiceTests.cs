// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Common;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace Agent.Tests.Unit.Reasoning;

/// <summary>
/// Unit tests for ToolOutputProcessService
/// </summary>
public class ToolOutputProcessServiceTests
{
    private readonly Mock<IThreadFileStorageService> _mockStorage;
    private readonly Mock<ILogger<ToolOutputProcessService>> _mockLogger;
    private readonly ToolOutputProcessService _service;

    public ToolOutputProcessServiceTests()
    {
        _mockStorage = new Mock<IThreadFileStorageService>();
        _mockLogger = new Mock<ILogger<ToolOutputProcessService>>();

        var settings = Options.Create(new ToolOutputSettings
        {
            MaxOutputChars = 1000,
        });

        _service = new ToolOutputProcessService(_mockStorage.Object, _mockLogger.Object, settings);
    }

    #region DetectContentType Tests

    [Fact]
    public void DetectContentType_ValidJson_ReturnsJson()
    {
        // Arrange
        var jsonContent = "{\"key\": \"value\"}";

        // Act
        var result = ToolOutputHelper.DetectContentType(jsonContent);

        // Assert
        result.ShouldBe("json");
    }

    [Fact]
    public void DetectContentType_ValidJsonArray_ReturnsJson()
    {
        // Arrange
        var jsonContent = "[1, 2, 3]";

        // Act
        var result = ToolOutputHelper.DetectContentType(jsonContent);

        // Assert
        result.ShouldBe("json");
    }

    [Fact]
    public void DetectContentType_InvalidJson_ReturnsTxt()
    {
        // Arrange
        var invalidJson = "{invalid json";

        // Act
        var result = ToolOutputHelper.DetectContentType(invalidJson);

        // Assert
        result.ShouldBe("txt");
    }

    [Fact]
    public void DetectContentType_ValidYaml_ReturnsYaml()
    {
        // Arrange
        var yamlContent = @"key: value
nested:
  item: test";

        // Act
        var result = ToolOutputHelper.DetectContentType(yamlContent);

        // Assert
        result.ShouldBe("yaml");
    }

    [Fact]
    public void DetectContentType_PlainText_ReturnsTxt()
    {
        // Arrange - Use special characters that won't parse as YAML
        var plainText = "This is just plain text: @#$% without any structure!!!";

        // Act
        var result = ToolOutputHelper.DetectContentType(plainText);

        // Assert
        result.ShouldBe("txt");
    }

    [Fact]
    public void DetectContentType_JsonWithWhitespace_ReturnsJson()
    {
        // Arrange
        var jsonWithWhitespace = "  \n  {\"key\": \"value\"}";

        // Act
        var result = ToolOutputHelper.DetectContentType(jsonWithWhitespace);

        // Assert
        result.ShouldBe("json");
    }

    #endregion

    #region GetPreviewContent Tests

    [Fact]
    public void GetPreviewContent_ShortContent_ReturnsFullContent()
    {
        // Arrange
        var content = "Line 1\nLine 2\nLine 3";

        // Act
        var result = ToolOutputHelper.GetPreviewContent(content);

        // Assert
        result.ShouldBe(content);
    }

    [Fact]
    public void GetPreviewContent_ExceedsMaxLines_TruncatesAtMaxLines()
    {
        // Arrange
        var lines = Enumerable.Range(1, 30).Select(i => $"Line {i}");
        var content = string.Join('\n', lines);

        // Act
        var result = ToolOutputHelper.GetPreviewContent(content);

        // Assert
        var resultLines = result.Split('\n');
        resultLines.Length.ShouldBeLessThanOrEqualTo(20);
        resultLines[0].ShouldBe("Line 1");
    }

    [Fact]
    public void GetPreviewContent_ExceedsMaxChars_TruncatesAtMaxChars()
    {
        // Arrange
        var longLine = new string('a', 5000);
        var content = $"Line 1\n{longLine}\nLine 3";

        // Act
        var result = ToolOutputHelper.GetPreviewContent(content);

        // Assert
        result.Length.ShouldBeLessThanOrEqualTo(4096 + 100); // Some buffer for line breaks
        result.ShouldStartWith("Line 1");
    }

    [Fact]
    public void GetPreviewContent_EmptyContent_ReturnsEmpty()
    {
        // Arrange
        var content = string.Empty;

        // Act
        var result = ToolOutputHelper.GetPreviewContent(content);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetPreviewContent_FirstLineExceedsMaxChars_TruncatesAtFirstLine()
    {
        // Arrange
        var firstLine = new string('x', 5000); // Exceeds 4096 char limit
        var content = $"{firstLine}\nLine 2\nLine 3";

        // Act
        var result = ToolOutputHelper.GetPreviewContent(content);

        // Assert
        result.Length.ShouldBe(4096); // Should truncate to exactly max chars
        result.ShouldBe(new string('x', 4096)); // Should be truncated first line
        result.ShouldNotContain("Line 2"); // Should stop before second line
    }

    #endregion

    #region InferSchemaFromElement Tests

    [Fact]
    public void InferSchemaFromElement_String_ReturnsStringType()
    {
        // Arrange
        var json = "\"test string\"";
        using var doc = JsonDocument.Parse(json);

        // Act
        var result = ToolOutputHelper.InferSchemaFromElement(doc.RootElement, 0) as Dictionary<string, object>;

        // Assert
        result.ShouldNotBeNull();
        result["type"].ShouldBe("string");
    }

    [Fact]
    public void InferSchemaFromElement_Number_ReturnsNumberType()
    {
        // Arrange
        var json = "42";
        using var doc = JsonDocument.Parse(json);

        // Act
        var result = ToolOutputHelper.InferSchemaFromElement(doc.RootElement, 0) as Dictionary<string, object>;

        // Assert
        result.ShouldNotBeNull();
        result["type"].ShouldBe("number");
    }

    [Fact]
    public void InferSchemaFromElement_Boolean_ReturnsBooleanType()
    {
        // Arrange
        var json = "true";
        using var doc = JsonDocument.Parse(json);

        // Act
        var result = ToolOutputHelper.InferSchemaFromElement(doc.RootElement, 0) as Dictionary<string, object>;

        // Assert
        result.ShouldNotBeNull();
        result["type"].ShouldBe("boolean");
    }

    [Fact]
    public void InferSchemaFromElement_Null_ReturnsNullType()
    {
        // Arrange
        var json = "null";
        using var doc = JsonDocument.Parse(json);

        // Act
        var result = ToolOutputHelper.InferSchemaFromElement(doc.RootElement, 0) as Dictionary<string, object>;

        // Assert
        result.ShouldNotBeNull();
        result["type"].ShouldBe("null");
    }

    [Fact]
    public void InferSchemaFromElement_Array_ReturnsArrayType()
    {
        // Arrange
        var json = "[1, 2, 3]";
        using var doc = JsonDocument.Parse(json);

        // Act
        var result = ToolOutputHelper.InferSchemaFromElement(doc.RootElement, 0) as Dictionary<string, object>;

        // Assert
        result.ShouldNotBeNull();
        result["type"].ShouldBe("array");
        result.ShouldContainKey("items");
    }

    [Fact]
    public void InferSchemaFromElement_Object_ReturnsObjectTypeWithProperties()
    {
        // Arrange
        var json = "{\"name\": \"test\", \"age\": 30}";
        using var doc = JsonDocument.Parse(json);

        // Act
        var result = ToolOutputHelper.InferSchemaFromElement(doc.RootElement, 0) as Dictionary<string, object>;

        // Assert
        result.ShouldNotBeNull();
        result["type"].ShouldBe("object");
        result.ShouldContainKey("properties");

        var properties = result["properties"] as Dictionary<string, object>;
        properties.ShouldNotBeNull();
        properties.ShouldContainKey("name");
        properties.ShouldContainKey("age");
    }

    [Fact]
    public void InferSchemaFromElement_NestedObject_HandlesNesting()
    {
        // Arrange
        var json = "{\"user\": {\"name\": \"test\", \"settings\": {\"theme\": \"dark\"}}}";
        using var doc = JsonDocument.Parse(json);

        // Act
        var result = ToolOutputHelper.InferSchemaFromElement(doc.RootElement, 0) as Dictionary<string, object>;

        // Assert
        result.ShouldNotBeNull();
        result["type"].ShouldBe("object");

        var properties = result["properties"] as Dictionary<string, object>;
        properties.ShouldNotBeNull();
        properties.ShouldContainKey("user");
    }

    [Fact]
    public void InferSchemaFromElement_ExceedsMaxDepth_ReturnsGenericObject()
    {
        // Arrange
        var json = "{\"key\": \"value\"}";
        using var doc = JsonDocument.Parse(json);

        // Act - depth = 4 exceeds MaxSchemaDepth (3)
        var result = ToolOutputHelper.InferSchemaFromElement(doc.RootElement, 4) as Dictionary<string, object>;

        // Assert
        result.ShouldNotBeNull();
        result["type"].ShouldBe("object");
        result.ShouldNotContainKey("properties"); // Generic object without properties
    }

    #endregion

    #region ProcessToolOutputAsync Tests

    [Fact]
    public async Task ProcessToolOutputAsync_SmallOutput_ReturnsOriginal()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var output = "Small output";
        var mockTool = new SimpleTestTool();

        // Act
        var result = await _service.ProcessToolOutputAsync(threadId, mockTool, output);

        // Assert
        result.ShouldBe(output);
        _mockStorage.Verify(s => s.SaveToolOutputAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessToolOutputAsync_LargeOutput_TruncatesAndStores()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var mockTool = new SimpleTestTool();
        var largeOutput = new string('x', 2000); // Exceeds default 1000 char limit
        var expectedFileKey = "test-file.yaml";

        _mockStorage
            .Setup(s => s.SaveToolOutputAsync(threadId, mockTool.Name, largeOutput, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedFileKey);

        // Act
        var result = await _service.ProcessToolOutputAsync(threadId, mockTool, largeOutput);

        // Assert
        result.ShouldBeOfType<string>();
        var resultString = result as string;
        resultString.ShouldNotBeNull();
        resultString.ShouldContain("partial preview");
        resultString.ShouldContain("File Key:");
        // The file key should be in the output somewhere
        _mockStorage.Verify(s => s.SaveToolOutputAsync(threadId, mockTool.Name, largeOutput, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessToolOutputAsync_NullOutput_ReturnsNull()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var mockTool = new SimpleTestTool();

        // Act
        var result = await _service.ProcessToolOutputAsync(threadId, mockTool, null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessToolOutputAsync_ToolOutputRetriever_ReturnsOriginal()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var mockTool = new ToolOutputRetrieverTool();
        var largeOutput = new string('x', 2000);

        // Act
        var result = await _service.ProcessToolOutputAsync(threadId, mockTool, largeOutput);

        // Assert
        result.ShouldBe(largeOutput);
        _mockStorage.Verify(s => s.SaveToolOutputAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessToolOutputAsync_WithDisableOutputTruncation_ReturnsOriginal()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var largeOutput = new string('x', 2000);

        // Create a mock tool with DisableOutputTruncation = true
        var mockTool = new TestToolWithDisabledTruncation();

        // Act
        var result = await _service.ProcessToolOutputAsync(threadId, mockTool, largeOutput);

        // Assert
        result.ShouldBe(largeOutput);
        _mockStorage.Verify(s => s.SaveToolOutputAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessToolOutputAsync_WithoutDisableOutputTruncation_Truncates()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var largeOutput = new string('x', 2000);
        _mockStorage.Setup(s => s.SaveToolOutputAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-file-key");

        // Create a mock tool without DisableOutputTruncation (defaults to false)
        var mockTool = new TestToolWithoutDisabledTruncation();

        // Act
        var result = await _service.ProcessToolOutputAsync(threadId, mockTool, largeOutput);

        // Assert
        result.ShouldNotBe(largeOutput);
        result.ShouldBeOfType<string>();
        ((string)result!).ShouldContain("test-file-key");
        _mockStorage.Verify(s => s.SaveToolOutputAsync(threadId, mockTool.Name, largeOutput, "txt", default), Times.Once);
    }

    #endregion

    #region Test Tool Classes

    private class SimpleTestTool : Microsoft.Extensions.AI.AIFunction
    {
        public override string Name => "TestTool";
        public override string Description => "Simple test tool";
        public override System.Reflection.MethodInfo? UnderlyingMethod => typeof(SimpleTestTool).GetMethod(nameof(Execute));

        [Agent.Framework.AgentTool(Agent.Framework.ToolMode.Auto)]
        public static void Execute() { }

        protected override async ValueTask<object?> InvokeCoreAsync(Microsoft.Extensions.AI.AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return null;
        }
    }

    private class ToolOutputRetrieverTool : Microsoft.Extensions.AI.AIFunction
    {
        public override string Name => "ToolOutputRetriever";
        public override string Description => "Tool output retriever";
        public override System.Reflection.MethodInfo? UnderlyingMethod => typeof(ToolOutputRetrieverTool).GetMethod(nameof(Execute));

        [Agent.Framework.AgentTool(Agent.Framework.ToolMode.Auto, DisableOutputTruncation = true)]
        public static void Execute() { }

        protected override async ValueTask<object?> InvokeCoreAsync(Microsoft.Extensions.AI.AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return null;
        }
    }

    private class TestToolWithDisabledTruncation : Microsoft.Extensions.AI.AIFunction
    {
        public override string Name => "TestToolWithDisabledTruncation";
        public override string Description => "Test tool";
        public override System.Reflection.MethodInfo? UnderlyingMethod => typeof(TestToolWithDisabledTruncation).GetMethod(nameof(Execute));

        [Agent.Framework.AgentTool(Agent.Framework.ToolMode.Auto, DisableOutputTruncation = true)]
        public static void Execute() { }

        protected override async ValueTask<object?> InvokeCoreAsync(Microsoft.Extensions.AI.AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return null;
        }
    }

    private class TestToolWithoutDisabledTruncation : Microsoft.Extensions.AI.AIFunction
    {
        public override string Name => "TestToolWithoutDisabledTruncation";
        public override string Description => "Test tool";
        public override System.Reflection.MethodInfo? UnderlyingMethod => typeof(TestToolWithoutDisabledTruncation).GetMethod(nameof(Execute));

        [Agent.Framework.AgentTool(Agent.Framework.ToolMode.Auto)]
        public static void Execute() { }

        protected override async ValueTask<object?> InvokeCoreAsync(Microsoft.Extensions.AI.AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return null;
        }
    }

    #endregion
}
