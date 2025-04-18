// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.ManagedIdentityMigration
{
    [DurableTask]
    public class ManagedIdentityMigrationAgent : GenericAgentOrchestrator<ManagedIdentityMigrationAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, ManagedIdentityMigrationAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<ManagedIdentityMigrationAgent>();

            // Initial planning phase: generate plan
            List<ChatMessage> chatHistory = await context.CallManagedIdentityPlanActivityAsync(agentInput.Input);

            // Send a summary and start the execution
            chatHistory = await context.CallSendSummaryAndStartActivityAsync(
                new GetNextActionInput
                {
                    ChatMessages = chatHistory,
                    StepCounter = 0,
                    ToolSignatures = [],
                });

            // Run the generic reasoning loop to get actions and process function calls until the plan is complete
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

