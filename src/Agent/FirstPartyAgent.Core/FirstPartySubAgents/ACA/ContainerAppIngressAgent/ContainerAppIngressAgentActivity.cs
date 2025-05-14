using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Extensions;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppIngressAgent
{
    public record ContainerAppIngressAgentActivityInput : BaseContainerAppIssueActivityInput 
    {
        [Description("The name of the container app.")]
        public string ContainerAppName { get; init; } = string.Empty;
        [Description("The resource group name of the container app.")]
        public string ResourceGroupName { get; init; } = string.Empty;
        [Description("The subscription ID of the container app.")]
        public string SubscriptionId { get; init; } = string.Empty;
    }

    [DurableTask]
    public class ContainerAppIngressAgentActivity : TaskActivity<ContainerAppIngressAgentActivityInput, List<ChatMessage>>
    {
        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppIngressAgentActivity> _logger;
        public ContainerAppIngressAgentActivity(IChatClient chatClient, ILogger<ContainerAppIngressAgentActivity> logger) 
        {
            _chatClient = chatClient;
            _logger = logger;
        }

        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppIngressAgentActivityInput input)
        {
           
            _logger.LogInformation($"ContainerAppRevisionAgentActivity started with input: {JsonSerializer.Serialize(input)}");

            var systemPrompt = await GetPromptTextAsync(input);

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - Container App Name: {input.ContainerAppName}
                    - Resource Group Name: {input.ResourceGroupName}
                    - Managed Environment Name: {input.ManagedEnvironmentName}
                    - Subscription Id: {input.SubscriptionId}
                    - Region: {input.Region}
                    - From: {input.FromDate:O}
                    - To: {input.ToDate:O}
                    ")
                    ];

            _logger.LogInformation("ContainerAppIngressAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            _logger.LogInformation("ContainerAppIngressAgentActivity completed with response.");
            return messages;

        }
        public  async Task<string> GetPromptTextAsync(ContainerAppIngressAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppIngressAgent), "ContainerAppIngressAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
