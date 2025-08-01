// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Framework.Models;

/// <summary>
/// Configuration for mapping execution results to next agents in a workflow.
/// </summary>
public class NextAgentMapping
{
    /// <summary>
    /// The condition key that determines which agents to execute next.
    /// </summary>
    [YamlMember(Alias = "condition")]
    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// List of agent names to execute when the condition is met.
    /// </summary>
    [YamlMember(Alias = "next_agents")]
    public List<string> NextAgents { get; set; } = [];
}
