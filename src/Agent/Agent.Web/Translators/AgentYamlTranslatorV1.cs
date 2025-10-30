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
        return new AgentDocumentModel(
            model.Name,
            model.Name,
            model.Instructions,
            model.HandoffDescription,
            model.Handoffs,
            model.Tools,
            model.McpTools,
            model.Connectors,
            model.AllowParallelToolCalls,
            model.AgentsAsTools,
            model.MaxReflectionCount,
            model.CriticPromptPath,
            model.CriticOnHandOff,
            model.CustomReflectionNote,
            model.CommonPrompts,
            model.DisableDocumentRetrieval,
            model.EnableHandoffPromptOverride,
            model.UserPromptOverride,
            model.HandoffPromptOverride,
            model.InstructionsOverride,
            model.CommonTools,
            model.Temperature,
            // Workflow agent properties
            model.AgentType,
            model.ParameterExtractionAgent,
            model.OrchestrationStartAgents,
            model.ResultSummarizationPrompt,
            model.NextAgentMappings,
            model.OutputType,
            model.Metadata,
            operationId
        );
    }

    public AgentConfigurationDocumentModel Translate(AgentDeploymentModel model)
    {
        var operationId = Guid.NewGuid().ToString();

        return new AgentConfigurationDocumentModel
        {
            Id = model.Spec.Name,
            ApiVersion = model.ApiVersion,
            Agent = new AgentDocumentModel(
                model.Spec.Name,
                model.Spec.Name,
                model.Spec.Instructions,
                model.Spec.HandoffDescription,
                model.Spec.Handoffs,
                model.Spec.Tools,
                model.Spec.McpTools,
                model.Spec.Connectors,
                model.Spec.AllowParallelToolCalls,
                model.Spec.AgentsAsTools,
                model.Spec.MaxReflectionCount,
                model.Spec.CriticPromptPath,
                model.Spec.CriticOnHandOff,
                model.Spec.CustomReflectionNote,
                model.Spec.CommonPrompts,
              
                model.Spec.DisableDocumentRetrieval,
                model.Spec.EnableHandoffPromptOverride,
                model.Spec.UserPromptOverride,
                model.Spec.HandoffPromptOverride,
                model.Spec.InstructionsOverride,
                  model.Spec.CommonTools,
                model.Spec.Temperature,
                // Workflow agent properties
                model.Spec.AgentType,
                model.Spec.ParameterExtractionAgent,
                model.Spec.OrchestrationStartAgents,
                model.Spec.ResultSummarizationPrompt,
                model.Spec.NextAgentMappings,
                model.Spec.OutputType,
                model.Metadata,
                operationId
            ),
            // Tools and Connectors in the spec are just names (string references), not full definitions
            // So we can't create document models from them. The agent document already has the references.
            Tools = new List<ToolDocumentModel>(),
            Connectors = new List<ConnectorDocumentModel>()
        };
    }


}

