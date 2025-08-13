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
            Id = model.Spec.Agent.Name,
            ApiVersion = model.ApiVersion,
            Agent = new AgentDocumentModel(
                model.Spec.Agent.Name,
                model.Spec.Agent.Name,
                model.Spec.Agent.Instructions,
                model.Spec.Agent.HandoffDescription,
                model.Spec.Agent.Handoffs,
                model.Spec.Agent.Tools,
                model.Spec.Agent.Connectors,
                model.Spec.Agent.AllowParallelToolCalls,
                model.Spec.Agent.AgentsAsTools,
                model.Spec.Agent.MaxReflectionCount,
                model.Spec.Agent.CriticPromptPath,
                model.Spec.Agent.CriticOnHandOff,
                model.Spec.Agent.CustomReflectionNote,
                model.Spec.Agent.CommonPrompts,
              
                model.Spec.Agent.DisableDocumentRetrieval,
                model.Spec.Agent.EnableHandoffPromptOverride,
                model.Spec.Agent.UserPromptOverride,
                model.Spec.Agent.HandoffPromptOverride,
                model.Spec.Agent.InstructionsOverride,
                  model.Spec.Agent.CommonTools,
                model.Spec.Agent.Temperature,
                // Workflow agent properties
                model.Spec.Agent.AgentType,
                model.Spec.Agent.ParameterExtractionAgent,
                model.Spec.Agent.OrchestrationStartAgents,
                model.Spec.Agent.ResultSummarizationPrompt,
                model.Spec.Agent.NextAgentMappings,
                model.Spec.Agent.OutputType,
                model.Metadata,
                operationId
            ),
            Tools = model.Spec.Tools.Select(t =>
                ApiToRuntimeMapper.ToDocumentTool(t, operationId)
            ).ToList(),

            Connectors = model.Spec.Connectors.Select(c =>
                ApiToRuntimeMapper.ToDocumentConnector(c, operationId)
            ).ToList()
        };
    }


}

