// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvoyAgent
{
    // [MENDATORY]
    public record ContainerAppEnvoyAgentInput(
        ContainerAppEnvoyAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
        : SimpleResourceSubAgentInput<ContainerAppEnvoyAgentActivityInput>(Input, ToolSignatures, ThreadId)
    {
        public ContainerAppEnvoyAgentInput()
            : this(
                  new ContainerAppEnvoyAgentActivityInput(new List<SimpleResourceSubAgentResourceInformation>()),
                  new List<string>(),
                  Guid.Empty
                  )
        {
        }
    }

    // [MENDATORY]
    [DurableTask]
    public class ContainerAppEnvoyAgent : SimpleResourceSubAgentBase<ContainerAppEnvoyAgentInput, ContainerAppEnvoyAgentActivity, ContainerAppEnvoyAgentActivityInput>
    {
    }
}
