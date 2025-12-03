// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Core.JsonConverters;

/// <summary>
/// Helper class for converting between JSON and YAML formats
/// </summary>
public static class YamlJsonConverter
{
    /// <summary>
    /// Converts a JsonElement to YAML format
    /// </summary>
    /// <param name="jsonElement">The JsonElement to convert</param>
    /// <returns>YAML string representation</returns>
    public static string ConvertJsonElementToYaml(JsonElement jsonElement)
    {
        // Handle primitive types directly
        if (jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False)
        {
            return jsonElement.GetBoolean().ToString().ToLower();
        }
        else if (jsonElement.ValueKind == JsonValueKind.String)
        {
            return jsonElement.GetString() ?? string.Empty;
        }
        else if (jsonElement.ValueKind == JsonValueKind.Number)
        {
            return jsonElement.GetRawText();
        }
        else if (jsonElement.ValueKind == JsonValueKind.Null)
        {
            return "null";
        }
        else
        {
            // For complex types (objects/arrays), convert through YAML serializer
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            // Convert JsonElement to object for YAML serialization
            object? yamlObject = ConvertJsonElementToObject(jsonElement);
            return serializer.Serialize(yamlObject ?? new object());
        }
    }

    /// <summary>
    /// Converts a JsonElement to a plain object (Dictionary or List) for YAML serialization
    /// </summary>
    private static object? ConvertJsonElementToObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                {
                    dict[property.Name] = ConvertJsonElementToObject(property.Value);
                }
                return dict;

            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonElementToObject(item));
                }
                return list;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt32(out int intValue))
                    return intValue;
                if (element.TryGetInt64(out long longValue))
                    return longValue;
                if (element.TryGetDouble(out double doubleValue))
                    return doubleValue;
                return element.GetDecimal();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
            default:
                return null;
        }
    }

    /// <summary>
    /// Converts YAML content to a JsonElement
    /// </summary>
    /// <param name="yamlContent">The YAML content to convert</param>
    /// <returns>JsonElement representation of the YAML content</returns>
    public static JsonElement ConvertYamlToJsonElement(string yamlContent)
    {
        var deserializer = new DeserializerBuilder()
            .Build();
        var yamlObject = deserializer.Deserialize(new System.IO.StringReader(yamlContent));
        var jsonString = JsonSerializer.Serialize(yamlObject ?? new object());
        return JsonDocument.Parse(jsonString).RootElement;
    }
}
