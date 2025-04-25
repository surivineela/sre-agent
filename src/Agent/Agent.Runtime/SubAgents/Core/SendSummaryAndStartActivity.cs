// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Core.Interfaces;
using Agent.Core.Extensions;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class SendSummaryAndStartActivity : TaskActivity<GetNextActionInput, List<ChatMessage>>
{
    private readonly IChatClient _chatClient;
    private readonly IAgentOutboundCommunicationService _communicationService;
    private readonly ILogger<SendSummaryAndStartActivity> _logger;

    public SendSummaryAndStartActivity(
        IChatClient chatClient,
        IAgentOutboundCommunicationService communicationService,
        ILogger<SendSummaryAndStartActivity> logger)
    {
        _chatClient = chatClient;
        _communicationService = communicationService;
        _logger = logger;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, GetNextActionInput input)
    {
        var chatMessages = input.ChatMessages;

        chatMessages.Add(new ChatMessage(ChatRole.System, """
                Now that the plan is complete, I would share a comprehensive summary of the steps I'll take
                """
        ));

        var response = await _chatClient.GetResponseAsync(chatMessages);
        chatMessages.Add(response.GetMessage());

        // Get thread ID from parent orchestration context
        // Since we can't use GetInput<dynamic>, we need to pass the threadId explicitly
        // This should be handled where the activity is called (in ManagedIdentityMigrationAgent.cs)

        // Get any available text from response
        var messageText = response.GetMessage().Contents.OfType<TextContent>().FirstOrDefault()?.Text
            ?? "I've created a plan for your managed identity migration.";

        // NOTE: Thread ID should be passed explicitly to this activity from the orchestrator
        // This will be done in the orchestrator code that calls this activity

        chatMessages.Add(new ChatMessage(ChatRole.User, "Great, lets start executing the plan."));

        return chatMessages;
    }
}
