//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Web.Json;

public class SettableJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
        {
            return false;
        }

        if (typeToConvert.GetGenericTypeDefinition() != typeof(Settable<>))
        {
            return false;
        }

        return true;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type valueType = typeToConvert.GetGenericArguments()[0];

        var converter = Activator.CreateInstance(
            type: typeof(SettableJsonConverterInner<>).MakeGenericType(new Type[] { valueType }),
            bindingAttr: BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            args: null,
            culture: null) as JsonConverter;

        if (converter == null)
        {
            throw new InvalidOperationException($"Unable to create converter for type {typeToConvert}.");
        }

        return converter;
    }

    private class SettableJsonConverterInner<T> : JsonConverter<Settable<T>>
    {
        public SettableJsonConverterInner()
        {
        }

        public override bool HandleNull => true;

        public override Settable<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var newOptions = new JsonSerializerOptions(options);
            newOptions.AllowOutOfOrderMetadataProperties = true;
            T? value = JsonSerializer.Deserialize<T>(ref reader, newOptions);

            return new Settable<T>(value);
        }

        public override void Write(Utf8JsonWriter writer, Settable<T> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}
