// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask;

namespace Agent.Runtime.SubAgents.StorageAccountAgent
{
    public record StorageAccountAgentInput(
        StorageAccountAgentActivityInput Input, 
        IReadOnlyList<string> ToolSignatures, 
        Guid ThreadId
        )
        : SimpleResourceSubAgentInput<StorageAccountAgentActivityInput>(Input, ToolSignatures, ThreadId)
    {
        public StorageAccountAgentInput()
            : this(
                  new StorageAccountAgentActivityInput(FeatureState.Disabled, FeatureState.Disabled, new List<SimpleResourceSubAgentResourceInformation>()),
                  new List<string>(),
                  Guid.Empty
                  )
        {
        }
    }

    [DurableTask]
    public class StorageAccountAgent : SimpleResourceSubAgentBase<StorageAccountAgentInput, StorageAccountAgentActivity, StorageAccountAgentActivityInput>
    {
    }
}

