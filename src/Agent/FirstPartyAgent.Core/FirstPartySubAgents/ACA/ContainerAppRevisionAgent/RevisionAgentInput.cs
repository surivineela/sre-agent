// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// Follow the pattern of https://msazure.visualstudio.com/One/_git/AAPT-Antares-OperationalAgent?path=/docs/adding-a-sub-agent.md&_a=preview&version=GBmain
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent
{
    public record RevisionAgentInput(
    ContainerAppRevisionAgentActivityInput Input,
    IReadOnlyList<string> ToolSignatures,
    Guid ThreadId)
    {
       
    }

}

