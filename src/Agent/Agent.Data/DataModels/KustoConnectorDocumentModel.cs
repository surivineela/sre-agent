// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using Agent.Framework;

namespace Agent.Data.DataModels;

public record KustoConnectorDocumentModel : ConnectorDocumentModel
{
    public string ClusterUrl { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string? ClusterHint { get; set; }

    public KustoConnectorDocumentModel(
        string id,
        string name,
        string type,
        YamlMetadata metadata,
        string description,
        ConnectorAuthSettings auth,
        bool enabled,

        string operationId
    ) : base(id,
     name,
     type,
     description,
     metadata,
     auth,
     enabled,
     operationId)
    { }


    public List<KustoRegionalGroupSettings> RegionalClusterGroups { get; set; } = new();
}
