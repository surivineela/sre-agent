// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Models;

namespace Agent.Framework;

public interface IAgentDescriptor
{
    public string Name { get; set; }
    public string Instructions { get; set; }
    public string? HandoffDescription { get; set; }
    public List<string> Handoffs { get; set; }
    public List<string> Tools { get; set; }
    public List<string> McpTools { get; set; }
    public bool AllowParallelToolCalls { get; set; }
    public List<AgentsAsTools> AgentsAsTools { get; set; }
    public int MaxReflectionCount { get; set; }
    public string CustomReflectionNote { get; set; }
    public string CriticPromptPath { get; set; }
    public bool CriticOnHandOff { get; set; }
    public List<string> CommonPrompts { get; set; }
    public List<string> CommonTools { get; set; }
    public float? Temperature { get; set; }
    public string? LlmModelName { get; set; }
    public string? OutputType { get; set; }
    public string? UserPromptOverride { get; set; }
    public bool DisableDocumentRetrieval { get; set; }
    public bool EnableHandoffPromptOverride { get; set; }
    public bool DisableCommonPrompts { get; set; }
    public bool EnableVanillaMode { get; set; }
    public bool EnableSkills { get; set; }

    /// <summary>
    /// Indicates whether system skills should be added to the agent's skill set. Only applicable if EnableSkills is true.
    /// </summary>
    public bool AddSystemSkills { get; set; }

    // === Workflow Agent Support ===
    public AgentType AgentType { get; set; }
    public string? ParameterExtractionAgent { get; set; }
    public List<string> OrchestrationStartAgents { get; set; }
    public string? ResultSummarizationPrompt { get; set; }
    public List<NextAgentMapping> NextAgentMappings { get; set; }
}
