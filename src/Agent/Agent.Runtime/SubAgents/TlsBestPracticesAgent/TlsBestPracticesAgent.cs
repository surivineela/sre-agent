using System.Text;
using Agent.Core.Interfaces;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.TlsBestPractices
{
    [DurableTask]
    public class TlsBestPracticesAgent : GenericAgentOrchestrator<TlsBestPracticesAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, TlsBestPracticesAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<TlsBestPracticesAgent>();
            // Initial planning phase: generate plan (e.g. list of apps to update)
            List<ChatMessage> chatHistory = await context.CallTlsPlanActivityAsync(agentInput.Input);

            var introMessage = await context.CallActivityAsync<ChatMessage>(new TaskName(nameof(TlsSendIntroActivity)), agentInput);
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
                agentInput.Context.ThreadId.ToString(),
                log);

            return "success";
        }

        protected override async Task OnPlanComplete(TaskOrchestrationContext context, string threadId)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "TlsBestPracticesAgent", "TlsBestPracticesInfo.txt");
            var info = File.ReadAllText(path);

            await context.CallUpdateThreadWithAgentMessageActivityAsync(new UpdateThreadWithAgentMessageInput(threadId, context.InstanceId, info));
        }
    }

    [DurableTask]
    public class TlsSendIntroActivity : TaskActivity<TlsBestPracticesAgentInput, ChatMessage>
    {
        private readonly IAgentOutboundCommunicationService _subAgentOutboundCommunicationService;

        public TlsSendIntroActivity(IAgentOutboundCommunicationService subAgentOutboundCommunicationService)
        {
            _subAgentOutboundCommunicationService = subAgentOutboundCommunicationService;
        }

        public override async Task<ChatMessage> RunAsync(TaskActivityContext context, TlsBestPracticesAgentInput agentInput)
        {
            StringBuilder introMessage = new StringBuilder("""
                I can update these applications to require TLS 1.2 one at a time. I'll wait 30 seconds between each app and monitor its health during that time.  

                #### Application Updates  

                """);

            foreach (var app in agentInput.Input.AppsInViolation)
            {
                introMessage.AppendLine($"**{app.Name}**: TLS {app.MinimumTlsVersion} -> TLS {agentInput.Input.DesiredVersion}  ");
            }

            introMessage.AppendLine();
            introMessage.AppendLine("Would you like me to proceed as planned above? I can trigger an approval flow.");

            var newMessage = new ChatMessage(ChatRole.Assistant, introMessage.ToString());

            await _subAgentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                agentInput.Context.ThreadId.ToString(),
                context.InstanceId,
                newMessage);

            return newMessage;
        }
    }
}
