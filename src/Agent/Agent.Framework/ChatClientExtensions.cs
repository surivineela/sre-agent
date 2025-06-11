// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

// based on https://github.com/dotnet/extensions/blob/34cdd3a2ddeea9e329356448719ea0d9b896c19c/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/ChatClientStructuredOutputExtensions.cs#L150
// adapted to take a Type parameter instead of using generics because our output types are not known at compile time
public static class ChatClientExtensions
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = AIJsonUtilities.DefaultOptions;

    private static readonly AIJsonSchemaCreateOptions _inferenceOptions = new()
    {
        IncludeSchemaKeyword = true,
        TransformOptions = new AIJsonSchemaTransformOptions
        {
            DisallowAdditionalProperties = true,
            RequireAllProperties = true,
            MoveDefaultKeywordToDescription = true,
        },
    };

    private static readonly Regex _invalidNameCharsRegex = new("[^0-9A-Za-z_]", RegexOptions.Compiled);

    public static async Task<(ChatResponse response, object? result)> GetResponseAsync(
        this IChatClient client,
        IEnumerable<ChatMessage> messages,
        Type outputType,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var schemaElement = AIJsonUtilities.CreateJsonSchema(
            type: outputType,
            serializerOptions: _jsonSerializerOptions,
            inferenceOptions: _inferenceOptions);

        bool isWrappedInObject;
        JsonElement schema;
        if (SchemaRepresentsObject(schemaElement))
        {
            // For object-representing schemas, we can use them as-is
            isWrappedInObject = false;
            schema = schemaElement;
        }
        else
        {
            // For non-object-representing schemas, we wrap them in an object schema, because all
            // the real LLM providers today require an object schema as the root. This is currently
            // true even for providers that support native structured output.
            isWrappedInObject = true;
            schema = JsonSerializer.SerializeToElement(new JsonObject
            {
                { "$schema", "https://json-schema.org/draft/2020-12/schema" },
                { "type", "object" },
                { "properties", new JsonObject { { "data", JsonElementToJsonNode(schemaElement) } } },
                { "additionalProperties", false },
                { "required", new JsonArray("data") },
            }, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(JsonObject)));
        }

        options = options is not null ? options.Clone() : new();

        options.ResponseFormat = ChatResponseFormat.ForJsonSchema(
            schema,
            schemaName: SanitizeMemberName(outputType.Name),
            schemaDescription: outputType.GetCustomAttribute<DescriptionAttribute>()?.Description);

        var chatResponse = await client.GetResponseAsync(messages, options, cancellationToken);

        // if tool calls are being made, don't try to parse the response
        if (chatResponse.FinishReason == ChatFinishReason.ToolCalls)
        {
            return (chatResponse, null);
        }

        var json = chatResponse.Text;

        if (string.IsNullOrEmpty(json))
        {
            throw new InvalidOperationException("The response did not contain any JSON output");
        }

        if (isWrappedInObject)
        {
            if (JsonDocument.Parse(json).RootElement.TryGetProperty("data", out var data))
            {
                json = data.GetRawText();
            }
            else
            {
                throw new InvalidOperationException("The response did not contain a valid JSON object with a 'data' property");
            }
        }

        object? deserializedResult = default;

        try
        {
            deserializedResult = JsonSerializer.Deserialize(json, outputType, _jsonSerializerOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to deserialize the response", ex);
        }

        return (chatResponse, deserializedResult);
    }

    private static bool SchemaRepresentsObject(JsonElement schemaElement)
    {
        if (schemaElement.ValueKind is JsonValueKind.Object)
        {
            foreach (var property in schemaElement.EnumerateObject())
            {
                if (property.NameEquals("type"u8))
                {
                    return property.Value.ValueKind == JsonValueKind.String
                        && property.Value.ValueEquals("object"u8);
                }
            }
        }

        return false;
    }

    private static JsonNode? JsonElementToJsonNode(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Array => JsonArray.Create(element),
            JsonValueKind.Object => JsonObject.Create(element),
            _ => JsonValue.Create(element)
        };
    }

    /// <summary>
    /// Removes characters from a .NET member name that shouldn't be used in an AI function name.
    /// </summary>
    /// <param name="memberName">The .NET member name that should be sanitized.</param>
    /// <returns>
    /// Replaces non-alphanumeric characters in the identifier with the underscore character.
    /// Primarily intended to remove characters produced by compiler-generated method name mangling.
    /// </returns>
    private static string SanitizeMemberName(string memberName) =>
        _invalidNameCharsRegex.Replace(memberName, "_");
}
