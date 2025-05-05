// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

// Follow the pattern of https://msazure.visualstudio.com/One/_git/AAPT-Antares-OperationalAgent?path=/docs/adding-a-sub-agent.md&_a=preview&version=GBmain
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.HelloWorldAgent
{
    // [MENDATORY]
    public record HelloWorldAgentInput(
        HelloWorldAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
        
    {
        
    }

    // [MENDATORY]
    [DurableTask]
    public class HelloWorldAgent: GenericAgentOrchestrator<HelloWorldAgentInput, string>
    {
        public async override Task<string> RunAsync(TaskOrchestrationContext context, HelloWorldAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<HelloWorldAgent>();
            // Initial planning phase: generate plan (e.g. list of apps to update)
            var chatHistory = await context.CallHelloWorldAgentActivityAsync(agentInput.Input);

            var introMessage = await context.CallActivityAsync<ChatMessage>(new TaskName(nameof(HelloWorldAgentActivity)), agentInput);
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

