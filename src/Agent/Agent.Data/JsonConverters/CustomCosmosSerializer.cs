// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace Agent.Data.JsonConverters;

public class CustomCosmosSerializer : CosmosSerializer
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream.CanSeek
                   && stream.Length == 0)
            {
                return default(T)!;
            }

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            return JsonSerializer.Deserialize<T>(stream, _serializerOptions)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, input.GetType(), _serializerOptions);
        stream.Position = 0;
        return stream;
    }
}
