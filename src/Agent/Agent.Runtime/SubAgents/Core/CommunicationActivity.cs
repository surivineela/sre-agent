// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.DurableTask;
using Agent.Core.Interfaces;
using Microsoft.Extensions.AI;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.SubAgents.Core;

public record UpdateThreadWithAgentMessageInput(
    ThreadContext ThreadContext,
    string InstanceId,
    string Message);

[DurableTask]
public class UpdateThreadWithAgentMessageActivity : TaskActivity<UpdateThreadWithAgentMessageInput, string>
{
    private readonly IAgentOutboundCommunicationService _subAgentOutboundCommunicationService;

    public UpdateThreadWithAgentMessageActivity(IAgentOutboundCommunicationService subAgentOutboundCommunicationService)
    {
        _subAgentOutboundCommunicationService = subAgentOutboundCommunicationService;
    }

    public override async Task<string> RunAsync(TaskActivityContext context, UpdateThreadWithAgentMessageInput input)
    {
        await _subAgentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            input.ThreadContext,
            input.InstanceId,
            new ChatMessage(ChatRole.Assistant, input.Message));

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
    private readonly IAgentOutboundCommunicationService _communicationService;

    public NotifyCompletionActivity(IAgentOutboundCommunicationService communicationService)
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

