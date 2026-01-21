// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Agent.Common;

/// <summary>
/// Shared utility functions for processing and formatting tool outputs.
/// </summary>
public static class ToolOutputHelper
{
    private const int DefaultPreviewMaxLines = 20;
    private const int DefaultPreviewMaxChars = 4096;
    private const int MaxSchemaDepth = 3;
    private const int MaxSchemaRequiredFields = 5;

    /// <summary>
    /// Gets preview content (first N lines or M chars, whichever is shorter).
    /// </summary>
    /// <param name="content">The content to preview</param>
    /// <param name="maxLines">Maximum number of lines to include (default: 20)</param>
    /// <param name="maxChars">Maximum number of characters to include (default: 4096)</param>
    /// <returns>Preview of the content</returns>
    public static string GetPreviewContent(string content, int maxLines = DefaultPreviewMaxLines, int maxChars = DefaultPreviewMaxChars)
    {
        var lines = content.Split('\n');
        var previewLines = new List<string>();
        var charCount = 0;

        for (int i = 0; i < Math.Min(lines.Length, maxLines); i++)
        {
            var line = lines[i];
            var remainingChars = maxChars - charCount;

            if (remainingChars <= 0)
            {
                break;
            }

            if (line.Length <= remainingChars)
            {
                previewLines.Add(line);
                charCount += line.Length + 1;
            }
            else
            {
                previewLines.Add(line.Substring(0, remainingChars));
                break;
            }
        }

        return string.Join('\n', previewLines);
    }

    /// <summary>
    /// Formats file size in a human-readable format.
    /// </summary>
    /// <param name="sizeInChars">Size in characters/bytes</param>
    /// <returns>Human-readable file size string</returns>
    public static string FormatFileSize(int sizeInChars)
    {
        if (sizeInChars < 1024)
        {
            return $"{sizeInChars} bytes";
        }
        else if (sizeInChars < 1024 * 1024)
        {
            return $"{sizeInChars / 1024.0:F1} KB";
        }
        else
        {
            return $"{sizeInChars / (1024.0 * 1024.0):F1} MB";
        }
    }

    /// <summary>
    /// Detects the content type based on content patterns.
    /// </summary>
    /// <param name="content">The content to analyze</param>
    /// <returns>Content type string (json, yaml, xml, or txt)</returns>
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


    /// <summary>
    /// Attempts to infer a JSON schema from the content.
    /// </summary>
    /// <param name="jsonContent">The JSON content to infer schema from</param>
    /// <returns>JSON schema string or null if inference fails</returns>
    public static string? InferJsonSchema(string jsonContent)
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
    /// Recursively infers schema from JSON element (limited depth to avoid large schemas).
    /// </summary>
    /// <param name="element">The JSON element to infer schema from</param>
    /// <param name="depth">Current recursion depth</param>
    /// <returns>Dictionary representing the JSON schema</returns>
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

                if (required.Count > 0 && required.Count <= MaxSchemaRequiredFields)
                {
                    objSchema["required"] = required;
                }

                return objSchema;

            case JsonValueKind.Array:
                var arraySchema = new Dictionary<string, object>
                {
                    ["type"] = "array"
                };

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
    /// Formats output section metadata for tool output processors.
    /// This provides a consistent format for sections saved to file.
    /// </summary>
    /// <param name="sectionName">The name of the section (e.g., "STDOUT", "EXECUTION RESULT")</param>
    /// <param name="fileKey">The file key where content is saved</param>
    /// <param name="contentType">The content type (e.g., "json", "txt")</param>
    /// <param name="content">The original content</param>
    /// <param name="previewMaxLines">Maximum number of lines in preview</param>
    /// <param name="previewMaxChars">Maximum number of characters in preview</param>
    /// <returns>Formatted section string</returns>
    public static string FormatSavedToFileSection(
        string sectionName,
        string fileKey,
        string contentType,
        string content,
        int previewMaxLines = DefaultPreviewMaxLines,
        int previewMaxChars = DefaultPreviewMaxChars)
    {
        var sb = new StringBuilder();
        var lineCount = content.Split('\n').Length;
        var preview = GetPreviewContent(content, previewMaxLines, previewMaxChars);

        sb.AppendLine($"=== {sectionName} ===");
        sb.AppendLine("This is **only a partial preview** of the full content.");
        sb.AppendLine("Use the appropriate tool to retrieve more content from the stored file.");
        sb.AppendLine("This file is not the result of execution. Do not mention the file in your response.");
        sb.AppendLine($"File Key: `{fileKey}`");
        sb.AppendLine($"Content Type: {contentType}");
        sb.AppendLine($"Total Size: {FormatFileSize(content.Length)}");
        sb.AppendLine($"Total Lines: {lineCount:N0}");

        // Add inferred schema for JSON content
        if (contentType == "json")
        {
            var schema = InferJsonSchema(content);
            if (!string.IsNullOrEmpty(schema))
            {
                sb.AppendLine("JSON Schema (inferred):");
                sb.AppendLine("```json");
                sb.AppendLine(schema);
                sb.AppendLine("```");
            }
        }

        sb.AppendLine("Preview:");
        sb.AppendLine($"```{contentType}");
        sb.AppendLine(preview);
        sb.AppendLine("```");
        sb.AppendLine($"=== END {sectionName} ===");

        return sb.ToString();
    }

    /// <summary>
    /// Formats an inline content section for tool output processors.
    /// Used when content is small enough to include directly.
    /// </summary>
    /// <param name="sectionName">The name of the section</param>
    /// <param name="content">The content to include</param>
    /// <param name="maxLength">Maximum length before truncation</param>
    /// <returns>Formatted section string</returns>
    public static string FormatInlineSection(
        string sectionName,
        string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {sectionName} ===");
        sb.AppendLine(content);
        sb.AppendLine($"=== END {sectionName} ===");
        return sb.ToString();
    }
}
