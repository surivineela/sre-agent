// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;

namespace Agent.Web.Models.ExtendedAgents;

public abstract class ExtendedAgentToolApiModel
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Connector { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<YamlParameter> Parameters { get; set; } = new();
    public List<string> Attributes { get; set; } = new();
    public YamlMetadata Metadata { get; set; } = new();
}
