// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Common;
using Agent.Core.Models;
using Agent.Framework;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Processor for CodeExecutionResponse output from the CodeInterpreter tool.
/// Handles formatting of execution results, stdout/stderr, and error messages.
/// Large content is saved to files with metadata and preview in the output.
/// </summary>
public class CodeExecutionResponseProcessor : IToolOutputProcessor
{
    /// <summary>
    /// Threshold for considering content "large" and saving to file.
    /// Used for stdout, stderr, and result content.
    /// </summary>
    private const int LargeContentThreshold = 8000;

    /// <summary>
    /// Maximum number of lines to include in preview.
    /// </summary>
    private const int PreviewMaxLines = 15;

    /// <summary>
    /// Maximum number of characters to include in preview.
    /// </summary>
    private const int PreviewMaxChars = 2000;

    /// <inheritdoc />
    public async Task<object?> ProcessAsync(
        object? output,
        ToolOutputProcessorContext context,
        CancellationToken cancellationToken = default)
    {
        if (output is not CodeExecutionResponse response)
        {
            return output;
        }

        return await FormatCodeExecutionResponseAsync(
            response,
            context,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Formats a CodeExecutionResponse into a human-readable string.
    /// Large content is saved to files with metadata and preview included.
    /// </summary>
    /// <param name="response">The code execution response to format</param>
    /// <param name="context">Processor context for saving large content to files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Formatted execution result string</returns>
    /// Sample formated result:
    /*
        Status: Success
        ExitCode: 0
        === EXECUTION RESULT ===
        42
        === END EXECUTION RESULT ===
        === STDOUT ===
        Hello from stdout
        === END STDOUT ===
        === STDERR ===
        Warning: test warning
        This is **only a partial preview** of the full content.
        Use the appropriate tool to retrieve more content from the stored file.
        This file is not the result of execution. Do not mention the file in your response.
        File Key: `saved-file-2.txt`
        Content Type: txt
        Total Size: 9.8 KB
        Total Lines: 1
        Preview:
        ```txt
        ABCDEFGHIJ...
        ```

        === CODE GENERATED FILES ===
        📊 Image (file name: chart.png): ![chart.png](/api/files/chart.png)
        📊 Data (file name: data.csv): [Download data.csv](/api/files/data.csv)
        To make these files available to the user, they must be presented in markdown syntax.
        === END CODE GENERATED FILES ===
    */
    public async Task<string> FormatCodeExecutionResponseAsync(
        CodeExecutionResponse response,
        ToolOutputProcessorContext context,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Status: {response.Status ?? "(n/a)"}");
        sb.AppendLine($"ExitCode: {(response.Hresult != null ? response.Hresult.ToString() : "(n/a)")}");

        // Handle Result which can be a string or an image
        await FormatResultSection(sb, response, context, cancellationToken);

        if (!string.IsNullOrWhiteSpace(response.Stdout))
        {
            await FormatOutputSection(sb, "STDOUT", response.Stdout, context, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(response.Stderr))
        {
            await FormatOutputSection(sb, "STDERR", response.Stderr, context, cancellationToken);
        }

        if (!string.IsNullOrEmpty(response.ErrorName))
        {
            sb.AppendLine("=== ERROR ===");
            sb.AppendLine($"Error: {response.ErrorName}");
            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                sb.AppendLine($"Message: {response.ErrorMessage}");
            }
            if (!string.IsNullOrEmpty(response.ErrorStackTrace))
            {
                sb.AppendLine($"Stack Trace: {response.ErrorStackTrace}");
            }
            sb.AppendLine("=== END ERROR ===");
        }

        // Format code generated files
        FormatRetrievedFilesSection(sb, response.RetrievedFiles);

        return sb.ToString();
    }

    /// <summary>
    /// Formats the code generated files section.
    /// </summary>
    private static void FormatRetrievedFilesSection(StringBuilder sb, List<Core.Models.CodeFileInfo>? retrievedFiles)
    {
        if (retrievedFiles == null || retrievedFiles.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("=== CODE GENERATED FILES ===");

        foreach (var file in retrievedFiles)
        {
            var emoji = file.FileType switch
            {
                "Image" => "📊",
                "Data" => "📊",
                "Document" => "📄",
                "Code" => "💻",
                "Archive" => "🗜️",
                _ => "📁"
            };

            if (file.FileType == "Image")
            {
                sb.AppendLine($"{emoji} Image (file name: {file.Filename}): ![{file.Filename}]({file.DownloadLink})");
            }
            else
            {
                sb.AppendLine($"{emoji} {file.FileType} (file name: {file.Filename}): [Download {file.Filename}]({file.DownloadLink})");
            }
        }

        sb.AppendLine("To make these files available to the user, they must be presented in markdown syntax.");
        sb.AppendLine("=== END CODE GENERATED FILES ===");
    }

    /// <summary>
    /// Formats an output section (stdout/stderr), saving to file if content is large.
    /// Uses common section formatting from ToolOutputUtilities.
    /// </summary>
    private async Task FormatOutputSection(
        StringBuilder sb,
        string sectionName,
        string content,
        ToolOutputProcessorContext context,
        CancellationToken cancellationToken)
    {
        var shouldSaveToFile = content.Length > LargeContentThreshold;

        if (shouldSaveToFile)
        {
            // Save large content to file
            var fileKey = await context.SaveOutput(
                context.ThreadId,
                context.ToolName,
                content,
                "txt",
                cancellationToken);

            // Use common section formatting for saved-to-file content
            sb.Append(ToolOutputHelper.FormatSavedToFileSection(
                sectionName,
                fileKey,
                "txt",
                content,
                PreviewMaxLines,
                PreviewMaxChars));
        }
        else
        {
            // Use common inline section formatting
            sb.Append(ToolOutputHelper.FormatInlineSection(sectionName, content));
        }
    }

    /// <summary>
    /// Formats the result section based on result type.
    /// </summary>
    private async Task FormatResultSection(
        StringBuilder sb,
        CodeExecutionResponse response,
        ToolOutputProcessorContext context,
        CancellationToken cancellationToken)
    {
        switch (response.Result)
        {
            case ImageExecutionResult imageResult:
                FormatImageResultSection(sb, imageResult, response.ImageFile);
                break;

            case ObjectExecutionResult objectResult:
                var valueString = objectResult.Value switch
                {
                    string s => s,
                    null => string.Empty,
                    _ => JsonSerializer.Serialize(objectResult.Value)
                };
                await FormatStringResultSection(sb, valueString, context, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Formats an image execution result using the pre-saved ImageFile.
    /// </summary>
    private static void FormatImageResultSection(
        StringBuilder sb,
        ImageExecutionResult imageResult,
        CodeFileInfo? imageFile)
    {
        sb.AppendLine("=== EXECUTION RESULT (IMAGE) ===");
        if (imageFile != null)
        {
            sb.AppendLine($"Type: {imageResult.Type}");
            sb.AppendLine($"Format: {imageResult.Format}");
            sb.AppendLine($"File: `{imageFile.Filename}`");
            sb.AppendLine($"Use following markdown syntax if you want to display the image to user: ![{imageFile.Filename}]({imageFile.DownloadLink})");
        }
        else
        {
            sb.AppendLine("The image file is not available.");
        }
        sb.AppendLine("=== END EXECUTION RESULT ===");
    }

    /// <summary>
    /// Formats a string execution result, saving to file if content is large.
    /// Uses common section formatting from ToolOutputUtilities.
    /// </summary>
    private async Task FormatStringResultSection(
        StringBuilder sb,
        string value,
        ToolOutputProcessorContext context,
        CancellationToken cancellationToken)
    {
        var shouldSaveToFile = value.Length > LargeContentThreshold;

        if (shouldSaveToFile)
        {
            // Save large result to file
            var contentType = ToolOutputHelper.DetectContentType(value);
            var fileKey = await context.SaveOutput(
                context.ThreadId,
                context.ToolName,
                value,
                contentType,
                cancellationToken);

            // Use common section formatting for saved-to-file content
            sb.Append(ToolOutputHelper.FormatSavedToFileSection(
                "EXECUTION RESULT",
                fileKey,
                contentType,
                value,
                PreviewMaxLines,
                PreviewMaxChars));
        }
        else
        {
            // Use common inline section formatting
            sb.Append(ToolOutputHelper.FormatInlineSection("EXECUTION RESULT", value));
        }
    }
}
