// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using Agent.Framework.Reasoning.Models;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent Tool storage
/// </summary>
/// <summary>
/// A factory for creating generic CosmosDocument wrappers from specific domain models.
/// </summary>


public record LinkToolDocumentModel : ToolDocumentModel
{
    public LinkToolDocumentModel(
        string id,
        string name,
        string type,
        string connector,
        string description,
        List<YamlParameter> parameters,
        List<string> attributes,
    YamlMetadata metadata ,
        string operationId
    ) : base(id, name, type, connector, description, parameters, attributes, metadata, operationId) {

    }


    public string Template { get; set; } = string.Empty;

}
