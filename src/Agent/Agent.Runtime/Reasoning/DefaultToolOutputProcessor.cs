// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Common;
using Agent.Framework;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Default processor for tool outputs that don't have a dedicated processor.
/// Handles truncation and storage of large outputs with preview formatting.
/// </summary>
public class DefaultToolOutputProcessor : IToolOutputProcessor
{
    private const int PreviewMaxLines = 20;
    private const int PreviewMaxChars = 4096;

    private readonly int _maxCharacterCount;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultToolOutputProcessor"/> class.
    /// </summary>
    /// <param name="maxCharacterCount">Maximum character count before truncation is applied</param>
    /// <param name="logger">Optional logger for diagnostic output</param>
    public DefaultToolOutputProcessor(int maxCharacterCount, ILogger? logger = null)
    {
        _maxCharacterCount = maxCharacterCount;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<object?> ProcessAsync(
        object? output,
        ToolOutputProcessorContext context,
        CancellationToken cancellationToken = default)
    {
        if (output == null)
        {
            return output;
        }

        // Convert output to string representation for truncation check
        string outputString;
        if (output is string str)
        {
            outputString = str;
        }
        else if (output is JsonElement jsonElement
            && jsonElement.ValueKind == JsonValueKind.String)
        {
            // Handle JsonElement specifically to avoid double serialization
            outputString = jsonElement.GetString()!;
        }
        else
        {
            // Serialize object to JSON
            try
            {
                outputString = JsonSerializer.Serialize(output);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to serialize tool output for tool {ToolName}, returning empty string", context.ToolName);
                outputString = string.Empty;
            }
        }

        // Check if truncation is needed
        if (!ShouldTruncate(outputString))
        {
            _logger?.LogDebug("Output for tool {ToolName} does not require truncation (Length: {Length})",
                context.ToolName, outputString.Length);
            return output;
        }

        // Truncation is needed - store the full output and return truncation message
        try
        {
            var contentType = ToolOutputHelper.DetectContentType(outputString);
            var fileKey = await context.SaveOutput(
                context.ThreadId,
                context.ToolName,
                context.CallId,
                outputString,
                contentType,
                cancellationToken);

            var lineCount = outputString.Split('\n').Length;

            _logger?.LogDebug(
                "Truncated large tool output for tool {ToolName}, saved as {FileKey}. Lines: {LineCount}, Length: {ContentLength}",
                context.ToolName, fileKey, lineCount, outputString.Length);

            return FormatTruncationMessage(fileKey, contentType, lineCount, outputString);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save large tool output for tool {ToolName}", context.ToolName);

            // Return error message when storage fails
            return $"Error: Tool output size exceeds maximum allowed limit.";
        }
    }

    /// <summary>
    /// Determines if the output should be truncated based on size thresholds.
    /// </summary>
    /// <param name="output">The tool output to check</param>
    /// <returns>True if the output exceeds size thresholds</returns>
    public bool ShouldTruncate(string? output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return false;
        }

        return output.Length > _maxCharacterCount;
    }

    /// <summary>
    /// Formats the truncation message that replaces the original output.
    /// Uses the common section formatting from ToolOutputUtilities.
    /// </summary>
    /// <param name="fileKey">The file key where full content is stored</param>
    /// <param name="contentType">The detected content type</param>
    /// <param name="lineCount">Total number of lines in original content</param>
    /// <param name="originalOutput">The original full output</param>
    /// <returns>Formatted truncation message with preview</returns>
    public string FormatTruncationMessage(string fileKey, string contentType, int lineCount, string originalOutput)
    {
        var preview = new StringBuilder();

        preview.AppendLine("> This is **only a partial preview** of the full tool output.");
        preview.AppendLine("> Use the appropriate tool to retrieve more content from the stored file.");
        preview.AppendLine();
        preview.AppendLine($"**File Key:** `{fileKey}`");
        preview.AppendLine();
        preview.AppendLine("**Metadata**");

        preview.AppendLine($"- Content type: `{contentType}`");
        preview.AppendLine($"- Total size: `{ToolOutputHelper.FormatFileSize(originalOutput.Length)}`");
        preview.AppendLine($"- Total lines: `{lineCount:N0}`");

        // Add schema if it's JSON
        if (contentType == "json")
        {
            var schema = ToolOutputHelper.InferJsonSchema(originalOutput);
            if (!string.IsNullOrEmpty(schema))
            {
                preview.AppendLine($"- JSON schema (inferred):");
                preview.AppendLine("  ```json");
                foreach (var line in schema.Split('\n'))
                {
                    preview.AppendLine($"  {line}");
                }
                preview.AppendLine("  ```");
            }
        }

        preview.AppendLine();
        preview.AppendLine("**Preview**");

        var previewContent = ToolOutputHelper.GetPreviewContent(originalOutput, PreviewMaxLines, PreviewMaxChars);
        preview.AppendLine($"```{contentType}");
        preview.AppendLine(previewContent);
        preview.AppendLine("```");

        return preview.ToString();
    }
}
