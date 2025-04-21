// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask;

namespace Agent.Runtime.SubAgents.ServiceBusAgent
{
    public record ServiceBusAgentInput(
        ServiceBusAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
        : SimpleResourceSubAgentInput<ServiceBusAgentActivityInput>(Input, ToolSignatures, ThreadId)
    {
        public ServiceBusAgentInput()
            : this(
                  new ServiceBusAgentActivityInput(FeatureState.Disabled, new List<SimpleResourceSubAgentResourceInformation>()),
                  new List<string>(),
                  Guid.Empty
                  )
        {
        }
    }

    [DurableTask]
    public class ServiceBusAgent : SimpleResourceSubAgentBase<ServiceBusAgentInput, ServiceBusAgentActivity, ServiceBusAgentActivityInput>
    {
    }
}

