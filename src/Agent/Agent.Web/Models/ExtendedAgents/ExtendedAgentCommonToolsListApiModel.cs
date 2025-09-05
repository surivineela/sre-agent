// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;

namespace Agent.Web.Models.ExtendedAgents;

public  class ExtendedAgentCommonToolsListApiModel
{
    public string Name { get; set; } = string.Empty;
    public List<string> Tools { get; set; } = new();
    public YamlMetadata Metadata { get; set; } = new();
}
