// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using Agent.Framework;

namespace Agent.Data.DataModels.Legacy;

public record KustoConnectorDocumentModelLegacy : ConnectorDocumentModelLegacy, ILegacyModelConverter<KustoConnectorDocumentModel>
{
    public string ClusterUrl { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string? ClusterHint { get; set; }

    public KustoConnectorDocumentModelLegacy(
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

    public new KustoConnectorSpec ToResourceSpec()
    {
        return new KustoConnectorSpec
        {
            Name = Name,
            Type = Type,
            Description = Description,
            Auth = Auth,
            Enabled = Enabled,
            ClusterUrl = ClusterUrl,
            Database = Database,
            ClusterHint = ClusterHint,
            RegionalClusterGroups = RegionalClusterGroups
        };
    }

    public override KustoConnectorDocumentModel ToNewModel()
    {
        var metadata = ToResourceMetadata();
        var spec = ToResourceSpec();

        // explicitly set type because the properties is discarded during polymorphic deserialization
        spec.Type = ConnectorDocumentModel.KustoConnectorType;

        return new KustoConnectorDocumentModel(metadata, spec);
    }
}
