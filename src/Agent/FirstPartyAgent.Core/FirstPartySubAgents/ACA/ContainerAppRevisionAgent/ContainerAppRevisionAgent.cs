// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;

// Follow the pattern of https://msazure.visualstudio.com/One/_git/AAPT-Antares-OperationalAgent?path=/docs/adding-a-sub-agent.md&_a=preview&version=GBmain
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent
{
    // [MENDATORY]
    public record RevisionAgentInput(
        ContainerAppRevisionAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
        : SimpleResourceSubAgentInput<ContainerAppRevisionAgentActivityInput>(Input, ToolSignatures, ThreadId)
    {
        public RevisionAgentInput()
            : this(
                  new ContainerAppRevisionAgentActivityInput(new List<SimpleResourceSubAgentResourceInformation>()),
                  new List<string>(),
                  Guid.Empty
                  )
        {
        }
    }

    // [MENDATORY]
    [DurableTask]
    public class ContainerAppRevisionAgent : SimpleResourceSubAgentBase<RevisionAgentInput, ContainerAppRevisionAgentActivity, ContainerAppRevisionAgentActivityInput>
    {
    }
}

