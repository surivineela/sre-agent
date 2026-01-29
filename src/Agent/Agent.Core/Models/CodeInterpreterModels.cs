// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Core.Models;

public class CodeExecuteRequest
{
    public string Code { get; set; } = string.Empty;
    public int TimeoutInSeconds { get; set; }
    public bool EnableEgress { get; set; } = true;
    public string ExecutionType { get; set; } = "synchronous"; // synchronous for now
    public int? StandardMsgLength { get; set; }
}

public class CodeExecutionResponse
{
    public int? Hresult { get; set; }
    public string? Status { get; set; }

    /// <summary>
    /// The result of the code execution. Can be a string result or an image result.
    /// </summary>
    [JsonConverter(typeof(CodeExecutionResultConverter))]
    public CodeExecutionResult? Result { get; set; }

    [JsonPropertyName("error_name")]
    public string? ErrorName { get; set; }
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
    [JsonPropertyName("error_stack_trace")]
    public string? ErrorStackTrace { get; set; }
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public CodeExecutionDiagnosticInfo? DiagnosticInfo { get; set; }
    public string? OperationId { get; set; }

    /// <summary>
    /// Files auto-retrieved from the session after execution.
    /// Each entry contains file metadata and download link.
    /// </summary>
    [JsonIgnore]
    public List<CodeFileInfo>? RetrievedFiles { get; set; }

    /// <summary>
    /// Information about the image file saved from an image result.
    /// This is populated when the execution result is an image that was saved to storage.
    /// </summary>
    [JsonIgnore]
    public CodeFileInfo? ImageFile { get; set; }
}

/// <summary>
/// Information about a file retrieved from the code interpreter session.
/// </summary>
public class CodeFileInfo
{
    /// <summary>
    /// The filename.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// The relative download link (e.g., "/api/files/filename.png").
    /// </summary>
    public string DownloadLink { get; set; } = string.Empty;

    /// <summary>
    /// The file type category (e.g., "Image", "Data", "Document", "Code", "Archive").
    /// </summary>
    public string FileType { get; set; } = string.Empty;
}

/// <summary>
/// Base class for code execution results.
/// </summary>
public abstract class CodeExecutionResult
{
}

/// <summary>
/// Represents an image result from code execution.
/// </summary>
public class ImageExecutionResult : CodeExecutionResult
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "image";

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("base64_data")]
    public string? Base64Data { get; set; }
}

/// <summary>
/// Represents an object result from code execution.
/// </summary>
public class ObjectExecutionResult : CodeExecutionResult
{
    /// <summary>
    /// The value of the result. Can be a primitive (string, number, bool, null),
    /// an array (List&lt;object?&gt;), or a dictionary (Dictionary&lt;string, object?&gt;).
    /// </summary>
    public object? Value { get; set; }
}

/// <summary>
/// Custom JSON converter for <see cref="CodeExecutionResult"/> that handles both string and object formats.
/// </summary>
public class CodeExecutionResultConverter : JsonConverter<CodeExecutionResult>
{
    public override CodeExecutionResult? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeProperty) &&
                typeProperty.GetString() == "image")
            {
                return new ImageExecutionResult
                {
                    Type = "image",
                    Format = root.TryGetProperty("format", out var format) ? format.GetString() : null,
                    Base64Data = root.TryGetProperty("base64_data", out var data) ? data.GetString() : null
                };
            }

            throw new JsonException($"Unknown object format for CodeExecutionResult");
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString() ?? string.Empty;

            // For object and array result, code interpreter serializes the result
            if (TryParseJson(stringValue, out var jsonElement))
            {
                return new ObjectExecutionResult { Value = ConvertJsonElement(jsonElement) };
            }

            return new ObjectExecutionResult { Value = stringValue };
        }

        // For primitive types, code interpreter directly returns the value
        using var primitiveDoc = JsonDocument.ParseValue(ref reader);
        return new ObjectExecutionResult { Value = ConvertJsonElement(primitiveDoc.RootElement) };
    }

    public override void Write(Utf8JsonWriter writer, CodeExecutionResult value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case ImageExecutionResult imageResult:
                writer.WriteStartObject();
                writer.WriteString("type", imageResult.Type);
                if (imageResult.Format != null)
                {
                    writer.WriteString("format", imageResult.Format);
                }
                if (imageResult.Base64Data != null)
                {
                    writer.WriteString("base64_data", imageResult.Base64Data);
                }
                writer.WriteEndObject();
                break;
            case ObjectExecutionResult objectResult:
                JsonSerializer.Serialize(writer, objectResult.Value, options);
                break;
            default:
                throw new JsonException($"Unknown result type: {value.GetType()}");
        }
    }

    /// <summary>
    /// Converts a JsonElement to the corresponding C# type.
    /// </summary>
    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => GetNumber(element),
            _ => element,
        };
    }

    /// <summary>
    /// Gets the most appropriate numeric type from a JsonElement.
    /// </summary>
    private static object GetNumber(JsonElement element)
    {
        // Try integer types first
        if (element.TryGetInt64(out var longValue))
        {
            if (longValue >= int.MinValue && longValue <= int.MaxValue)
            {
                return (int)longValue;
            }
            return longValue;
        }

        // Fall back to double for floating point
        if (element.TryGetDouble(out var doubleValue))
        {
            return doubleValue;
        }

        // Last resort: decimal for high precision
        if (element.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        return element.GetRawText();
    }

    private static bool TryParseJson(string value, out JsonElement element)
    {
        element = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(value);
            element = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public class CodeExecutionDiagnosticInfo
{
    public int? ExecutionRequestTimeInMilliSeconds { get; set; }
    public int? ExecutionProcessResponseTimeInMilliSeconds { get; set; }
    public int? ExecutionDuration { get; set; }
    public string? Identifier { get; set; }
}
