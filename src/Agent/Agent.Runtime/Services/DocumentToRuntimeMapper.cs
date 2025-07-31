// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Data.Tools;
using Agent.Plugins.Tools;
using Agent.Framework.Reasoning.Models;

namespace Agent.Web.Services;

public static class DocumentToRuntimeMapper
{
    

 
    public static YamlAgentDescriptor ToRuntimeAgent(AgentDocumentModel api) => new YamlAgentDescriptor
    {
        AgentsAsTools = api.AgentsAsTools,
        Name = api.Name,
        Instructions = api.Instructions,
        HandoffDescription = api.HandoffDescription,
        Handoffs = api.Handoffs,
        Tools = api.Tools,
        Connectors = api.Connectors,
        AllowParallelToolCalls = api.AllowParallelToolCalls,
        MaxReflectionCount = api.MaxReflectionCount,
        CriticPromptPath = api.CriticPromptPath,
        CriticOnHandOff = api.CriticOnHandOff,
        CustomReflectionNote = api.CustomReflectionNote,
        CommonPrompts = api.CommonPrompts,
        CommonTools = api.CommonTools,
        Temperature = api.Temperature,
        OutputType = api.OutputType,
      DisableDocumentRetrieval = api.DisableDocumentRetrieval,
      EnableHandoffPromptOverride = api.EnableHandoffPromptOverride,
      UserPromptOverride = api.UserPromptOverride,
      
        Metadata = api.Metadata,
      //  UserPromptOverride = api.UserPromptOverride,
        //Hooks = api.Hooks,
       // FactoryTools = api.FactoryTools

    };
    public static YamlToolDefinitionBase ToRuntimeTool(ToolDocumentModel tool) => tool switch
    {
        KustoToolDocumentModel k => new KustoToolDefinition
        {
            Name = k.Name,
            Type = k.Type,
            Connector = k.Connector,
            Description = k.Description,
            Parameters = k.Parameters,
            Attributes = k.Attributes,
            Metadata = k.Metadata,
            Mode = k.Mode,
            Function = k.Function,
            Query = k.Query,
            File = k.File,
            Database = k.Database,
            ClusterHint = k.ClusterHint,
         
        },
        _ => throw new NotSupportedException($"Unknown tool document type: {tool.Type}")
    };

    public static DataConnectorDefinitionBase ToRuntimeConnector(ConnectorDocumentModel tool) => tool switch
    {
        KustoConnectorDocumentModel k => new KustoConnector
        {
            Name = k.Name,
            Type = k.Type,
            ClusterUrl = k.ClusterUrl,
            Database = k.Database,
            ClusterHint = k.ClusterHint,
            RegionalClusterGroups = k.RegionalClusterGroups,
            Description = k.Description,
            Auth= k.Auth,
            Metadata = k.Metadata,
            Enabled = k.Enabled,


        },
        _ => throw new NotSupportedException($"Unknown tool document type: {tool.Type}")
    };

    


}
