// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppsQuotaAgent
{
    public record ContainerAppsQuotaAgentInput(
            ContainerAppsQuotaAgentActivityInput Input,
            IReadOnlyList<string> ToolSignatures,
            Guid ThreadId
        )
        : SimpleResourceSubAgentInput<ContainerAppsQuotaAgentActivityInput>(Input, ToolSignatures, ThreadId)
    {
        public ContainerAppsQuotaAgentInput()
            : this(
                new ContainerAppsQuotaAgentActivityInput(new List<SimpleResourceSubAgentResourceInformation>()),
                new List<string>(),
                Guid.Empty)
        {
        }
    }


    [DurableTask]
    public class ContainerAppsQuotaAgent : SimpleResourceSubAgentBase<ContainerAppsQuotaAgentInput, ContainerAppsQuotaAgentActivity, ContainerAppsQuotaAgentActivityInput>
    {
    }
}
