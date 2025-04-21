// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents;
using Microsoft.DurableTask;

// Follow the pattern of https://msazure.visualstudio.com/One/_git/AAPT-Antares-OperationalAgent?path=/docs/adding-a-sub-agent.md&_a=preview&version=GBmain
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.HelloWorldAgent
{
    // [MENDATORY]
    public record HelloWorldAgentInput(
        HelloWorldAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
        : SimpleResourceSubAgentInput<HelloWorldAgentActivityInput>(Input, ToolSignatures, ThreadId)
    {
        public HelloWorldAgentInput()
            : this(
                  new HelloWorldAgentActivityInput(new List<SimpleResourceSubAgentResourceInformation>()),
                  new List<string>(),
                  Guid.Empty
                  )
        {
        }
    }

    // [MENDATORY]
    [DurableTask]
    public class HelloWorldAgent : SimpleResourceSubAgentBase<HelloWorldAgentInput, HelloWorldAgentActivity, HelloWorldAgentActivityInput>
    {
    }
}

