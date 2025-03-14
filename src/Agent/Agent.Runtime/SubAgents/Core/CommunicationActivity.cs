using Microsoft.DurableTask;
using Agent.Runtime.Communication;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;

public record UpdateThreadWithAgentMessageInput(
    string ThreadId,
    string AgentId,
    string Message);

[DurableTask]
public class UpdateThreadWithAgentMessageActivity : TaskActivity<UpdateThreadWithAgentMessageInput, string>
{
    private readonly ISubAgentOutboundCommunicationService _subAgentOutboundCommunicationService;

    public UpdateThreadWithAgentMessageActivity(ISubAgentOutboundCommunicationService subAgentOutboundCommunicationService)
    {
        _subAgentOutboundCommunicationService = subAgentOutboundCommunicationService;
    }

    public override async Task<string> RunAsync(TaskActivityContext context, UpdateThreadWithAgentMessageInput input)
    {
        await _subAgentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            input.ThreadId,
            input.AgentId,
            new ChatMessage(ChatRole.Assistant, input.Message));

        return "Message sent";
    }
}

public record NotifyCompletionInput(
    string ThreadId,
    string AgentId,
    string InstanceId,
    string Status,
    string? Summary = null);

[DurableTask]
public class NotifyCompletionActivity : TaskActivity<NotifyCompletionInput, string>
{
    private readonly ISubAgentOutboundCommunicationService _communicationService;

    public NotifyCompletionActivity(ISubAgentOutboundCommunicationService communicationService)
    {
        _communicationService = communicationService;
    }

    public override async Task<string> RunAsync(TaskActivityContext context, NotifyCompletionInput input)
    {
        await _communicationService.NotifyCompletionAsync(
            input.ThreadId, input.AgentId, input.Status, input.Summary);
        return "Completion notification sent";
    }
}
