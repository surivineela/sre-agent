using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Extensions;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent
{
    public record ContainerAppRevisionAgentActivityInput : BaseContainerAppIssueActivityInput
    {
        [Description("The name of the container app.")]
        public string ContainerAppName { get; init; } = string.Empty;

        [Description("The revision name of the container app.")]
        public string RevisionName { get; init; } = string.Empty;
    }

    [DurableTask]
    public class ContainerAppRevisionAgentActivity : TaskActivity<ContainerAppRevisionAgentActivityInput, List<ChatMessage>>
    {

        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppRevisionAgentActivity> _logger;

        public ContainerAppRevisionAgentActivity(IChatClient chatClient, ILogger<ContainerAppRevisionAgentActivity> logger)
        {
            _logger = logger;
            _chatClient = chatClient;
        }

        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppRevisionAgentActivityInput input)
        {
            _logger.LogInformation($"ContainerAppRevisionAgentActivity started with input: {JsonSerializer.Serialize(input)}");

            
            var systemPrompt = await GetPromptTextAsync(input);
        
            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - Container App Name: {input.ContainerAppName}
                    - Revision Name: {input.RevisionName}
                    - Resource Group Name: {input.ResourceGroupName}
                    - Managed Environment Name: {input.ManagedEnvironmentName}
                    - Subscription: {input.SubscriptionId}
                    - Region: {input.Region}
                    - From: {input.FromDate:O}
                    - To: {input.ToDate:O}
                    ")
                    ];

            _logger.LogInformation("ContainerAppRevisionAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            _logger.LogInformation("ContainerAppRevisionAgentActivity completed with response.");
            return messages;
        }

        public async Task<string> GetPromptTextAsync(ContainerAppRevisionAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppRevisionAgent), "ContainerAppRevisionAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
