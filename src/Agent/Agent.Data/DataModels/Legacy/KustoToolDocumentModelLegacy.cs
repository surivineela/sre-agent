// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using Agent.Framework;

namespace Agent.Data.DataModels.Legacy;

public enum KustoExecutionMode
{
    Function,
    Query,
    Script
}
/// <summary>
/// Cosmos DB document for Extended Agent Tool storage (Legacy)
/// </summary>
/// <summary>
/// A factory for creating generic CosmosDocument wrappers from specific domain models.
/// </summary>


public record KustoToolDocumentModelLegacy : ToolDocumentModelLegacy, ILegacyModelConverter<KustoToolDocumentModel>
{
    public KustoToolDocumentModelLegacy(
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

    public new KustoToolSpec ToResourceSpec()
    {
        return new KustoToolSpec
        {
            Name = Name,
            Type = Type,
            Connector = Connector,
            Description = Description,
            Parameters = Parameters,
            Attributes = Attributes,
            Mode = (DataModels.KustoExecutionMode)Mode,
            Function = Function,
            Query = Query,
            File = File,
            Database = Database,
            ClusterHint = ClusterHint,
            RegionalClusterGroups = RegionalClusterGroups,
            ClusterUri = ClusterUri
        };
    }

    public override KustoToolDocumentModel ToNewModel()
    {
        var metadata = ToResourceMetadata();
        var spec = ToResourceSpec();

        // explicitly set type because the property is discarded during polymorphic deserialization
        spec.Type = ToolDocumentModel.KustoToolType;

        return new KustoToolDocumentModel(metadata, spec);
    }
}
