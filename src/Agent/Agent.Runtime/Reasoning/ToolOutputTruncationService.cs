// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.IO;
using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Service for truncating large tool outputs and storing them in external storage
/// </summary>
public class ToolOutputTruncationService : IToolOutputTruncationService
{
    private readonly IToolOutputStorage _toolOutputStorage;
    private readonly ILogger<ToolOutputTruncationService> _logger;
    private readonly int _maxCharacterCount;

    private const int PreviewMaxLines = 20;
    private const int PreviewMaxChars = 4096;
    private const int MaxSchemaDepth = 3;
    private const int MaxSchemaRequiredFields = 5;

    public ToolOutputTruncationService(
        IToolOutputStorage toolOutputStorage,
        ILogger<ToolOutputTruncationService> logger,
        IOptions<ToolOutputSettings> settings)
    {
        _toolOutputStorage = toolOutputStorage;
        _logger = logger;
        _maxCharacterCount = settings.Value.MaxOutputChars;
    }

    /// <summary>
    /// Determines if the output should be truncated based on size thresholds
    /// </summary>
    /// <param name="output">The tool output to check</param>
    /// <returns>True if the output exceeds size thresholds</returns>
    public bool ShouldTruncate(string? output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return false;
        }

        // Check character count
        return output.Length > _maxCharacterCount;
    }

    /// <summary>
    /// Processes tool output, truncating and storing large outputs
    /// </summary>
    /// <param name="threadId">The thread ID for this execution</param>
    /// <param name="toolName">The name of the tool that produced this output</param>
    /// <param name="output">The original tool output (string or object that will be serialized to JSON)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The processed output - either the original if small enough,
    /// or a truncation message with file reference if large
    /// </returns>
    public async Task<object?> ProcessToolOutputAsync(
        Guid threadId,
        string toolName,
        object? output,
        CancellationToken cancellationToken = default)
    {
        if (output == null)
        {
            return output;
        }

        // Don't truncate output from certain tools:
        // - ToolOutputRetriever: already retrieving truncated content
        // - read_skill_file: skill content needs to be preserved in full
        // - ToDoWrite: todo list planning output should be preserved for context
        if (string.Equals(toolName, "ToolOutputRetriever", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(toolName, "read_skill_file", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(toolName, "ToDoWrite", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInternalInformation("Returning full output for excluded tool {ToolName}", toolName);
            return output;
        }

        // Convert output to string representation
        string outputString;
        if (output is string str)
        {
            outputString = str;
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
                _logger.LogInternalWarning(ex, "Failed to serialize tool output for tool {ToolName}, returning empty string", toolName);
                outputString = string.Empty;
            }
        }

        if (!ShouldTruncate(outputString))
        {
            _logger.LogInternalInformation("Output for tool {ToolName} does not require truncation (Length: {Length})",
                toolName, outputString.Length);
            return output;
        }

        try
        {
            var contentType = DetectContentType(outputString);
            var fileKey = await _toolOutputStorage.SaveAsync(
                threadId,
                toolName,
                outputString,
                contentType,
                cancellationToken);

            var lineCount = outputString.Split('\n').Length;

            _logger.LogInternalInformation(
                "Truncated large tool output for tool {ToolName}, saved as {FileKey}. Lines: {LineCount}, Length: {ContentLength}",
                toolName, fileKey, lineCount, outputString.Length);

            return FormatTruncationMessage(fileKey, contentType, lineCount, outputString);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to save large tool output for tool {ToolName}", toolName);

            // Return error message when storage fails
            return $"Error: Tool output size exceeds maximum allowed limit.";
        }
    }

    /// <summary>
    /// Processes tool output using a generic context, truncating and storing large outputs
    /// </summary>
    public async Task<object?> ProcessToolOutputAsync<TContext>(
        TContext? context,
        string toolName,
        object? output,
        CancellationToken cancellationToken = default) where TContext : class
    {
        // Extract ThreadId from context using reflection
        var threadIdProperty = typeof(TContext).GetProperty("ThreadId");
        if (threadIdProperty == null)
        {
            _logger.LogInternalWarning("Context type {ContextType} does not have a ThreadId property", typeof(TContext).Name);
            return output;
        }

        var threadIdValue = threadIdProperty.GetValue(context);
        if (threadIdValue is not Guid threadId)
        {
            _logger.LogInternalWarning("ThreadId property in context is not a Guid");
            return output;
        }

        // Delegate to the main implementation
        return await ProcessToolOutputAsync(threadId, toolName, output, cancellationToken);
    }

    /// <summary>
    /// Formats the truncation message that replaces the original output
    /// </summary>
    public string FormatTruncationMessage(string fileKey, string contentType, int lineCount, string originalOutput)
    {
        var preview = new StringBuilder();

        preview.AppendLine("> This is **only a partial preview** of the full tool output.");
        preview.AppendLine("> Use the appropriate tool to retrieve more content from the stored file.");
        preview.AppendLine();
        preview.AppendLine($"**File Key:** `{fileKey}`");
        preview.AppendLine();
        preview.AppendLine("**Metadata**");

        // Detect content type from extension
        preview.AppendLine($"- Content type: `{contentType}`");
        preview.AppendLine($"- Total size: `{FormatFileSize(originalOutput.Length)}`");
        preview.AppendLine($"- Total lines: `{lineCount:N0}`");

        // Add schema if it's JSON or YAML
        if (contentType == "json")
        {
            var schema = InferJsonSchema(originalOutput);
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

        // Get preview content (first 20 lines or first 4K characters, whichever is shorter)
        var previewContent = GetPreviewContent(originalOutput);
        preview.AppendLine($"```{contentType}");
        preview.AppendLine(previewContent);
        preview.AppendLine("```");

        return preview.ToString();
    }

    /// <summary>
    /// Gets preview content (first 20 lines or 4K chars, whichever is shorter)
    /// </summary>
    public static string GetPreviewContent(string content)
    {
        var lines = content.Split('\n');
        var previewLines = new List<string>();
        var charCount = 0;

        for (int i = 0; i < Math.Min(lines.Length, PreviewMaxLines); i++)
        {
            var line = lines[i];
            var remainingChars = PreviewMaxChars - charCount;

            if (remainingChars <= 0)
            {
                break;
            }

            // If the line fits entirely, add it
            if (line.Length <= remainingChars)
            {
                previewLines.Add(line);
                charCount += line.Length + 1; // +1 for newline
            }
            else
            {
                // Truncate the line to fit within remaining chars
                previewLines.Add(line.Substring(0, remainingChars));
                break;
            }
        }

        return string.Join('\n', previewLines);
    }

    /// <summary>
    /// Formats file size in a human-readable format
    /// </summary>
    private string FormatFileSize(int sizeInChars)
    {
        // Approximate bytes (assuming UTF-8, most chars are 1 byte)
        var sizeInBytes = sizeInChars;

        if (sizeInBytes < 1024)
        {
            return $"{sizeInBytes} bytes";
        }
        else if (sizeInBytes < 1024 * 1024)
        {
            return $"{sizeInBytes / 1024.0:F1} KB";
        }
        else
        {
            return $"{sizeInBytes / (1024.0 * 1024.0):F1} MB";
        }
    }

    /// <summary>
    /// Attempts to infer a JSON schema from the content
    /// </summary>
    private static string? InferJsonSchema(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var schema = InferSchemaFromElement(doc.RootElement, 0);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            return JsonSerializer.Serialize(schema, options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Recursively infers schema from JSON element (limited depth to avoid large schemas)
    /// </summary>
    public static object InferSchemaFromElement(JsonElement element, int depth)
    {
        if (depth > MaxSchemaDepth)
        {
            return new Dictionary<string, object> { ["type"] = "object" };
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var objSchema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>()
                };

                var properties = (Dictionary<string, object>)objSchema["properties"];
                var required = new List<string>();

                foreach (var prop in element.EnumerateObject())
                {
                    properties[prop.Name] = InferSchemaFromElement(prop.Value, depth + 1);
                    required.Add(prop.Name);
                }

                if (required.Count > 0 && required.Count <= MaxSchemaRequiredFields) // Only show required for small objects
                {
                    objSchema["required"] = required;
                }

                return objSchema;

            case JsonValueKind.Array:
                var arraySchema = new Dictionary<string, object>
                {
                    ["type"] = "array"
                };

                // Infer items type from first element
                if (element.GetArrayLength() > 0)
                {
                    var firstElement = element.EnumerateArray().First();
                    arraySchema["items"] = InferSchemaFromElement(firstElement, depth + 1);
                }

                return arraySchema;

            case JsonValueKind.String:
                return new Dictionary<string, object> { ["type"] = "string" };

            case JsonValueKind.Number:
                return new Dictionary<string, object> { ["type"] = "number" };

            case JsonValueKind.True:
            case JsonValueKind.False:
                return new Dictionary<string, object> { ["type"] = "boolean" };

            case JsonValueKind.Null:
                return new Dictionary<string, object> { ["type"] = "null" };

            default:
                return new Dictionary<string, object> { ["type"] = "object" };
        }
    }

    /// <summary>
    /// Detects the content type and returns it without the dot prefix.
    /// Uses parsing validation with System.Text.Json and YamlDotNet.
    /// Only detects structured YAML (containing ':' or '-') to avoid false positives.
    /// </summary>
    /// <param name="content">The content to analyze</param>
    /// <returns>Content type (json, yaml, or txt)</returns>
    public static string DetectContentType(string content)
    {
        var trimmedContent = content.TrimStart();

        // Try parsing as JSON first
        if (trimmedContent.StartsWith("{") || trimmedContent.StartsWith("["))
        {
            try
            {
                using var document = JsonDocument.Parse(content);
                return "json";
            }
            catch (JsonException)
            {
                // Not valid JSON, return text
                return "txt";
            }
        }

        // Try parsing as YAML only if it looks like structured YAML
        if (IsLikelyYaml(trimmedContent))
        {
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                var result = deserializer.Deserialize(new StringReader(content));

                // Only consider it YAML if it's not just a plain string scalar
                if (result != null && result.GetType() != typeof(string))
                {
                    return "yaml";
                }
            }
            catch (YamlException)
            {
                // Not valid YAML, fall through to default
            }
        }

        // Default to plain text
        return "txt";
    }



    /// <summary>
    /// Checks if content looks like YAML based on common patterns.
    /// Looks for key-value pairs, document markers, or list items.
    /// </summary>
    /// <param name="content">The content to check</param>
    /// <returns>True if content has YAML-like patterns</returns>
    public static bool IsLikelyYaml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var trimmed = content.TrimStart();

        // YAML typically has key: value pairs, starts with ---, or has list items with -
        return trimmed.Contains(": ") ||
               trimmed.StartsWith("---") ||
               content.Contains("\n- ") ||
               trimmed.StartsWith("- ");
    }
}
