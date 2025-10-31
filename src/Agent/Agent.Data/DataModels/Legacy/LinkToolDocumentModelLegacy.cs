// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using Agent.Framework;

namespace Agent.Data.DataModels.Legacy;

/// <summary>
/// Cosmos DB document for Extended Agent Tool storage (Legacy)
/// </summary>
/// <summary>
/// A factory for creating generic CosmosDocument wrappers from specific domain models.
/// </summary>


public record LinkToolDocumentModelLegacy : ToolDocumentModelLegacy, ILegacyModelConverter<LinkToolDocumentModel>
{
    public LinkToolDocumentModelLegacy(
        string id,
        string name,
        string type,
        string connector,
        string description,
        List<YamlParameter> parameters,
        List<string> attributes,
    YamlMetadata metadata,
        string operationId
    ) : base(id, name, type, connector, description, parameters, attributes, metadata, operationId)
    {

    }


    public string Template { get; set; } = string.Empty;

    public new LinkToolSpec ToResourceSpec()
    {
        return new LinkToolSpec
        {
            Name = Name,
            Type = Type,
            Connector = Connector,
            Description = Description,
            Parameters = Parameters,
            Attributes = Attributes,
            Template = Template
        };
    }

    public override LinkToolDocumentModel ToNewModel()
    {
        var metadata = ToResourceMetadata();
        var spec = ToResourceSpec();

        // explicitly set type because the property is discarded during polymorphic deserialization
        spec.Type = ToolDocumentModel.LinkToolType;

        return new LinkToolDocumentModel(metadata, spec);
    }
}
