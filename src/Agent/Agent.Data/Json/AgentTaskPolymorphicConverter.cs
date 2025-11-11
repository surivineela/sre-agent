// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.Json;

/// <summary>
///
/// Custom System.Text.Json converter for polymorphic deserialization of AgentTaskProperties.
/// 
/// This is required because CosmosSystemTextJsonSerializer forces System.Text.Json serialization,
/// but System.Text.Json cannot deserialize abstract types without help. This converter:
/// 1. Reads the "$type" discriminator property to determine the concrete type
/// 2. Falls back to IncidentInvestigationTaskProperties for backward compatibility with old data
/// </summary>
/// </summary>
public class AgentTaskPropertiesConverter : JsonConverter<AgentTaskProperties>
{
    public override AgentTaskProperties Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Look for the $type discriminator property
        Type actualType;
        if (root.TryGetProperty("$type", out var typeProperty))
        {
            var typeName = typeProperty.GetString();
            actualType = AgentTaskProperties.GetSubType(typeName!);
        }
        else
        {
            // No $type property - default to IncidentInvestigationTaskProperties for backward compatibility
            actualType = typeof(IncidentInvestigationTaskProperties);
        }

        var optionsWithoutConverter = CreateOptionsWithoutThisConverter(options);
        return (AgentTaskProperties)JsonSerializer.Deserialize(root.GetRawText(), actualType, optionsWithoutConverter)!
            ?? throw new JsonException($"Failed to deserialize AgentTaskProperties");
    }

    public override void Write(Utf8JsonWriter writer, AgentTaskProperties value, JsonSerializerOptions options)
    {
        var optionsWithoutConverter = CreateOptionsWithoutThisConverter(options);
        JsonSerializer.Serialize(writer, value, value.GetType(), optionsWithoutConverter);
    }

    private JsonSerializerOptions CreateOptionsWithoutThisConverter(JsonSerializerOptions baseOptions)
    {
        var options = new JsonSerializerOptions(baseOptions);
        options.Converters.Clear();
        foreach (var converter in baseOptions.Converters)
        {
            if (converter is not AgentTaskPropertiesConverter)
            {
                options.Converters.Add(converter);
            }
        }
        return options;
    }
}

/// <summary>
/// Custom JsonConverter to handle polymorphic deserialization of AgentTaskInputData
/// </summary>
public class AgentTaskInputDataConverter : JsonConverter<AgentTaskInputData>
{
    public override AgentTaskInputData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Look for the $type discriminator property
        Type actualType;
        if (root.TryGetProperty("$type", out var typeProperty))
        {
            var typeName = typeProperty.GetString();
            actualType = AgentTaskInputData.GetSubType(typeName!);
        }
        else
        {
            // No $type property - default to IncidentInvestigationTaskInputData for backward compatibility
            actualType = typeof(IncidentInvestigationTaskInputData);
        }

        var optionsWithoutConverter = CreateOptionsWithoutThisConverter(options);
        return (AgentTaskInputData)JsonSerializer.Deserialize(root.GetRawText(), actualType, optionsWithoutConverter)!
            ?? throw new JsonException($"Failed to deserialize AgentTaskInputData");
    }

    public override void Write(Utf8JsonWriter writer, AgentTaskInputData value, JsonSerializerOptions options)
    {
        var optionsWithoutConverter = CreateOptionsWithoutThisConverter(options);
        JsonSerializer.Serialize(writer, value, value.GetType(), optionsWithoutConverter);
    }

    private JsonSerializerOptions CreateOptionsWithoutThisConverter(JsonSerializerOptions baseOptions)
    {
        var options = new JsonSerializerOptions(baseOptions);
        options.Converters.Clear();
        foreach (var converter in baseOptions.Converters)
        {
            if (converter is not AgentTaskInputDataConverter)
            {
                options.Converters.Add(converter);
            }
        }
        return options;
    }
}
