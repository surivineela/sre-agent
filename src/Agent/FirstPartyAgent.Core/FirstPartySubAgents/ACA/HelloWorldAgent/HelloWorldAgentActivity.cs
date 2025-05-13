using System.ComponentModel;
using Agent.Core.Extensions;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.HelloWorldAgent
{
    public record HelloWorldAgentActivityInput : BaseContainerAppIssueActivityInput {
        [Description("Resource id")]
        public string? ResourceId { get; init; }
    }
    

    [DurableTask]
    public class HelloWorldAgentActivity : TaskActivity<HelloWorldAgentActivityInput, List<ChatMessage>>
    {
        private readonly IChatClient _chatClient;
        private readonly ILogger<HelloWorldAgentActivity> _logger;
        public HelloWorldAgentActivity(IChatClient chatClient, ILogger<HelloWorldAgentActivity> logger)
        {
            _logger = logger;
            _chatClient = chatClient;
        }

        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, HelloWorldAgentActivityInput input)
        {
            var resourcesStr = input.ResourceId;
            var userMessage = $"Here are the resources that need updating: {resourcesStr}";

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, await GetPromptTextAsync(input)),
                new ChatMessage(ChatRole.User, userMessage)
                ];

            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            return messages;
        }
       

        public  async Task<string> GetPromptTextAsync(HelloWorldAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(HelloWorldAgent), "HelloWorldAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
