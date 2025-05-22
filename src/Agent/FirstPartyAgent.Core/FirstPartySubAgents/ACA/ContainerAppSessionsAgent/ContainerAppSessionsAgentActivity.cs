using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Extensions;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppSessionsAgent
{
    public record ContainerAppSessionsAgentActivityInput : BaseContainerAppIssueActivityInput  {
        [Description("[Required] The name of the session pool to analyze. Example: 'my-session-pool'")]
        public string SessionPoolName { get; init; } = string.Empty;
    }

    [DurableTask]
    public class ContainerAppSessionsAgentActivity : TaskActivity<ContainerAppSessionsAgentActivityInput, List<ChatMessage>>
    {

        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppSessionsAgentActivity> _logger;

        public ContainerAppSessionsAgentActivity(IChatClient chatClient, ILogger<ContainerAppSessionsAgentActivity> logger)
        {
            _logger = logger;
            _chatClient = chatClient;
        }

        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppSessionsAgentActivityInput input)
        {
            _logger.LogInformation($"ContainerAppSessionsAgentActivity started with input: {JsonSerializer.Serialize(input)}");


            var systemPrompt = await GetPromptTextAsync(input);

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - From: {input.FromDate:O}
                    - To: {input.ToDate:O}
                    - Region: {input.Region}
                    - SubscriptionId: {input.SubscriptionId}
                    - ResourceGroupName: {input.ResourceGroupName}
                    - SessionPool Name: {input.SessionPoolName}
                    - Managed Environment Name: {input.ManagedEnvironmentName}
                    - IcM ID: {input.IcmId}
                    - Issue Description: {input.IssueDescription}
                    ")
                    ];

            _logger.LogInformation("ContainerAppSessionsAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            _logger.LogInformation("ContainerAppSessionsAgentActivity completed with response.");
            return messages;
        }

        public async Task<string> GetPromptTextAsync(ContainerAppSessionsAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartySubAgents), "ACA", nameof(ContainerAppSessionsAgent), "ContainerAppSessionsAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
