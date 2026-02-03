// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Runtime.Reasoning;
using Shouldly;
using Xunit;

namespace Agent.Tests.Unit.Reasoning;

/// <summary>
/// Unit tests for CodeExecutionResponseProcessor
/// </summary>
public class CodeExecutionResponseProcessorTests
{
    private readonly CodeExecutionResponseProcessor _processor;
    private readonly ToolOutputProcessorContext _context;
    private readonly List<(Guid ThreadId, string ToolName, string CallId, string Content, string ContentType)> _savedOutputs;

    public CodeExecutionResponseProcessorTests()
    {
        _processor = new CodeExecutionResponseProcessor();
        _savedOutputs = new List<(Guid, string, string, string, string)>();

        _context = new ToolOutputProcessorContext
        {
            ThreadId = Guid.NewGuid(),
            ToolName = "CodeInterpreter",
            CallId = "test-call-id",
            SaveOutput = (threadId, toolName, callId, content, contentType, ct) =>
            {
                _savedOutputs.Add((threadId, toolName, callId, content, contentType));
                return Task.FromResult($"saved-file-{_savedOutputs.Count}.{contentType}");
            }
        };
    }

    #region Exact Output Tests

    [Fact]
    public async Task FormatResponse_SmallStringResult_WithSmallStdoutAndStderr_ExactOutput()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Result = new ObjectExecutionResult { Value = "42" },
            Stdout = "Hello from stdout",
            Stderr = "Warning: test warning",
            RetrievedFiles = new List<Agent.Core.Models.CodeFileInfo>
            {
                new Agent.Core.Models.CodeFileInfo { Filename = "chart.png", DownloadLink = "/api/files/chart.png", FileType = "Image" },
                new Agent.Core.Models.CodeFileInfo { Filename = "data.csv", DownloadLink = "/api/files/data.csv", FileType = "Data" }
            }
        };

        // Use StringBuilder.AppendLine() to ensure cross-platform compatibility (matches production code)
        var sb = new StringBuilder();
        sb.AppendLine("Status: Success");
        sb.AppendLine("ExitCode: 0");
        sb.AppendLine("=== EXECUTION RESULT ===");
        sb.AppendLine("42");
        sb.AppendLine("=== END EXECUTION RESULT ===");
        sb.AppendLine("=== STDOUT ===");
        sb.AppendLine("Hello from stdout");
        sb.AppendLine("=== END STDOUT ===");
        sb.AppendLine("=== STDERR ===");
        sb.AppendLine("Warning: test warning");
        sb.AppendLine("=== END STDERR ===");
        sb.AppendLine();
        sb.AppendLine("=== CODE GENERATED FILES ===");
        sb.AppendLine("📊 Image (file name: chart.png): ![chart.png](/api/files/chart.png)");
        sb.AppendLine("📊 Data (file name: data.csv): [Download data.csv](/api/files/data.csv)");
        sb.AppendLine("To make these files available to the user, they must be presented in markdown syntax.");
        sb.AppendLine("=== END CODE GENERATED FILES ===");

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldBe(sb.ToString());
        _savedOutputs.ShouldBeEmpty();
    }

    [Fact]
    public async Task FormatResponse_SmallImageResult_WithSmallStdoutAndStderr_ExactOutput()
    {
        // Arrange
        var base64Data = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Result = new ImageExecutionResult
            {
                Type = "image",
                Format = "png",
                Base64Data = base64Data
            },
            ImageFile = new CodeFileInfo
            {
                Filename = "output_abc123.png",
                DownloadLink = "/api/files/thread-id/output_abc123.png",
                FileType = "Image"
            },
            Stdout = "Generating chart...",
            Stderr = "Matplotlib warning"
        };

        // Use StringBuilder.AppendLine() to ensure cross-platform compatibility (matches production code)
        var sb = new StringBuilder();
        sb.AppendLine("Status: Success");
        sb.AppendLine("ExitCode: 0");
        sb.AppendLine("=== EXECUTION RESULT (IMAGE) ===");
        sb.AppendLine("Type: image");
        sb.AppendLine("Format: png");
        sb.AppendLine("File: `output_abc123.png`");
        sb.AppendLine("Use following markdown syntax if you want to display the image to user: ![output_abc123.png](/api/files/thread-id/output_abc123.png)");
        sb.AppendLine("=== END EXECUTION RESULT ===");
        sb.AppendLine("=== STDOUT ===");
        sb.AppendLine("Generating chart...");
        sb.AppendLine("=== END STDOUT ===");
        sb.AppendLine("=== STDERR ===");
        sb.AppendLine("Matplotlib warning");
        sb.AppendLine("=== END STDERR ===");

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldBe(sb.ToString());
        _savedOutputs.ShouldBeEmpty(); // Image is pre-saved, no SaveOutput call needed
    }

    [Fact]
    public async Task FormatResponse_LargeStringResult_WithLargeStdoutAndStderr_ExactOutput()
    {
        // Arrange
        var largeResult = new string('R', 10000);
        var largeStdout = new string('O', 10000);
        var largeStderr = new string('E', 10000);

        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Result = new ObjectExecutionResult { Value = largeResult },
            Stdout = largeStdout,
            Stderr = largeStderr
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert - verify structure and key elements
        // File 1: large result, File 2: large stdout, File 3: large stderr
        _savedOutputs.Count.ShouldBe(3);
        _savedOutputs[0].Content.ShouldBe(largeResult);
        _savedOutputs[0].ContentType.ShouldBe("txt");
        _savedOutputs[1].Content.ShouldBe(largeStdout);
        _savedOutputs[1].ContentType.ShouldBe("txt");
        _savedOutputs[2].Content.ShouldBe(largeStderr);
        _savedOutputs[2].ContentType.ShouldBe("txt");

        // Verify exact output structure
        // Use StringBuilder.AppendLine() to ensure cross-platform compatibility (matches production code)
        var sb = new StringBuilder();
        sb.AppendLine("Status: Success");
        sb.AppendLine("ExitCode: 0");
        sb.AppendLine("=== EXECUTION RESULT ===");
        sb.AppendLine("This is **only a partial preview** of the full content.");
        sb.AppendLine("Use the appropriate tool to retrieve more content from the stored file.");
        sb.AppendLine("This file is not the result of execution. Do not mention the file in your response.");
        sb.AppendLine("File Key: `saved-file-1.txt`");
        sb.AppendLine("Content Type: txt");
        sb.AppendLine("Total Size: 9.8 KB");
        sb.AppendLine("Total Lines: 1");
        sb.AppendLine("Preview:");
        sb.AppendLine("```txt");
        sb.AppendLine(new string('R', 2000));
        sb.AppendLine("```");
        sb.AppendLine("=== END EXECUTION RESULT ===");
        sb.AppendLine("=== STDOUT ===");
        sb.AppendLine("This is **only a partial preview** of the full content.");
        sb.AppendLine("Use the appropriate tool to retrieve more content from the stored file.");
        sb.AppendLine("This file is not the result of execution. Do not mention the file in your response.");
        sb.AppendLine("File Key: `saved-file-2.txt`");
        sb.AppendLine("Content Type: txt");
        sb.AppendLine("Total Size: 9.8 KB");
        sb.AppendLine("Total Lines: 1");
        sb.AppendLine("Preview:");
        sb.AppendLine("```txt");
        sb.AppendLine(new string('O', 2000));
        sb.AppendLine("```");
        sb.AppendLine("=== END STDOUT ===");
        sb.AppendLine("=== STDERR ===");
        sb.AppendLine("This is **only a partial preview** of the full content.");
        sb.AppendLine("Use the appropriate tool to retrieve more content from the stored file.");
        sb.AppendLine("This file is not the result of execution. Do not mention the file in your response.");
        sb.AppendLine("File Key: `saved-file-3.txt`");
        sb.AppendLine("Content Type: txt");
        sb.AppendLine("Total Size: 9.8 KB");
        sb.AppendLine("Total Lines: 1");
        sb.AppendLine("Preview:");
        sb.AppendLine("```txt");
        sb.AppendLine(new string('E', 2000));
        sb.AppendLine("```");
        sb.AppendLine("=== END STDERR ===");

        result.ShouldBe(sb.ToString());
    }

    #endregion

    #region ProcessAsync Tests

    [Fact]
    public async Task ProcessAsync_NonCodeExecutionResponse_ReturnsOriginalOutput()
    {
        // Arrange
        var output = "This is just a string";

        // Act
        var result = await _processor.ProcessAsync(output, _context);

        // Assert
        result.ShouldBe(output);
    }

    [Fact]
    public async Task ProcessAsync_NullOutput_ReturnsNull()
    {
        // Act
        var result = await _processor.ProcessAsync(null, _context);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessAsync_CodeExecutionResponse_ReturnsFormattedString()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0
        };

        // Act
        var result = await _processor.ProcessAsync(response, _context);

        // Assert
        result.ShouldBeOfType<string>();
        var resultString = (string)result!;
        resultString.ShouldContain("Status: Success");
        resultString.ShouldContain("ExitCode: 0");
    }

    #endregion

    #region Basic Response Formatting Tests

    [Fact]
    public async Task FormatResponse_IncludesStatusAndExitCode()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Completed",
            Hresult = 42
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("Status: Completed");
        result.ShouldContain("ExitCode: 42");
    }

    [Fact]
    public async Task FormatResponse_NullStatus_ShowsNA()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = null,
            Hresult = 0
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("Status: (n/a)");
    }

    [Fact]
    public async Task FormatResponse_NullHresult_ShowsNA()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = null
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("ExitCode: (n/a)");
    }

    #endregion

    #region String Result Handling Tests

    [Fact]
    public async Task FormatResponse_SmallStringResult_IncludesInline()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Result = new ObjectExecutionResult { Value = "Hello, World!" }
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("=== EXECUTION RESULT ===");
        result.ShouldContain("Hello, World!");
        result.ShouldContain("=== END EXECUTION RESULT ===");
        _savedOutputs.ShouldBeEmpty(); // Should not save small content
    }

    [Fact]
    public async Task FormatResponse_LargeStringResult_SavesAndShowsPreview()
    {
        // Arrange
        var largeContent = new string('x', 10000); // Exceeds 8000 threshold
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Result = new ObjectExecutionResult { Value = largeContent }
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("EXECUTION RESULT");
        result.ShouldContain("only a partial preview");
        result.ShouldContain("File Key:");
        result.ShouldContain("Preview:");
        _savedOutputs.Count.ShouldBe(1);
        _savedOutputs[0].Content.ShouldBe(largeContent);
    }

    [Fact]
    public async Task FormatResponse_EmptyStringResult_ShowsEmptySection()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Result = new ObjectExecutionResult { Value = string.Empty }
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert - empty results still show the section header
        result.ShouldContain("=== EXECUTION RESULT ===");
        result.ShouldContain("=== END EXECUTION RESULT ===");
    }

    #endregion

    #region Image Result Handling Tests

    [Fact]
    public async Task FormatResponse_ImageResult_WithImageFile_ShowsFileInfo()
    {
        // Arrange
        var base64Data = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Result = new ImageExecutionResult
            {
                Type = "image",
                Format = "png",
                Base64Data = base64Data
            },
            ImageFile = new CodeFileInfo
            {
                Filename = "chart.png",
                DownloadLink = "/api/files/thread-id/chart.png",
                FileType = "Image"
            }
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("=== EXECUTION RESULT (IMAGE) ===");
        result.ShouldContain("File: `chart.png`");
        result.ShouldContain("![chart.png](/api/files/thread-id/chart.png)");
        result.ShouldContain("=== END EXECUTION RESULT ===");
        _savedOutputs.ShouldBeEmpty(); // Image is pre-saved, no SaveOutput call
    }

    [Fact]
    public async Task FormatResponse_ImageResult_IncludesTypeAndFormat()
    {
        // Arrange
        var base64Data = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF }); // JPEG header
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Result = new ImageExecutionResult
            {
                Type = "image",
                Format = "jpeg",
                Base64Data = base64Data
            },
            ImageFile = new CodeFileInfo
            {
                Filename = "photo.jpeg",
                DownloadLink = "/api/files/thread-id/photo.jpeg",
                FileType = "Image"
            }
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("Type: image");
        result.ShouldContain("Format: jpeg");
    }

    [Fact]
    public async Task FormatResponse_ImageResult_WithNullImageFile_ShowsNotAvailableMessage()
    {
        // Arrange
        var base64Data = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Result = new ImageExecutionResult
            {
                Type = "image",
                Format = "png",
                Base64Data = base64Data
            },
            ImageFile = null // Image file not available
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("=== EXECUTION RESULT (IMAGE) ===");
        result.ShouldContain("The image file is not available.");
        result.ShouldContain("=== END EXECUTION RESULT ===");
        result.ShouldNotContain("Type:");
        result.ShouldNotContain("Format:");
        _savedOutputs.ShouldBeEmpty();
    }

    #endregion

    #region Stdout/Stderr Handling Tests

    [Fact]
    public async Task FormatResponse_SmallStdout_IncludesInline()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Stdout = "Print output here"
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("=== STDOUT ===");
        result.ShouldContain("Print output here");
        result.ShouldContain("=== END STDOUT ===");
        _savedOutputs.ShouldBeEmpty();
    }

    [Fact]
    public async Task FormatResponse_LargeStdout_SavesAndShowsPreview()
    {
        // Arrange
        var largeStdout = new string('a', 10000);
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Stdout = largeStdout
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("STDOUT");
        result.ShouldContain("only a partial preview");
        result.ShouldContain("File Key:");
        _savedOutputs.Count.ShouldBe(1);
        _savedOutputs[0].Content.ShouldBe(largeStdout);
        _savedOutputs[0].ContentType.ShouldBe("txt");
    }

    [Fact]
    public async Task FormatResponse_SmallStderr_IncludesInline()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Error",
            Hresult = 1,
            Stderr = "Error message here"
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("=== STDERR ===");
        result.ShouldContain("Error message here");
        result.ShouldContain("=== END STDERR ===");
    }

    [Fact]
    public async Task FormatResponse_LargeStderr_SavesAndShowsPreview()
    {
        // Arrange
        var largeStderr = new string('e', 10000);
        var response = new CodeExecutionResponse
        {
            Status = "Error",
            Hresult = 1,
            Stderr = largeStderr
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("STDERR");
        result.ShouldContain("only a partial preview");
        _savedOutputs.Count.ShouldBe(1);
        _savedOutputs[0].Content.ShouldBe(largeStderr);
    }

    [Fact]
    public async Task FormatResponse_EmptyStdout_Omitted()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Stdout = null
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldNotContain("STDOUT");
    }

    [Fact]
    public async Task FormatResponse_WhitespaceStdout_Omitted()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Stdout = "   "
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldNotContain("STDOUT");
    }

    [Fact]
    public async Task FormatResponse_EmptyStderr_Omitted()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            Stderr = string.Empty
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldNotContain("STDERR");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task FormatResponse_WithError_IncludesErrorSection()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Error",
            Hresult = 1,
            ErrorName = "RuntimeError",
            ErrorMessage = "Division by zero",
            ErrorStackTrace = "at main.py:10\nat helper.py:5"
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("=== ERROR ===");
        result.ShouldContain("Error: RuntimeError");
        result.ShouldContain("Message: Division by zero");
        result.ShouldContain("Stack Trace: at main.py:10");
        result.ShouldContain("=== END ERROR ===");
    }

    [Fact]
    public async Task FormatResponse_ErrorNameOnly_IncludesPartialErrorSection()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Error",
            Hresult = 1,
            ErrorName = "UnknownError",
            ErrorMessage = null,
            ErrorStackTrace = null
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("=== ERROR ===");
        result.ShouldContain("Error: UnknownError");
        result.ShouldNotContain("Message:");
        result.ShouldNotContain("Stack Trace:");
        result.ShouldContain("=== END ERROR ===");
    }

    [Fact]
    public async Task FormatResponse_NoError_OmitsErrorSection()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            ErrorName = null
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldNotContain("=== ERROR ===");
    }

    #endregion

    #region Retrieved Files Handling Tests

    [Fact]
    public async Task FormatResponse_WithRetrievedFiles_IncludesFilesSection()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            RetrievedFiles = new List<Agent.Core.Models.CodeFileInfo>
            {
                new Agent.Core.Models.CodeFileInfo
                {
                    Filename = "output.csv",
                    DownloadLink = "/api/files/output.csv",
                    FileType = "Data"
                }
            }
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("=== CODE GENERATED FILES ===");
        result.ShouldContain("output.csv");
        result.ShouldContain("/api/files/output.csv");
        result.ShouldContain("=== END CODE GENERATED FILES ===");
    }

    [Fact]
    public async Task FormatResponse_ImageFile_UsesImageMarkdown()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            RetrievedFiles = new List<Agent.Core.Models.CodeFileInfo>
            {
                new Agent.Core.Models.CodeFileInfo
                {
                    Filename = "chart.png",
                    DownloadLink = "/api/files/chart.png",
                    FileType = "Image"
                }
            }
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("![chart.png](/api/files/chart.png)");
    }

    [Fact]
    public async Task FormatResponse_DataFile_UsesDownloadLink()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            RetrievedFiles = new List<Agent.Core.Models.CodeFileInfo>
            {
                new Agent.Core.Models.CodeFileInfo
                {
                    Filename = "data.json",
                    DownloadLink = "/api/files/data.json",
                    FileType = "Data"
                }
            }
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("[Download data.json](/api/files/data.json)");
    }

    [Fact]
    public async Task FormatResponse_NoRetrievedFiles_OmitsFilesSection()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            RetrievedFiles = null
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldNotContain("CODE GENERATED FILES");
    }

    [Fact]
    public async Task FormatResponse_EmptyRetrievedFiles_OmitsFilesSection()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            RetrievedFiles = new List<Agent.Core.Models.CodeFileInfo>()
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldNotContain("CODE GENERATED FILES");
    }

    [Theory]
    [InlineData("Image", "📊")]
    [InlineData("Data", "📊")]
    [InlineData("Document", "📄")]
    [InlineData("Code", "💻")]
    [InlineData("Archive", "🗜️")]
    [InlineData("Unknown", "📁")]
    public async Task FormatResponse_DifferentFileTypes_UsesCorrectEmojis(string fileType, string expectedEmoji)
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            RetrievedFiles = new List<Agent.Core.Models.CodeFileInfo>
            {
                new Agent.Core.Models.CodeFileInfo
                {
                    Filename = "file.ext",
                    DownloadLink = "/api/files/file.ext",
                    FileType = fileType
                }
            }
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain(expectedEmoji);
    }

    [Fact]
    public async Task FormatResponse_MultipleRetrievedFiles_IncludesAll()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            RetrievedFiles = new List<Agent.Core.Models.CodeFileInfo>
            {
                new Agent.Core.Models.CodeFileInfo { Filename = "chart.png", DownloadLink = "/api/files/chart.png", FileType = "Image" },
                new Agent.Core.Models.CodeFileInfo { Filename = "data.csv", DownloadLink = "/api/files/data.csv", FileType = "Data" },
                new Agent.Core.Models.CodeFileInfo { Filename = "report.pdf", DownloadLink = "/api/files/report.pdf", FileType = "Document" }
            }
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("chart.png");
        result.ShouldContain("data.csv");
        result.ShouldContain("report.pdf");
    }

    [Fact]
    public async Task FormatResponse_RetrievedFiles_IncludesMarkdownInstructions()
    {
        // Arrange
        var response = new CodeExecutionResponse
        {
            Status = "Success",
            Hresult = 0,
            RetrievedFiles = new List<Agent.Core.Models.CodeFileInfo>
            {
                new Agent.Core.Models.CodeFileInfo { Filename = "file.txt", DownloadLink = "/api/files/file.txt", FileType = "Data" }
            }
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        result.ShouldContain("markdown syntax");
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public async Task FormatResponse_LargeStdoutAndStderr_SavesBoth()
    {
        // Arrange
        var largeStdout = new string('o', 10000);
        var largeStderr = new string('e', 10000);
        var response = new CodeExecutionResponse
        {
            Status = "Error",
            Hresult = 1,
            Stdout = largeStdout,
            Stderr = largeStderr
        };

        // Act
        var result = await _processor.FormatCodeExecutionResponseAsync(response, _context);

        // Assert
        _savedOutputs.Count.ShouldBe(2);
        _savedOutputs[0].Content.ShouldBe(largeStdout);
        _savedOutputs[1].Content.ShouldBe(largeStderr);
    }

    #endregion
}
