// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Reasoning;
using Shouldly;
using Xunit;

namespace Agent.Tests.Unit.Reasoning;

/// <summary>
/// Unit tests for DefaultToolOutputProcessor
/// </summary>
public class DefaultToolOutputProcessorTests
{
    private readonly DefaultToolOutputProcessor _processor;

    public DefaultToolOutputProcessorTests()
    {
        _processor = new DefaultToolOutputProcessor(maxCharacterCount: 1000);
    }

    #region ShouldTruncate Tests

    [Fact]
    public void ShouldTruncate_NullOutput_ReturnsFalse()
    {
        // Act
        var result = _processor.ShouldTruncate(null);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ShouldTruncate_EmptyOutput_ReturnsFalse()
    {
        // Act
        var result = _processor.ShouldTruncate(string.Empty);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ShouldTruncate_SmallOutput_ReturnsFalse()
    {
        // Arrange
        var output = new string('x', 500);

        // Act
        var result = _processor.ShouldTruncate(output);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ShouldTruncate_LargeOutput_ReturnsTrue()
    {
        // Arrange
        var output = new string('x', 2000);

        // Act
        var result = _processor.ShouldTruncate(output);

        // Assert
        result.ShouldBeTrue();
    }

    #endregion

    #region FormatTruncationMessage Tests

    [Fact]
    public void FormatTruncationMessage_Json_IncludesSchema()
    {
        // Arrange
        var fileKey = "test-file-123.json";
        var contentType = "json";
        var lineCount = 100;
        var originalOutput = "{\"key\": \"value\", \"count\": 42}";

        // Act
        var result = _processor.FormatTruncationMessage(fileKey, contentType, lineCount, originalOutput);

        // Assert
        result.ShouldContain("File Key:");
        result.ShouldContain(fileKey);
        result.ShouldContain("Content type: `json`");
        result.ShouldContain("Total lines: `100`");
        result.ShouldContain("JSON schema (inferred):");
        result.ShouldContain("**Preview**");
    }

    [Fact]
    public void FormatTruncationMessage_Yaml_NoSchema()
    {
        // Arrange
        var fileKey = "test-file-456.yaml";
        var contentType = "yaml";
        var lineCount = 50;
        var originalOutput = "key: value\ncount: 42";

        // Act
        var result = _processor.FormatTruncationMessage(fileKey, contentType, lineCount, originalOutput);

        // Assert
        result.ShouldContain("File Key:");
        result.ShouldContain(fileKey);
        result.ShouldContain("Content type: `yaml`");
        result.ShouldContain("Total lines: `50`");
        result.ShouldNotContain("JSON schema");
        result.ShouldContain("**Preview**");
    }

    [Fact]
    public void FormatTruncationMessage_Text_NoSchema()
    {
        // Arrange
        var fileKey = "test-file-789.txt";
        var contentType = "txt";
        var lineCount = 200;
        var originalOutput = "This is plain text output";

        // Act
        var result = _processor.FormatTruncationMessage(fileKey, contentType, lineCount, originalOutput);

        // Assert
        result.ShouldContain("File Key:");
        result.ShouldContain(fileKey);
        result.ShouldContain("Content type: `txt`");
        result.ShouldContain("Total lines: `200`");
        result.ShouldNotContain("JSON schema");
        result.ShouldContain("**Preview**");
    }

    [Fact]
    public void FormatTruncationMessage_LargeContent_ShowsFileSize()
    {
        // Arrange
        var fileKey = "large-file.json";
        var contentType = "json";
        var lineCount = 10000;
        var originalOutput = new string('x', 50000); // 50KB

        // Act
        var result = _processor.FormatTruncationMessage(fileKey, contentType, lineCount, originalOutput);

        // Assert
        result.ShouldContain("Total size:");
        result.ShouldContain("KB"); // Should show size in KB
    }

    [Fact]
    public void FormatTruncationMessage_IncludesPreview()
    {
        // Arrange
        var fileKey = "test-file.json";
        var contentType = "json";
        var lineCount = 10;
        var originalOutput = "{\"test\": \"data\"}";

        // Act
        var result = _processor.FormatTruncationMessage(fileKey, contentType, lineCount, originalOutput);

        // Assert
        result.ShouldContain("**Preview**");
        result.ShouldContain("```json");
        result.ShouldContain(originalOutput);
    }

    [Fact]
    public void FormatTruncationMessage_IncludesPartialPreviewNotice()
    {
        // Arrange
        var fileKey = "test-file.txt";
        var contentType = "txt";
        var lineCount = 100;
        var originalOutput = "Some content here";

        // Act
        var result = _processor.FormatTruncationMessage(fileKey, contentType, lineCount, originalOutput);

        // Assert
        result.ShouldContain("partial preview");
        result.ShouldContain("retrieve more content");
    }

    #endregion
}
