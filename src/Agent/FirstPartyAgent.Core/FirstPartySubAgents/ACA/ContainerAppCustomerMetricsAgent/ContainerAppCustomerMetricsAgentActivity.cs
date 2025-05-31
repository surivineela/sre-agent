using Microsoft.DurableTask;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Agent.Core.Extensions;
using System.ComponentModel;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCustomerMetricsAgent
{
    public record ContainerAppCustomerMetricsAgentActivityInput : BaseContainerAppIssueActivityInput
    {
        [Description("[Required] The name of the managed Kubernetes cluster or azure container apps environment associated with the container app. Example: 'victoriouspond-6e0afa3a'")]
        public string ClusterName { get; init; } = string.Empty;

        [Description("[Required] The name of the container app to investigate. Example: 'appName'")]
        public string ContainerAppName { get; init; } = string.Empty;
    }

    [DurableTask]
    public class ContainerAppCustomerMetricsAgentActivity : TaskActivity<ContainerAppCustomerMetricsAgentActivityInput, List<ChatMessage>>
    {
        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppCustomerMetricsAgentActivity> _logger;

        public ContainerAppCustomerMetricsAgentActivity(IChatClient chatClient, ILogger<ContainerAppCustomerMetricsAgentActivity> logger)
        {
            _logger = logger;
            _chatClient = chatClient;
        }

        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppCustomerMetricsAgentActivityInput input)
        {
            _logger.LogInformation($"ContainerAppCustomerMetricsAgentActivity started with input: {JsonSerializer.Serialize(input)}");

            var systemPrompt = await GetPromptTextAsync(input);

            var containerAppArmId = $"/subscriptions/{input.SubscriptionId}/resourceGroups/{input.ResourceGroupName}/providers/Microsoft.App/containerApps/{input.ContainerAppName}";

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - Cluster Name: {input.ClusterName}
                    - Region: {input.Region}
                    - From: {input.FromDate:O}
                    - To: {input.ToDate:O}
                    - ContainerAppArmId: {containerAppArmId}
                    - IcM ID: {input.IcmId}
                    - Issue Description: {input.IssueDescription}
                    ")
            ];

            _logger.LogInformation("ContainerAppCustomerMetricsAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            return messages;
        }

        public async Task<string> GetPromptTextAsync(ContainerAppCustomerMetricsAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartySubAgents), "ACA", nameof(ContainerAppCustomerMetricsAgent), "ContainerAppCustomerMetricsAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }

    }
}
