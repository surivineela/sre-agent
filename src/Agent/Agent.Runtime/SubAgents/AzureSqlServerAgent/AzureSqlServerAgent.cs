// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Microsoft.DurableTask;

namespace Agent.Runtime.SubAgents.AzureSqlServerAgent
{
    public record AzureSqlServerAgentInput(
        AzureSqlServerAgentActivityInput Input, 
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
        : SimpleResourceSubAgentInput<AzureSqlServerAgentActivityInput>(Input, ToolSignatures, ThreadId)
    {
        public AzureSqlServerAgentInput()
            : this(
                  new AzureSqlServerAgentActivityInput(FeatureState.Disabled, new List<SimpleResourceSubAgentResourceInformation>()),
                  new List<string>(),
                  Guid.Empty
                  )
        {
        }
    }

    [DurableTask]
    public class AzureSqlServerAgent : SimpleResourceSubAgentBase<AzureSqlServerAgentInput, AzureSqlServerActivity, AzureSqlServerAgentActivityInput>
    {
    }
}

