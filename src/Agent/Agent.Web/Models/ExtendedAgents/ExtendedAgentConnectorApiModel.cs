// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;

namespace Agent.Web.Models.ExtendedAgents;

public class ExtendedAgentConnectorApiModel
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ConnectorAuthSettings Auth { get; set; } = new();
    public YamlMetadata Metadata { get; set; } = new();

    public ExtendedAgentConnectorApiModel()
    { }
}
