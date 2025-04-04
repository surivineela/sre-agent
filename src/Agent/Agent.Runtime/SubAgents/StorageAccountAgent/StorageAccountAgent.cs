// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.StorageAccountAgent
{
    public sealed record StorageAccountAgentInput(
        [Description("Defines if key based access and public blob access should be disabled. Contains a list of statuses for retrieved resources.")] 
        StorageAccountAgentPlanInput Input,
        [Description("The set of tools used by this agent")]
        IReadOnlyList<string> ToolSignatures,
        [Description("The context in which this agent is running")]
        ThreadContext Context);

    [DurableTask]
    public class StorageAccountAgent : GenericAgentOrchestrator<StorageAccountAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, StorageAccountAgentInput storageAccountAgentInput)
        {
            var log = context.CreateReplaySafeLogger<StorageAccountAgent>();

            // Initial planning phase: generate plan
            List<Microsoft.Extensions.AI.ChatMessage> chatHistory = await context.CallStorageAccountAgentPlanActivityAsync(storageAccountAgentInput.Input);

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
                storageAccountAgentInput.ToolSignatures,
                storageAccountAgentInput.Context,
                log);

            return "success";
        }
    }
}

