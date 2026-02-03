// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework.Models;
using Agent.Web.ApiResources;
using Agent.Web.Json;

namespace Agent.Web.Views.v2;

public class ExtendedAgentView
{
    public Settable<string> Instructions { get; set; }

    public Settable<string> HandoffDescription { get; set; }

    public Settable<List<string>> Handoffs { get; set; }

    public Settable<List<string>> Tools { get; set; }

    public Settable<List<string>> Connectors { get; set; }

    public Settable<bool?> AllowParallelToolCalls { get; set; }

    public Settable<List<AgentsAsToolsView>> AgentsAsTools { get; set; }

    public Settable<int?> MaxReflectionCount { get; set; }

    public Settable<string> CriticPromptPath { get; set; }

    public Settable<bool?> CriticOnHandOff { get; set; }

    public Settable<string> CustomReflectionNote { get; set; }

    public Settable<List<string>> CommonPrompts { get; set; }

    public Settable<bool?> DisableDocumentRetrieval { get; set; }

    public Settable<bool?> EnableHandoffPromptOverride { get; set; }

    public Settable<string> UserPromptOverride { get; set; }

    public Settable<string> HandoffPromptOverride { get; set; }

    public Settable<string> InstructionsOverride { get; set; }

    public Settable<List<string>> CommonTools { get; set; }

    public Settable<float?> Temperature { get; set; }

    public Settable<string> LlmModelName { get; set; }

    public Settable<bool?> EnableVanillaMode { get; set; }

    public Settable<bool?> EnableSkills { get; set; }

    public Settable<bool?> AddSystemSkills { get; set; }

    public Settable<List<string>> AllowedSkills { get; set; }

    public Settable<string> OutputType { get; set; }

    public Settable<Dictionary<string, List<HookDefinitionView>>> Hooks { get; set; }


    public static ApiResponseEnvelope<ExtendedAgentView> CreateApiResponseEnvelope(AgentDocumentModel agentDoc)
    {
        var agent = agentDoc.Spec;
        var agentView = new ExtendedAgentView();

        agentView.Instructions = agent.Instructions;
        agentView.HandoffDescription = agentDoc.HandoffDescription;
        agentView.Handoffs = agent.Handoffs;
        agentView.Tools = agent.Tools;
        agentView.Connectors = agent.Connectors;
        agentView.AllowParallelToolCalls = agent.AllowParallelToolCalls;
        agentView.AgentsAsTools = agent.AgentsAsTools?.Select(
            a => new AgentsAsToolsView
            {
                AgentName = a.AgentName,
                ToolName = a.ToolName,
                ToolDescription = a.ToolDescription,
                InputDescription = a.InputDescription
            }
        ).ToList();
        agentView.MaxReflectionCount = agent.MaxReflectionCount;
        agentView.CriticPromptPath = agent.CriticPromptPath;
        agentView.CriticOnHandOff = agent.CriticOnHandOff;
        agentView.CustomReflectionNote = agent.CustomReflectionNote;
        agentView.CommonPrompts = agent.CommonPrompts;
        agentView.DisableDocumentRetrieval = agent.DisableDocumentRetrieval;
        agentView.EnableHandoffPromptOverride = agent.EnableHandoffPromptOverride;
        agentView.UserPromptOverride = agent.UserPromptOverride;
        agentView.HandoffPromptOverride = agent.HandoffPromptOverride;
        agentView.InstructionsOverride = agent.InstructionsOverride;
        agentView.CommonTools = agent.CommonTools;
        agentView.Temperature = agent.Temperature;
        agentView.LlmModelName = agent.LlmModelName;
        agentView.EnableVanillaMode = agent.EnableVanillaMode;
        agentView.EnableSkills = agent.EnableSkills;
        agentView.AddSystemSkills = agent.AddSystemSkills;
        agentView.AllowedSkills = agent.AllowedSkills;
        agentView.OutputType = agent.OutputType;
        agentView.Hooks = ConvertHooksToView(agent.Hooks);

        ApiResponseEnvelope<ExtendedAgentView> apiResponse = new()
        {
            Name = agentDoc.Name,
            Type = agentDoc.DocumentType,
            Tags = agentDoc.Metadata.Tags,
            Properties = agentView,
        };

        return apiResponse;
    }

    public static AgentDocumentModel CreateModel(
        ApiRequestEnvelope<ExtendedAgentView> envelope,
        ResourceMetadata? metadata = null,
        AgentDocumentModel? baseModel = null)
    {
        var result = baseModel ?? new AgentDocumentModel(
            new ResourceMetadata
            {
                CreatedAt = DateTime.UtcNow,
            },
            new AgentSpec()
        );

        if (metadata != null)
        {
            result = result with
            {
                Metadata = metadata,
            };
        }

        result.Metadata.UpdatedAt = DateTime.UtcNow;

        envelope.Name.ApplyTo(name => result.Metadata.Name = name!);
        envelope.Tags.ApplyTo(tag => result.Metadata.Tags = tag);
        envelope.Properties.ApplyTo(properties =>
        {
            if (properties == null)
            {
                return;
            }

            properties.Instructions.ApplyTo(value => result.Spec.Instructions = value);
            properties.HandoffDescription.ApplyTo(value => result.Spec.HandoffDescription = value);
            properties.Handoffs.ApplyTo(value => result.Spec.Handoffs = value);
            properties.Tools.ApplyTo(value => result.Spec.Tools = value);
            properties.Connectors.ApplyTo(value => result.Spec.Connectors = value);
            properties.AllowParallelToolCalls.ApplyTo(value => result.Spec.AllowParallelToolCalls = value);
            properties.AgentsAsTools.ApplyTo(value =>
            {
                if (value == null)
                {
                    result.Spec.AgentsAsTools = null;
                    return;
                }

                result.Spec.AgentsAsTools = value.Select(a => new AgentsAsTools
                {
                    AgentName = a.AgentName!,
                    ToolName = a.ToolName!,
                    ToolDescription = a.ToolDescription!,
                    InputDescription = a.InputDescription!,
                }).ToList();
            });
            properties.MaxReflectionCount.ApplyTo(value => result.Spec.MaxReflectionCount = value);
            properties.CriticPromptPath.ApplyTo(value => result.Spec.CriticPromptPath = value);
            properties.CriticOnHandOff.ApplyTo(value => result.Spec.CriticOnHandOff = value);
            properties.CustomReflectionNote.ApplyTo(value => result.Spec.CustomReflectionNote = value);
            properties.CommonPrompts.ApplyTo(value => result.Spec.CommonPrompts = value);
            properties.DisableDocumentRetrieval.ApplyTo(value => result.Spec.DisableDocumentRetrieval = value);
            properties.EnableHandoffPromptOverride.ApplyTo(value => result.Spec.EnableHandoffPromptOverride = value);
            properties.UserPromptOverride.ApplyTo(value => result.Spec.UserPromptOverride = value);
            properties.HandoffPromptOverride.ApplyTo(value => result.Spec.HandoffPromptOverride = value);
            properties.InstructionsOverride.ApplyTo(value => result.Spec.InstructionsOverride = value);
            properties.CommonTools.ApplyTo(value => result.Spec.CommonTools = value);
            properties.Temperature.ApplyTo(value => result.Spec.Temperature = value);
            properties.LlmModelName.ApplyTo(value => result.Spec.LlmModelName = value);
            properties.EnableVanillaMode.ApplyTo(value => result.Spec.EnableVanillaMode = value ?? false);
            properties.EnableSkills.ApplyTo(value => result.Spec.EnableSkills = value);
            properties.AddSystemSkills.ApplyTo(value => result.Spec.AddSystemSkills = value);
            properties.AllowedSkills.ApplyTo(value => result.Spec.AllowedSkills = value);
            properties.OutputType.ApplyTo(value => result.Spec.OutputType = value);
            properties.Hooks.ApplyTo(value => result.Spec.Hooks = ConvertHooksToDto(value));
        });

        return result;
    }

    private static Dictionary<string, List<HookDefinitionView>>? ConvertHooksToView(
        Dictionary<string, List<HookDefinitionDto>>? hooks)
    {
        if (hooks == null || hooks.Count == 0)
        {
            return null;
        }

        return hooks.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(dto => new HookDefinitionView
            {
                Type = dto.Type,
                Prompt = dto.Prompt,
                Command = dto.Command,
                Script = dto.Script,
                Matcher = dto.Matcher,
                Timeout = dto.Timeout,
                Model = dto.Model,
                FailMode = dto.FailMode
            }).ToList()
        );
    }

    private static Dictionary<string, List<HookDefinitionDto>>? ConvertHooksToDto(
        Dictionary<string, List<HookDefinitionView>>? hooks)
    {
        if (hooks == null || hooks.Count == 0)
        {
            return null;
        }

        return hooks.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(view => new HookDefinitionDto
            {
                Type = view.Type ?? string.Empty,
                Prompt = view.Prompt,
                Command = view.Command,
                Script = view.Script,
                Matcher = view.Matcher,
                Timeout = view.Timeout ?? 30,
                Model = view.Model,
                FailMode = view.FailMode
            }).ToList()
        );
    }
}

public class AgentsAsToolsView
{
    public Settable<string> AgentName { get; set; }

    public Settable<string> ToolName { get; set; }

    public Settable<string> ToolDescription { get; set; }

    public Settable<string> InputDescription { get; set; }
}

/// <summary>
/// API view for hook definitions.
/// </summary>
public class HookDefinitionView
{
    /// <summary>
    /// The type of hook execution (e.g., "prompt", "command").
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// For prompt hooks: the prompt text to send to the LLM.
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// For command hooks: the shell command to execute.
    /// Mutually exclusive with Script.
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// For command hooks: a multi-line bash script to execute.
    /// Mutually exclusive with Command.
    /// </summary>
    public string? Script { get; set; }

    /// <summary>
    /// Pattern to match tool names for PostToolUse hooks.
    /// </summary>
    public string? Matcher { get; set; }

    /// <summary>
    /// Timeout in seconds for hook execution.
    /// </summary>
    public int? Timeout { get; set; }

    /// <summary>
    /// Optional model name/identifier for prompt hooks.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// How to handle command hook execution errors.
    /// </summary>
    public string? FailMode { get; set; }
}
