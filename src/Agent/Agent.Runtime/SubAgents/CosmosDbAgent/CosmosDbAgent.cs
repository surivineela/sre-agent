// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.CosmosDbAgent
{
    public record CosmosDbAgentInput(
        CosmosDbAgentActivityInput Input, 
        IReadOnlyList<string> ToolSignatures, 
        Guid ThreadId
        )
        : SimpleResourceSubAgentInput<CosmosDbAgentActivityInput>(Input, ToolSignatures, ThreadId)
    {
        public CosmosDbAgentInput()
            : this(
                  new CosmosDbAgentActivityInput(FeatureState.Disabled, new List<SimpleResourceSubAgentResourceInformation>()),
                  new List<string>(),
                  Guid.Empty
                  )
        {
        }
    }

    [DurableTask]
    public class CosmosDbAgent : SimpleResourceSubAgentBase<CosmosDbAgentInput, CosmosDbAgentActivity, CosmosDbAgentActivityInput>
    {
    }
}

