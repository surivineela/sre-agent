// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Core.Helpers;

public static class WebJsonSerializer
{
    public static JsonOptions JsonOptions { get; } = GetCustomJsonOptions();

    public static JsonSerializerOptions SerializerOptions { get; } = JsonOptions.JsonSerializerOptions;

    public static string Serialize<TValue>(TValue value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    public static TValue? DeserializeNoThrow<TValue>(this string json)
    {
        try
        {
            return JsonSerializer.Deserialize<TValue>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static bool TryDeserialize<TValue>(this string json, [NotNullWhen(true)] out TValue? result)
    {
        try
        {
            result = Deserialize<TValue>(json);
            if (result is null)
            {
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            result = default;
            return false;
        }
    }

    public static TValue DeserializeOrThrow<TValue>(string json)
    {
        var t = Deserialize<TValue>(json);
        if (t == null)
        {
            throw new InvalidDataException($"Failed to deserialize to type {typeof(TValue)}");
        }

        return t;
    }

    private static TValue? Deserialize<TValue>(this string json)
    {
        return JsonSerializer.Deserialize<TValue>(json, SerializerOptions);
    }

    private static JsonOptions GetCustomJsonOptions()
    {
        var jsonOptions = new JsonOptions();
        CustomizeSerializer(jsonOptions.JsonSerializerOptions);
        return jsonOptions;
    }

    private static void CustomizeSerializer(JsonSerializerOptions serializerOptions)
    {
        serializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        serializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
        serializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
    }
}
