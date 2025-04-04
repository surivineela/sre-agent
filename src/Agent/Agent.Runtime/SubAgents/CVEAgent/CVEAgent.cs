// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Interfaces;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.CVEAgent
{
    [DurableTask]
    public class CVEAgent : GenericAgentOrchestrator<CVEAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, CVEAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<CVEAgent>();
            // Initial planning phase: generate plan (e.g. list of apps to update)
            List<ChatMessage> chatHistory = await context.CallCVEPlanActivityAsync(agentInput.Input);

            var introMessage = await context.CallActivityAsync<ChatMessage>(new TaskName(nameof(CVEAgentSendIntroActivity)), agentInput);
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
                agentInput.Context,
                log);

            return "success";
        }
    }

    [DurableTask]
    public class CVEAgentSendIntroActivity : TaskActivity<CVEAgentInput, ChatMessage>
    {
        private readonly IAgentOutboundCommunicationService _subAgentOutboundCommunicationService;

        public CVEAgentSendIntroActivity(IAgentOutboundCommunicationService subAgentOutboundCommunicationService)
        {
            _subAgentOutboundCommunicationService = subAgentOutboundCommunicationService;
        }

        public override async Task<ChatMessage> RunAsync(TaskActivityContext context, CVEAgentInput agentInput)
        {
            StringBuilder introMessage = new StringBuilder("""
                I will scan the following list of github repo urls in order to find any security vulnerabilities:

                """);

            foreach (var repo in agentInput.Input.ReposToScan)
            {
                introMessage.AppendLine($"**{repo.RepoUrl}**");
            }

            var newMessage = new ChatMessage(ChatRole.Assistant, introMessage.ToString());

            await _subAgentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                agentInput.Context,
                context.InstanceId,
                newMessage);

            return newMessage;
        }
    }
}

