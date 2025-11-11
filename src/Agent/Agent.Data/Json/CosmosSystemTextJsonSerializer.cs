// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Agent.Data.DataModels;
using Agent.Data.DataModels.Legacy;
using Microsoft.Azure.Cosmos;

namespace Agent.Data.Json;

// CosmosDB SDK v3 does not recognize System.Text.Json as it was released before System.Text.Json was GA.
// Thus to avoid using Newtonsoft.Json, we need to define our own JSON serializer to by pass the problem.
// 
// Issue Reference: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/2533
// Solution Reference: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos.Samples/Usage/SystemTextJson/CosmosSystemTextJsonSerializer.cs
public class CosmosSystemTextJsonSerializer : CosmosLinqSerializer
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;


    public CosmosSystemTextJsonSerializer()
    {
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            Converters =
                {
                    new LegacyDocumentModelConverter<AgentDocumentModel, AgentDocumentModelLegacy>(),
                    new LegacyDocumentModelConverter<ToolDocumentModel, ToolDocumentModelLegacy>(),
                    new LegacyDocumentModelConverter<ConnectorDocumentModel, ConnectorDocumentModelLegacy>(),
                    new LegacyDocumentModelConverter<PlugInConfigDocumentModel, PlugInConfigDocumentModelLegacy>(),
                    new LegacyDocumentModelConverter<CommonPromptDocumentModel, CommonPromptDocumentModelLegacy>(),
                    new LegacyDocumentModelConverter<CommonToolsListDocumentModel, CommonToolsListDocumentModelLegacy>(),
                    new AgentTaskPropertiesConverter(),
                    new AgentTaskInputDataConverter(),
                },
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
            AllowOutOfOrderMetadataProperties = true
        };
    }

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream.CanSeek && stream.Length == 0)
            {
                return default!;
            }

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            // Use JsonSerializer.Deserialize which handles JsonPolymorphic properly
            return JsonSerializer.Deserialize<T>(stream, _jsonSerializerOptions)!;
        }
    }
    public override Stream ToStream<T>(T input)
    {
        MemoryStream streamPayload = new();
        // Use JsonSerializer.Deserialize which handles JsonPolymorphic properly
        JsonSerializer.Serialize(streamPayload, input, _jsonSerializerOptions);
        streamPayload.Position = 0;
        return streamPayload;
    }

    public override string SerializeMemberName(MemberInfo memberInfo)
    {
        var jsonPropertyNameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>(true);

        return !string.IsNullOrEmpty(jsonPropertyNameAttribute?.Name)
            ? jsonPropertyNameAttribute.Name
            // Apply the same naming policy used in serialization (camelCase)
            : _jsonSerializerOptions.PropertyNamingPolicy?.ConvertName(memberInfo.Name) ?? memberInfo.Name;
    }
}