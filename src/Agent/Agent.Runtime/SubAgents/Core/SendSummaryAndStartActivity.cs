using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class SendSummaryAndStartActivity : TaskActivity<GetNextActionInput, List<ChatMessage>>
{
    private readonly IChatClient chatClient;
    private readonly ILogger<SendSummaryAndStartActivity> logger;

    public SendSummaryAndStartActivity(IChatClient chatClient, ILogger<SendSummaryAndStartActivity> logger)
    {
        this.chatClient = chatClient;
        this.logger = logger;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, GetNextActionInput input)
    {
        var chatMessages = input.ChatMessages;

        chatMessages.Add(new ChatMessage(ChatRole.System, """
                Now that the plan is complete, I would share a comprehensive summary of the steps I'll take
                """
        ));

        var response = await chatClient.GetResponseAsync(chatMessages);
        chatMessages.Add(response.Message);

        // TODO
        //await PostTlsMessageToTeams(new TeamsMessage(response.Message.Text), client, executionContext);

        chatMessages.Add(new ChatMessage(ChatRole.User, "Great, lets start executing - trigger an approval flow so that I can approve it."));

        return chatMessages;
    }
}
