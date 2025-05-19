using Microsoft.DurableTask;
using Agent.Runtime.Services;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Agent.Core.Extensions;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCustomerMetricsAgent
{
    public record ContainerAppCustomerMetricsAgentActivityInput : BaseContainerAppIssueActivityInput
    {

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

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt)
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
