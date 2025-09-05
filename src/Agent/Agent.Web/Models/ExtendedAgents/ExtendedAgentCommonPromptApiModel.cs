// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;

namespace Agent.Web.Models.ExtendedAgents;

public class ExtendedAgentCommonPromptApiModel
{
    public string Name { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public YamlMetadata Metadata { get; set; } = new();
}
