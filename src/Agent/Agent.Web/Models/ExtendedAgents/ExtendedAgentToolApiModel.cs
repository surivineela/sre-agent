// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Framework;

namespace Agent.Web.Models.ExtendedAgents;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KustoToolApiModel), "KustoTool")]
[JsonDerivedType(typeof(LinkToolApiModel), "LinkTool")]
public abstract class ExtendedAgentToolApiModel
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Connector { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<YamlParameter> Parameters { get; set; } = new();
    public List<string> Attributes { get; set; } = new();
    public YamlMetadata Metadata { get; set; } = new();
    public ToolMode ToolMode { get; set; } = ToolMode.Auto;
}
