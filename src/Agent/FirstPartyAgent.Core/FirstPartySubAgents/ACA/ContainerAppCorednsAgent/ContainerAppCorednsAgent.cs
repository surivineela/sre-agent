// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;

// Follow the pattern of https://msazure.visualstudio.com/One/_git/AAPT-Antares-OperationalAgent?path=/docs/adding-a-sub-agent.md&_a=preview&version=GBmain
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.CorednsAgent
{
    // [MENDATORY]
    public record CorednsAgentInput(
        ContainerAppCorednsAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
        : SimpleResourceSubAgentInput<ContainerAppCorednsAgentActivityInput>(Input, ToolSignatures, ThreadId)
    {
        public CorednsAgentInput()
            : this(
                  new ContainerAppCorednsAgentActivityInput(new List<SimpleResourceSubAgentResourceInformation>()),
                  new List<string>(),
                  Guid.Empty
                  )
        {
        }
    }

    // [MENDATORY]
    [DurableTask]
    public class ContainerAppCorednsAgent : SimpleResourceSubAgentBase<CorednsAgentInput, ContainerAppCorednsAgentActivity, ContainerAppCorednsAgentActivityInput>
    {
    }
}

