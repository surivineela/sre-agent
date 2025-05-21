// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppJobsAgent
{
    public record ContainerAppJobsAgentInput(
        ContainerAppJobsAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId)
    {
       
    }

}
