// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Agent.Data.Tools;
using Agent.Framework;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent Tool storage
/// </summary>
public record LinkToolDocumentModel : ToolDocumentModel
{
    public LinkToolDocumentModel(ResourceMetadata metadata, LinkToolSpec spec)
        : base(metadata, spec)
    {
    }

    public new LinkToolSpec Spec => (LinkToolSpec)base.Spec;

    public override YamlToolDefinitionBase ToYamlToolDefinition()
    {
        return new LinkToolDefinition
        {
            Name = Name,
            Type = Type,
            Connector = Spec.Connector ?? string.Empty,
            Description = Spec.Description ?? string.Empty,
            Parameters = Spec.Parameters ?? new List<YamlParameter>(),
            Attributes = Spec.Attributes ?? new List<string>(),
            Metadata = Metadata.ToYamlMetadata(),
            Template = Spec.Template,
            ToolMode = Spec.ToolMode
        };
    }
}

/// <summary>
/// Link-specific tool spec
/// </summary>
public class LinkToolSpec : ToolSpec
{
    public string Template { get; set; } = string.Empty;
}
