// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Framework.Models;

/// <summary>
/// Configuration for a helper agent that can be used as a tool by another agent.
/// </summary>
public class AgentsAsTools
{
    /// <summary>
    /// The name of the helper agent.
    /// </summary>
    [YamlMember(Alias = "agent_name")]
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the tool.
    /// </summary>
    [YamlMember(Alias = "tool_name")]
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// The description of the tool.
    /// </summary>
    [YamlMember(Alias = "tool_description")]
    public string ToolDescription { get; set; } = string.Empty;

    /// <summary>
    /// The description for the input to be passed to this tool.
    /// </summary>
    [YamlMember(Alias = "input_description")]
    public string InputDescription { get; set; } = string.Empty;
}
