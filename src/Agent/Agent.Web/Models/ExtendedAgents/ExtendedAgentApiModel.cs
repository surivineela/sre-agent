// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;

namespace Agent.Web.Models.ExtendedAgents;

[AiExclude("ExtendedAgentApiModel")]
public class ExtendedAgentApiModel : YamlAgentDescriptor
{


    public static ExtendedAgentApiModel FromRuntime(YamlAgentDescriptor runtimeModel)
    {
        return new ExtendedAgentApiModel
        {
            Name = runtimeModel.Name,
            Instructions = runtimeModel.Instructions,
            HandoffDescription = runtimeModel.HandoffDescription,
            Handoffs = runtimeModel.Handoffs,
            Tools = runtimeModel.Tools,
            Connectors = runtimeModel.Connectors,
            AllowParallelToolCalls = runtimeModel.AllowParallelToolCalls,
            AgentsAsTools = runtimeModel.AgentsAsTools,
            MaxReflectionCount = runtimeModel.MaxReflectionCount,
            CriticPromptPath = runtimeModel.CriticPromptPath,
            CriticOnHandOff = runtimeModel.CriticOnHandOff,
            CustomReflectionNote = runtimeModel.CustomReflectionNote,
            CommonPrompts = runtimeModel.CommonPrompts,
            Temperature = runtimeModel.Temperature,
            OutputType = runtimeModel.OutputType,
            //CreatedAt = runtimeModel.CreatedAt,
            //UpdatedAt = runtimeModel.UpdatedAt
        };
    }

    public YamlAgentDescriptor ToRuntime()
    {
        return new YamlAgentDescriptor
        {
            Name = Name,
            Instructions = Instructions,
            HandoffDescription = HandoffDescription,
            Handoffs = Handoffs,
            Tools = Tools,
            Connectors = Connectors,    
            AllowParallelToolCalls = AllowParallelToolCalls,
            AgentsAsTools = AgentsAsTools,
            MaxReflectionCount = MaxReflectionCount,
            CriticPromptPath = CriticPromptPath,
            CriticOnHandOff = CriticOnHandOff,
            CustomReflectionNote = CustomReflectionNote,
            CommonPrompts = CommonPrompts,
            Temperature = Temperature,
            OutputType = OutputType,
            //CreatedAt = CreatedAt,
            //UpdatedAt = UpdatedAt
        };
    }
}
