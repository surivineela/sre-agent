// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Core.JsonConverters;

public class IPEndPointConverter : JsonConverter<IPEndPoint>
{
    public override void Write(Utf8JsonWriter writer, IPEndPoint value, JsonSerializerOptions options)
    {
        writer.WritePropertyName("Address");
        writer.WriteStringValue(value.Address.ToString());
        writer.WritePropertyName("Port");
        writer.WriteNumberValue(value.Port);
    }

    public override IPEndPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var el = JsonDocument.ParseValue(ref reader).RootElement.Clone();
        if (!el.TryGetProperty("Address", out var addressElement) || !el.TryGetProperty("Port", out var portElement))
        {
            throw new JsonException("Invalid JSON format for IPEndPoint.");
        }

        var address = addressElement.GetString()!;
        var port = portElement.GetInt32();
        return new IPEndPoint(IPAddress.Parse(address), port);
    }
}
