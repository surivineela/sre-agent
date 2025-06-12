// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.MetaAgent.Interfaces;

public interface IAgentsFactory
{
    public string GetMetaAgentSystemPrompt();
    public string GetIncidentHandlerAgentSystemPrompt();
    List<AITool> GetSubAgentsAITools(Guid threadGuid, AgentContext context);
    public List<Type> GetRequiredSubAgentPluginDefinitionTypes();
}
