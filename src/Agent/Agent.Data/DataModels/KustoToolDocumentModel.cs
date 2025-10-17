// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using Agent.Framework;

namespace Agent.Data.DataModels;

public enum KustoExecutionMode
{
    Function,
    Query,
    Script
}
/// <summary>
/// Cosmos DB document for Extended Agent Tool storage
/// </summary>
/// <summary>
/// A factory for creating generic CosmosDocument wrappers from specific domain models.
/// </summary>


public record KustoToolDocumentModel : ToolDocumentModel
{
    public KustoToolDocumentModel(
        string id,
        string name,
        string type,
        string connector,
        string description,
        List<YamlParameter> parameters,
        List<string> attributes,
    YamlMetadata metadata,
        string operationId
    ) : base(id, name, type, connector, description, parameters, attributes, metadata, operationId) { }

    public KustoExecutionMode Mode { get; set; }
    public string? Function { get; set; }
    public string? Query { get; set; }
    public string? File { get; set; }
    public string Database { get; set; } = string.Empty;
    public string? ClusterHint { get; set; }
    public List<KustoRegionalGroupSettings> RegionalClusterGroups { get; set; } = new List<KustoRegionalGroupSettings>();
    public string? ClusterUri { get; set; }
}
