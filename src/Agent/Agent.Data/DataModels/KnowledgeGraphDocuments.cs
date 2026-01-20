// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document representing an entity in the knowledge graph
/// </summary>
public class KnowledgeGraphEntityDocument : ICosmosDocument
{
    public static string ContainerName => AgentDataConfiguration.KnowledgeGraphContainerName;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "entity";

    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = "entity";

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("entityType")]
    public string EntityType { get; set; } = string.Empty;

    [JsonPropertyName("vector")]
    public float[] Vector { get; set; } = [];
}

/// <summary>
/// Cosmos DB document representing a relation in the knowledge graph
/// </summary>
public class KnowledgeGraphRelationDocument : ICosmosDocument
{
    public static string ContainerName => AgentDataConfiguration.KnowledgeGraphContainerName;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "relation";

    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = "relation";

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("relationType")]
    public string RelationType { get; set; } = string.Empty;
}

/// <summary>
/// Cosmos DB document representing an observation in the knowledge graph
/// </summary>
public class KnowledgeGraphObservationDocument : ICosmosDocument
{
    public static string ContainerName => AgentDataConfiguration.KnowledgeGraphContainerName;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "observation";

    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = "observation";

    [JsonPropertyName("entityId")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("vector")]
    public float[] Vector { get; set; } = [];
}
