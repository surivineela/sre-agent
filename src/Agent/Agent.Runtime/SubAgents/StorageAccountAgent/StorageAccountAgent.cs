// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.StorageAccountAgent
{
    public record StorageAccountAgentInput(
        StorageAccountAgentActivityInput Input, 
        IReadOnlyList<string> ToolSignatures, 
        ThreadContext Context
        )
        : SimpleResourceSubAgentInput<StorageAccountAgentActivityInput>(Input, ToolSignatures, Context)
    {
        public StorageAccountAgentInput()
            : this(
                  new StorageAccountAgentActivityInput(FeatureState.Disabled, FeatureState.Disabled, new List<SimpleResourceSubAgentResourceInformation>()),
                  new List<string>(),
                  new ThreadContext(Guid.Empty, AgentTypeEnum.DurableAgent)
                  )
        {
        }
    }

    [DurableTask]
    public class StorageAccountAgent : SimpleResourceSubAgentBase<StorageAccountAgentInput, StorageAccountAgentActivity, StorageAccountAgentActivityInput>
    {
    }
}

