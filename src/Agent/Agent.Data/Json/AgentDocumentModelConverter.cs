// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Data.DataModels;
using Agent.Data.DataModels.Legacy;
using Microsoft.Azure.Amqp.Framing;

namespace Agent.Data.Json;

/// <summary>
/// Custom JsonConverter to handle backward compatibility between legacy and new document model schemas.
/// </summary>
public class LegacyDocumentModelConverter<TModel, TModelLegacy> : JsonConverter<TModel>
where TModel : notnull
where TModelLegacy : ILegacyModelConverter<TModel>
{
    public override TModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Check if this is the new schema (has "Spec" and "Metadata" at root level)
        // Try both PascalCase and camelCase property names
        var hasSpec = root.TryGetProperty("Spec", out _) || root.TryGetProperty("spec", out _);
        var hasMetadata = root.TryGetProperty("Metadata", out _) || root.TryGetProperty("metadata", out _);

        if (hasSpec && hasMetadata)
        {
            // manual handle JsonPolymorphic deserialization due to custom converter does not work with JsonPolymorphic
            var polymorphicAttributes = typeToConvert.GetCustomAttributes(typeof(CustomizedJsonPolymorphicAttribute), true);
            var derivedTypeAttributes = typeToConvert.GetCustomAttributes(typeof(CustomizedJsonDerivedTypeAttribute), true);

            if (polymorphicAttributes.Length > 0 && derivedTypeAttributes.Length > 0)
            {
                var targetType = GetDerivedType(polymorphicAttributes, derivedTypeAttributes, root);

                var optionsWithoutConverter = CreateOptionsWithoutThisConverter(options);
                return (TModel)JsonSerializer.Deserialize(root.GetRawText(), targetType, optionsWithoutConverter)!
                    ?? throw new JsonException($"Failed to deserialize new schema {targetType.Name}.");
            }
            else
            {
                var optionsWithoutConverter = CreateOptionsWithoutThisConverter(options);
                return JsonSerializer.Deserialize<TModel>(root.GetRawText(), optionsWithoutConverter)
                    ?? throw new JsonException($"Failed to deserialize new schema {typeof(TModel).Name}");
            }

        }
        else
        {
            // Legacy schema - convert to new schema
            return ConvertLegacyToNew(root, options);
        }
    }

    public override void Write(Utf8JsonWriter writer, TModel value, JsonSerializerOptions options)
    {
        var optionsWithoutConverter = CreateOptionsWithoutThisConverter(options);
        JsonSerializer.Serialize(writer, value, value.GetType(), optionsWithoutConverter);
    }

    private TModel ConvertLegacyToNew(JsonElement root, JsonSerializerOptions options)
    {
        // Deserialize the legacy model first
        var legacyModel = JsonSerializer.Deserialize<TModelLegacy>(
            root.GetRawText(),
            CreateOptionsWithoutThisConverter(options));

        if (legacyModel == null)
        {
            throw new JsonException($"Failed to deserialize legacy schema {typeof(TModelLegacy).Name}");
        }

        return legacyModel.ToNewModel();
    }

    private JsonSerializerOptions CreateOptionsWithoutThisConverter(JsonSerializerOptions baseOptions)
    {
        var options = new JsonSerializerOptions(baseOptions);

        // Remove this converter to avoid infinite recursion
        options.Converters.Clear();
        foreach (var converter in baseOptions.Converters)
        {
            if (converter is not LegacyDocumentModelConverter<TModel, TModelLegacy>)
            {
                options.Converters.Add(converter);
            }
        }

        return options;
    }

    private Type GetDerivedType(object[] polymorphicAttributes, object[] derivedTypeAttributes, JsonElement root)
    {
        Dictionary<string, Type> derivedTypes = new Dictionary<string, Type>();
        foreach (CustomizedJsonDerivedTypeAttribute attr in derivedTypeAttributes)
        {
            derivedTypes[attr.Value] = attr.Type;
        }

        string discriminatorValue;
        if (root.TryGetProperty(((CustomizedJsonPolymorphicAttribute)polymorphicAttributes[0]).TypeDiscriminatorPropertyName, out var typeProperty))
        {
            if (derivedTypes.ContainsKey(typeProperty.GetString()!))
            {
                discriminatorValue = typeProperty.GetString()!;
                return derivedTypes[discriminatorValue];
            }
            else
            {
                throw new JsonException($"Unknown type discriminator value '{typeProperty.GetString()}' in JSON.");
            }
        }
        else
        {
            throw new JsonException($"Unknown type discriminator value '{typeProperty.GetString()}' in JSON.");
        }
    }
}
