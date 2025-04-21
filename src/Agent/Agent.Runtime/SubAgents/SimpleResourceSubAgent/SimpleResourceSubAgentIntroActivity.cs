using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents
{
    /// <summary>
    /// This is a simple bit of info required to send the intro message/plan to the user.
    /// </summary>
    /// <param name="Context"></param>
    /// <param name="IntroText"></param>
    public record SimpleResourceSubAgentIntroActivityInput(Guid ThreadId, string IntroText);

    /// <summary>
    /// This is a simple activity that sends the intro message/plan to the user. It does not need to be
    /// inherited from; it applies the same to all simple sub-agents.
    /// </summary>
    [DurableTask]
    public class SimpleResourceSubAgentIntroActivity : TaskActivity<SimpleResourceSubAgentIntroActivityInput, ChatMessage>
    {
        private readonly IAgentOutboundCommunicationService _subAgentOutboundCommunicationService;

        public SimpleResourceSubAgentIntroActivity(IAgentOutboundCommunicationService subAgentOutboundCommunicationService)
        {
            _subAgentOutboundCommunicationService = subAgentOutboundCommunicationService;
        }

        public override async Task<ChatMessage> RunAsync(TaskActivityContext context, SimpleResourceSubAgentIntroActivityInput agentInput)
        {
            var newMessage = new ChatMessage(ChatRole.Assistant, agentInput.IntroText);

            await _subAgentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                agentInput.ThreadId,
                context.InstanceId,
                newMessage);

            return newMessage;
        }
    }
}
