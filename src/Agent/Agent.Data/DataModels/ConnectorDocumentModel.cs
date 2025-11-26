// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Framework;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent Tool storage
/// </summary>
[CustomizedJsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[CustomizedJsonDerivedType(typeof(KustoConnectorDocumentModel), KustoConnectorType)]
public record ConnectorDocumentModel(
    ResourceMetadata Metadata,
    ConnectorSpec Spec
) : ICosmosDocument
{
    public const string KustoConnectorType = "Kusto";
    public string Id => Metadata.Id ?? Spec.Name;
    public string DocumentType => "ExtendedAgentConnector";
    public string PartitionKey => Spec.Name;
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    [JsonIgnore]
    public string Name => Spec.Name;

    // [JsonIgnore] uncomment when we remove the CustomizedJsonPolymorphic attribute
    public string Type => Spec.Type;
}

/// <summary>
/// Spec fields for connector documents
/// </summary>
public class ConnectorSpec
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ConnectorAuthSettings? Auth { get; set; }

    public bool Enabled { get; set; } = true;
}
