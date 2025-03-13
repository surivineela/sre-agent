using Microsoft.DurableTask;
using Agent.Runtime.Communication;

namespace Agent.Runtime.SubAgents.Core;

public record SendMessageInput(
    string ThreadId,
    string Message);

[DurableTask]
public class SendMessageActivity : TaskActivity<SendMessageInput, string>
{
    private readonly ICommunicationService _communicationService;

    public SendMessageActivity(ICommunicationService communicationService)
    {
        _communicationService = communicationService;
    }

    public override async Task<string> RunAsync(TaskActivityContext context, SendMessageInput input)
    {
        await _communicationService.SendMessageAsync(input.ThreadId, input.Message);
        return "Message sent";
    }
}

public record NotifyCompletionInput(
    string ThreadId,
    string InstanceId,
    string Status,
    string? Summary = null);

[DurableTask]
public class NotifyCompletionActivity : TaskActivity<NotifyCompletionInput, string>
{
    private readonly ICommunicationService _communicationService;

    public NotifyCompletionActivity(ICommunicationService communicationService)
    {
        _communicationService = communicationService;
    }

    public override async Task<string> RunAsync(TaskActivityContext context, NotifyCompletionInput input)
    {
        await _communicationService.NotifyCompletionAsync(
            input.ThreadId, input.InstanceId, input.Status, input.Summary);
        return "Completion notification sent";
    }
}
