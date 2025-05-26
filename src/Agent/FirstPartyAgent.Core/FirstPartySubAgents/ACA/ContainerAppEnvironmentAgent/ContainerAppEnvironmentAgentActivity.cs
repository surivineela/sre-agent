using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Agent.Core.Extensions;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvironmentAgent
{
    public record ContainerAppEnvironmentAgentActivityInput : BaseContainerAppIssueActivityInput
    {
        [Description("The resource URL of the container app environment. Example: '/subscriptions/xxxx/resourceGroups/xxxx/providers/Microsoft.App/managedEnvironments/xxxx'")]
        public string? EnvironmentResourceURL { get; init; } = string.Empty;

        [Description("The managed cluster name. Example: 'calmcliff-7c82e181'")]
        public string? ManagedClusterName { get; init; } = string.Empty;
    }

    [DurableTask]
    public class ContainerAppEnvironmentAgentActivity : TaskActivity<ContainerAppEnvironmentAgentActivityInput, List<ChatMessage>>
    {
        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppEnvironmentAgentActivity> _logger;

        public ContainerAppEnvironmentAgentActivity(IChatClient chatClient, ILogger<ContainerAppEnvironmentAgentActivity> logger)
        {
            _logger = logger;
            _chatClient = chatClient;
        }
        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppEnvironmentAgentActivityInput input)
        {
            _logger.LogInformation($"ContainerAppEnvironmentAgentActivity started with input: {JsonSerializer.Serialize(input)}");

            var systemPrompt = await GetPromptTextAsync(input);

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - IcM ID: {input.IcmId}
                    - Region: {input.Region}
                    - From: {input.FromDate:O}
                    - To: {input.ToDate:O}
                    - Environment Resource URL: {input.EnvironmentResourceURL}
                    - Managed Cluster Name: {input.ManagedClusterName}
                    ")
                    ];

            _logger.LogInformation("ContainerAppEnvironmentAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            _logger.LogInformation("ContainerAppEnvironmentAgentActivity completed with response.");
            return messages;
        }



        public async Task<string> GetPromptTextAsync(ContainerAppEnvironmentAgentActivityInput input)
        {
            // Read the system prompt from a file
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppEnvironmentAgent), "ContainerAppEnvironmentAgent.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
