// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Data.DataModels;
using Agent.Plugins.Tools;
using Agent.Web.Models.ExtendedAgents;
using Google.Protobuf.WellKnownTypes;

namespace Agent.Web.Services;



public class AgentYamlTranslatorV1 : IAgentYamlTranslator
{

    public AgentDocumentModel Translate(ExtendedAgentApiModel model)
    {
        var operationId = Guid.NewGuid().ToString();

        var spec = new AgentSpec
        {
            Name = model.Name,
            Instructions = model.Instructions,
            HandoffDescription = model.HandoffDescription,
            Handoffs = model.Handoffs,
            Tools = model.Tools,
            McpTools = model.McpTools,
            Connectors = model.Connectors,
            AllowParallelToolCalls = model.AllowParallelToolCalls,
            AgentsAsTools = model.AgentsAsTools,
            MaxReflectionCount = model.MaxReflectionCount,
            CriticPromptPath = model.CriticPromptPath,
            CriticOnHandOff = model.CriticOnHandOff,
            CustomReflectionNote = model.CustomReflectionNote,
            CommonPrompts = model.CommonPrompts,
            DisableDocumentRetrieval = model.DisableDocumentRetrieval,
            EnableHandoffPromptOverride = model.EnableHandoffPromptOverride,
            UserPromptOverride = model.UserPromptOverride,
            HandoffPromptOverride = model.HandoffPromptOverride,
            InstructionsOverride = model.InstructionsOverride,
            CommonTools = model.CommonTools,
            Temperature = model.Temperature,
            // Workflow agent properties
            AgentType = model.AgentType,
            ParameterExtractionAgent = model.ParameterExtractionAgent,
            OrchestrationStartAgents = model.OrchestrationStartAgents,
            ResultSummarizationPrompt = model.ResultSummarizationPrompt,
            NextAgentMappings = model.NextAgentMappings,
            OutputType = model.OutputType
        };

        var metadata = ResourceMetadata.FromYamlMetadata(model.Metadata, model.Name, operationId);

        return new AgentDocumentModel(
            Metadata: metadata,
            Spec: spec
        );
    }

    public AgentConfigurationDocumentModel Translate(AgentDeploymentModel model)
    {
        var operationId = Guid.NewGuid().ToString();

        var spec = new AgentSpec
        {
            Name = model.Spec.Name,
            Instructions = model.Spec.Instructions,
            HandoffDescription = model.Spec.HandoffDescription,
            Handoffs = model.Spec.Handoffs,
            Tools = model.Spec.Tools,
            McpTools = model.Spec.McpTools,
            Connectors = model.Spec.Connectors,
            AllowParallelToolCalls = model.Spec.AllowParallelToolCalls,
            AgentsAsTools = model.Spec.AgentsAsTools,
            MaxReflectionCount = model.Spec.MaxReflectionCount,
            CriticPromptPath = model.Spec.CriticPromptPath,
            CriticOnHandOff = model.Spec.CriticOnHandOff,
            CustomReflectionNote = model.Spec.CustomReflectionNote,
            CommonPrompts = model.Spec.CommonPrompts,
            DisableDocumentRetrieval = model.Spec.DisableDocumentRetrieval,
            EnableHandoffPromptOverride = model.Spec.EnableHandoffPromptOverride,
            UserPromptOverride = model.Spec.UserPromptOverride,
            HandoffPromptOverride = model.Spec.HandoffPromptOverride,
            InstructionsOverride = model.Spec.InstructionsOverride,
            CommonTools = model.Spec.CommonTools,
            Temperature = model.Spec.Temperature,
            // Workflow agent properties
            AgentType = model.Spec.AgentType,
            ParameterExtractionAgent = model.Spec.ParameterExtractionAgent,
            OrchestrationStartAgents = model.Spec.OrchestrationStartAgents,
            ResultSummarizationPrompt = model.Spec.ResultSummarizationPrompt,
            NextAgentMappings = model.Spec.NextAgentMappings,
            OutputType = model.Spec.OutputType
        };

        var metadata = ResourceMetadata.FromYamlMetadata(model.Metadata, model.Spec.Name, operationId);

        return new AgentConfigurationDocumentModel
        {
            Id = model.Spec.Name,
            ApiVersion = model.ApiVersion,
            Agent = new AgentDocumentModel(
                Metadata: metadata,
                Spec: spec
            ),
            // Tools and Connectors in the spec are just names (string references), not full definitions
            // So we can't create document models from them. The agent document already has the references.
            Tools = new List<ToolDocumentModel>(),
            Connectors = new List<ConnectorDocumentModel>()
        };
    }


}

