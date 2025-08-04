// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Azure.Core.Serialization;

namespace Agent.Data.AgentMemory;

public static class Constants
{
    public const string VectorSearchHnswProfile = "hnswProfile";
    public const string VectorSearchExhaustiveKnnProfile = "exhaustiveKnnProfile";
    public const string VectorSearchHnswConfig = "hnswConfig";
    public const string VectorSearchExhaustiveKnnConfig = "exhaustiveKnn";
    public const int VectorDimension = 1536;
    public const string SemanticSearchConfig = "semanticConfig";

    public static readonly JsonObjectSerializer JsonSerializer = new(
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        }
    );

    public enum AgentMemoryType
    {
        Trajectory,
        UserMemory,
        Document
    }

    public static string ToLowerString(this AgentMemoryType memoryType)
    {
        return memoryType switch
        {
            AgentMemoryType.Trajectory => "trajectory",
            AgentMemoryType.UserMemory => "usermemory",
            AgentMemoryType.Document => "document",
            _ => throw new ArgumentOutOfRangeException(nameof(memoryType))
        };
    }
}
