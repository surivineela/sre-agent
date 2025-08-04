// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agent.Core.Serialization;

public static class Constants
{
    public const string TypePropertyName = "$type";
}

public interface IPolymorphic
{
    static abstract Type GetSubType(string typeValue);
}

public abstract record PolymorphicBase
{
    [JsonProperty(Constants.TypePropertyName)]
    public virtual string Type => GetType().Name;
}

public class PolymorphicJsonConverter<TClass> : JsonConverter
    where TClass : PolymorphicBase, IPolymorphic
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(TClass).IsAssignableFrom(objectType);
    }

    public override bool CanWrite
    {
        get { return false; }
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);
        var typeToken = token[Constants.TypePropertyName] ?? throw new InvalidOperationException("invalid object");
        var actualType = TClass.GetSubType(typeToken.ToString());

        object? returnVal = null;

        if (existingValue == null)
        {
            var contract = serializer.ContractResolver.ResolveContract(actualType);

            if (contract != null && contract.DefaultCreator != null)
            {
                returnVal = contract.DefaultCreator();
            }
            else
            {
                throw new InvalidOperationException($"Unable to create object of type {actualType}");
            }
        }
        else if (existingValue.GetType() != actualType)
        {
            throw new InvalidOperationException("invalid object");
        }

        using (var subReader = token.CreateReader())
        {
            serializer.Populate(subReader, returnVal!);
        }

        return returnVal!;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
