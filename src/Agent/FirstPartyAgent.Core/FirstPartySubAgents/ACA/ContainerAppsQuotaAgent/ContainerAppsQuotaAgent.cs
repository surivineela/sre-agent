// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Runtime.SubAgents.Core;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppsQuotaAgent
{
    public record ContainerAppsQuotaAgentInput(
            ContainerAppsQuotaAgentActivityInput Input,
            IReadOnlyList<string> ToolSignatures,
            Guid ThreadId
        )
        
    {
        
    }


    [DurableTask]
    public class ContainerAppsQuotaAgent : GenericAgentOrchestrator<ContainerAppsQuotaAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, ContainerAppsQuotaAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<ContainerAppsQuotaAgent>();
            // Initial planning phase: generate plan (e.g. list of apps to update)
            var chatHistory = await context.CallContainerAppsQuotaAgentActivityAsync(agentInput.Input);

            var introMessage = await context.CallActivityAsync<ChatMessage>(new TaskName(nameof(ContainerAppRevisionAgentActivity)), agentInput);
            // todo - it would be better if this message is in the context, but skipping on adding it for now in case it breaks demo flow.
            // chatHistory.Add(introMessage);

            chatHistory = await context.CallSendSummaryAndStartActivityAsync(
                new GetNextActionInput
                {
                    ChatMessages = chatHistory,
                    StepCounter = 0,
                    ToolSignatures = [],
                });

            // Run the generic reasoning loop to get actions and process function calls until the plan is complete.
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
