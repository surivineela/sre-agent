// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask;

namespace Agent.Runtime.SubAgents.EventHubAgent
{
    public record EventHubAgentInput(
        EventHubAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
        : SimpleResourceSubAgentInput<EventHubAgentActivityInput>(Input, ToolSignatures, ThreadId)
    {
        public EventHubAgentInput()
            : this(
                  new EventHubAgentActivityInput(FeatureState.Disabled, new List<SimpleResourceSubAgentResourceInformation>()),
                  new List<string>(),
                  Guid.Empty
                  )
        {
        }
    }

    [DurableTask]
    public class EventHubAgent : SimpleResourceSubAgentBase<EventHubAgentInput, EventHubAgentActivity, EventHubAgentActivityInput>
    {
    }
}

