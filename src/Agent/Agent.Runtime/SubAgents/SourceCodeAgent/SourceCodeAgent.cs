using System.Text;
using Agent.Core.Interfaces;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.SourceCodeAgent
{
    [DurableTask]
    public class SourceCodeAgent : GenericAgentOrchestrator<SourceCodeAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, SourceCodeAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<SourceCodeAgent>();
            // Initial planning phase: generate plan (e.g. list of apps to update)
            List<ChatMessage> chatHistory = await context.CallSourceCodePlanActivityAsync(agentInput.Input);

            var introMessage = await context.CallActivityAsync<ChatMessage>(new TaskName(nameof(SourceCodeSendIntroActivity)), agentInput);
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
                agentInput.ThreadId,
                log);

            return "success";
        }

        protected override async Task OnPlanComplete(TaskOrchestrationContext context, string threadId)
        {
            await context.CallUpdateThreadWithAgentMessageActivityAsync(new UpdateThreadWithAgentMessageInput(threadId, context.InstanceId, "Thanks for your help!"));
        }
    }

    [DurableTask]
    public class SourceCodeSendIntroActivity : TaskActivity<SourceCodeAgentInput, ChatMessage>
    {
        private readonly IAgentOutboundCommunicationService _subAgentOutboundCommunicationService;

        public SourceCodeSendIntroActivity(IAgentOutboundCommunicationService subAgentOutboundCommunicationService)
        {
            _subAgentOutboundCommunicationService = subAgentOutboundCommunicationService;
        }

        public override async Task<ChatMessage> RunAsync(TaskActivityContext context, SourceCodeAgentInput agentInput)
        {
            StringBuilder introMessage = new StringBuilder("""
                I work to link source repository urls with applications in order to perform richer analysis on the apps. Here are the apps that I need source repo URLs for:  
                """);

            introMessage.AppendLine();
            foreach (var app in agentInput.Input.AppsWithoutSourceCodeNodes)
            {
                introMessage.AppendLine($"- {app.ResourceId}");
            }

            var newMessage = new ChatMessage(ChatRole.Assistant, introMessage.ToString());

            await _subAgentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                agentInput.ThreadId,
                context.InstanceId,
                newMessage);

            return newMessage;
        }
    }
}
