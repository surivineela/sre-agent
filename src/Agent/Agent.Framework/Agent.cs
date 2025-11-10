// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using Agent.Framework.Models;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class Agent<TContext>(string name) where TContext : class
{
    public string Name { get; } = name;

    public PromptText? HandoffDescription { get; set; }

    public PromptText Instructions { get; set; } = "";

    public Type? OutputType { get; set; }

    [MemberNotNullWhen(true, nameof(OutputType))]
    public bool HasStructuredOutput => OutputType is not null && OutputType != typeof(string);

    // Tools that are retrieved from the tool factory
    public List<string> FactoryTools { get; set; } = [];

    // Tools preconfigured in the agent
    public List<AIFunction> Tools { get; set; } = [];

    public List<string> StandardToolNames => Tools.Select(t => t.Name).ToList();

    public List<AIFunction> CustomTools { get; set; } = [];

    public List<string> CustomToolNames => CustomTools.Select(t => t.Name).ToList();

    public List<Handoff<TContext>> Handoffs { get; set; } = [];

    public List<AgentAsTool<TContext>> AgentsAsTools { get; set; } = [];

    public List<string> HandoffNames => Handoffs.Select(h => h.Name).ToList();

    public IAgentHooks? Hooks { get; set; }

    public int MaxReflectionCount { get; set; } = 0;

    public string CustomReflectionNote { get; set; } = string.Empty;

    //todo: map to thinking effort
    public string CriticPromptPath { get; set; } = string.Empty;

    public bool CriticOnHandOff { get; set; } = false;

    public bool AllowParallelToolCalls { get; set; } = true;

    public string? UserPromptOverride { get; set; } = null;

    public bool DisableDocumentRetrieval { get; set; } = false;

    public bool EnableHandoffPromptOverride { get; set; } = false;

    /// <summary>
    /// When true, disables DI-injected common prompts and uses only the original instructions.
    /// </summary>
    public bool DisableCommonPrompts { get; set; } = false;

    /// <summary>
    /// Indicates whether this agent is an extended (custom) agent loaded from the extensibility system.
    /// </summary>
    public bool IsExtended { get; set; } = false;

    /// <summary>
    /// When true, enables the skills system for this agent and automatically adds the read_skill_file tool.
    /// </summary>
    public bool EnableSkills { get; set; } = false;

    /// <summary>
    /// Only applies when EnableSkills is true. When true, system skills are automatically added to the agent.
    /// </summary>
    public bool AddSystemSkills { get; set; } = true;

    public virtual ChatToolMode ChatToolMode { get; set; } = ChatToolMode.Auto;

    public virtual float Temperature { get; set; } = 0.3f;

    public string ReasoningEffortLevel { get; set; } = "low";

    public bool EnableVanillaMode { get; private set; } = false;

    // === Workflow Agent Properties ===

    /// <summary>
    /// Specifies the execution type of this agent.
    /// </summary>
    public AgentType AgentType { get; set; } = AgentType.Autonomous;

    /// <summary>
    /// Name of the agent responsible for extracting parameters from conversation history,
    /// incident data, and function metadata for RCA execution.
    /// </summary>
    public string? ParameterExtractionAgent { get; set; }

    /// <summary>
    /// List of agent names to start orchestration with using extracted parameters.
    /// These agents execute without conversation history.
    /// </summary>
    public List<string> OrchestrationStartAgents { get; set; } = [];

    /// <summary>
    /// Prompt template used for summarizing results from orchestrated agents.
    /// The results are stored in-memory and summarized using LLM with this prompt.
    /// </summary>
    public string? ResultSummarizationPrompt { get; set; }

    /// <summary>
    /// Mappings that define which agents to execute next based on execution results.
    /// Used by Activity agents to dynamically select subsequent agents in the workflow.
    /// </summary>
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];

    public IChatClient? ChatClient { get; set; } = default;

    public virtual IChatClient GetChatClient(RunConfig config)
    {
        return ChatClient ?? config.ChatClient;
    }

    public void ApplyVanillaMode(bool vanillaMode)
    {
        EnableVanillaMode = vanillaMode;
        if (EnableVanillaMode)
        {
            OutputType = typeof(string);
            CriticOnHandOff = false;
            MaxReflectionCount = 0;
        }
    }

    public int GetAllToolsCount()
    {
        return FactoryTools.Count + Tools.Count + CustomTools.Count + Handoffs.Count; // agents as tools are included in 'Tools'
    }

    /// <summary>
    /// Creates a shallow clone of this agent. Collections are copied but not deeply cloned.
    /// Handoffs are initialized as empty and should be populated by the caller (e.g., AgentGraphCloner).
    /// </summary>
    public Agent<TContext> ShallowClone()
    {
        return new Agent<TContext>(Name)
        {
            Instructions = Instructions,
            HandoffDescription = HandoffDescription,
            MaxReflectionCount = MaxReflectionCount,
            CustomReflectionNote = CustomReflectionNote,
            CriticPromptPath = CriticPromptPath,
            CriticOnHandOff = CriticOnHandOff,
            AllowParallelToolCalls = AllowParallelToolCalls,
            UserPromptOverride = UserPromptOverride,
            DisableDocumentRetrieval = DisableDocumentRetrieval,
            EnableHandoffPromptOverride = EnableHandoffPromptOverride,
            DisableCommonPrompts = DisableCommonPrompts,
            IsExtended = IsExtended,
            ChatToolMode = ChatToolMode,
            Temperature = Temperature,
            ReasoningEffortLevel = ReasoningEffortLevel,

            // Clone collections
            FactoryTools = [.. FactoryTools],
            Tools = [.. Tools],
            CustomTools = [.. CustomTools],
            Handoffs = [], // Will be populated by AgentGraphCloner
            AgentsAsTools = [], // Will be populated by AgentGraphCloner

            // Workflow properties
            AgentType = AgentType,
            ParameterExtractionAgent = ParameterExtractionAgent,
            OrchestrationStartAgents = [.. OrchestrationStartAgents],
            ResultSummarizationPrompt = ResultSummarizationPrompt,
            NextAgentMappings = [.. NextAgentMappings],

            OutputType = OutputType,
            ChatClient = ChatClient,
            Hooks = Hooks, // Hooks can be shared across clones

            EnableSkills = EnableSkills,
            AddSystemSkills = AddSystemSkills,
        };
    }
}
