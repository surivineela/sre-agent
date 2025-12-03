// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Agent.Data.Tools;
using Agent.Framework;
using Azure.ResourceManager.AppService.Models;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent Tool storage
/// </summary>
[CustomizedJsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[CustomizedJsonDerivedType(typeof(KustoToolDocumentModel), KustoToolType)]
[CustomizedJsonDerivedType(typeof(LinkToolDocumentModel), LinkToolType)]
[CustomizedJsonDerivedType(typeof(PythonToolDocumentModel), PythonToolType)]
public record ToolDocumentModel(
    ResourceMetadata Metadata,
    ToolSpec Spec
) : ICosmosDocument
{
    public const string KustoToolType = "KustoTool";
    public const string LinkToolType = "LinkTool";
    public const string PythonToolType = "PythonFunctionTool";
    public string Id => Metadata.Id ?? Spec.Name;
    public string DocumentType => "ExtendedAgentTool";
    public string PartitionKey => Spec.Name;
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    [JsonIgnore]
    public string Name => Spec.Name;

    // [JsonIgnore] uncomment when we remove the CustomizedJsonPolymorphic attribute
    public string Type => Spec.Type;

    #region Conversion between runtime and data model

    public virtual YamlToolDefinitionBase ToYamlToolDefinition() => this switch
    {
        KustoToolDocumentModel k => k.ToYamlToolDefinition(),
        LinkToolDocumentModel l => l.ToYamlToolDefinition(),
        PythonToolDocumentModel p => p.ToYamlToolDefinition(),
        _ => throw new NotSupportedException($"Unknown tool document type: {Type}")
    };
    #endregion
}

/// <summary>
/// Spec fields for tool documents
/// </summary>
public class ToolSpec
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? Connector { get; set; }
    public string Description { get; set; } = string.Empty;

    public List<YamlParameter>? Parameters { get; set; }

    public List<string>? Attributes { get; set; }

    public ToolMode ToolMode { get; set; } = ToolMode.Auto;
}
