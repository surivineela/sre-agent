// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCustomerMetricsAgent
{
    public record CustomerMetricsAgentInput(
        ContainerAppCustomerMetricsAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
    )
    {
    }

    [DurableTask]
    public class ContainerAppCustomerMetricsAgent : GenericAgentOrchestrator<CustomerMetricsAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, CustomerMetricsAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<ContainerAppCustomerMetricsAgent>();
            var chatHistory = await context.CallContainerAppCustomerMetricsAgentActivityAsync(agentInput.Input);

            chatHistory = await RunReasoningLoopAsync(
                context,
                chatHistory,
                agentInput.ToolSignatures,
                log,
                agentInput.ThreadId);

            return "success";
        }
    }
}
