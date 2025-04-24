// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents;
using Agent.Runtime.V2.ContainerAppsAgent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.V2;

public static class SubAgentV2TypeMapping
{
    private static readonly IReadOnlyList<AgentTypeEnum> _onboardedAgentTypes =
        [
            AgentTypeEnum.ContainerAppsRemediation
        ];

    public static bool IsSubAgentV2(AgentTypeEnum agentType)
    {
        return _onboardedAgentTypes.Contains(agentType);
    }

    public static ISubAgentV2 GetAgentForContext(
        AgentContext agentContext,
        IChatClient chatClient,
        IToolsRepository toolsRepository,
        IThreadRepository threadRepository,
        IAgentOutboundCommunicationService outboundCommunicationService,
        ILoggerFactory loggerFactory)
    {
        return agentContext.AgentType switch
        {
            AgentTypeEnum.ContainerAppsRemediation =>
                new SubAgentV2<ContainerAppsRemediationAgentV2, string>(
                    agentContext,
                    chatClient,
                    toolsRepository,
                    threadRepository,
                    outboundCommunicationService,
                    loggerFactory),
            _ => throw new ArgumentOutOfRangeException(nameof(agentContext.AgentType), $"No conversion known for agentType {agentContext.AgentType}"),
        };
    }
}
