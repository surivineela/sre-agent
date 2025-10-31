// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Data.Tools;
using Agent.Framework;
using Microsoft.OData.ModelBuilder;

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
public record KustoToolDocumentModel : ToolDocumentModel
{
    public KustoToolDocumentModel(ResourceMetadata metadata, KustoToolSpec spec)
        : base(metadata, spec)
    {
    }

    public new KustoToolSpec Spec => (KustoToolSpec)base.Spec;

    public override YamlToolDefinitionBase ToYamlToolDefinition()
    {
        return new KustoToolDefinition
        {
            Name = Name,
            Type = Type,
            Connector = Spec.Connector ?? string.Empty,
            Description = Spec.Description,
            Parameters = Spec.Parameters ?? new List<YamlParameter>(),
            Attributes = Spec.Attributes ?? new List<string>(),
            Metadata = Metadata.ToYamlMetadata(),
            Mode = Spec.Mode,
            Function = Spec.Function,
            Query = Spec.Query,
            File = Spec.File,
            Database = Spec.Database,
            ClusterHint = Spec.ClusterHint,
        };
    }
}

/// <summary>
/// Kusto-specific tool spec
/// </summary>
public class KustoToolSpec : ToolSpec
{
    public KustoExecutionMode Mode { get; set; }
    public string? Function { get; set; }
    public string? Query { get; set; }

    public string? File { get; set; }
    public string Database { get; set; } = string.Empty;
    public string? ClusterHint { get; set; }
    public List<KustoRegionalGroupSettings>? RegionalClusterGroups { get; set; }
    public string? ClusterUri { get; set; }
}
