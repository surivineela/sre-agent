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

    public Settable<string> OutputType { get; set; }


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
        agentView.OutputType = agent.OutputType;

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
            properties.OutputType.ApplyTo(value => result.Spec.OutputType = value);
        });

        return result;
    }
}

public class AgentsAsToolsView
{
    public Settable<string> AgentName { get; set; }

    public Settable<string> ToolName { get; set; }

    public Settable<string> ToolDescription { get; set; }

    public Settable<string> InputDescription { get; set; }
}
