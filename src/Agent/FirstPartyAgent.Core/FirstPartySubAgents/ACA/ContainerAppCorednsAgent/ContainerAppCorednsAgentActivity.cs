using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Extensions;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCorednsAgent
{
    // [MENDATORY]
    public record ContainerAppCorednsAgentActivityInput : BaseContainerAppIssueActivityInput  {
        [Description("[Required] The name of the managed Kubernetes cluster or azure container apps environment associated with the container app. Example: 'victoriouspond-6e0afa3a'")]
        public string ManagedClusterName { get; init; } = string.Empty;
    }

    // [MENDATORY]
    [DurableTask]
    public class ContainerAppCorednsAgentActivity : TaskActivity<ContainerAppCorednsAgentActivityInput, List<ChatMessage>>
    {

        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppCorednsAgentActivity> _logger;

        public ContainerAppCorednsAgentActivity(IChatClient chatClient, ILogger<ContainerAppCorednsAgentActivity> logger)
        {
            _logger = logger;
            _chatClient = chatClient;
        }

        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppCorednsAgentActivityInput input)
        {
            _logger.LogInformation($"ContainerAppCorednsAgentActivity started with input: {JsonSerializer.Serialize(input)}");


            var systemPrompt = await GetPromptTextAsync(input);

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - Managed Cluster Name: {input.ManagedClusterName}
                    - Region: {input.Region}
                    - From: {input.FromDate:O}
                    - Managed Environment Name: {input.ManagedEnvironmentName}
                    - To: {input.ToDate:O}
                    - IcM ID: {input.IcmId}
                    - Issue Description: {input.IssueDescription}
                    ")
                    ];

            _logger.LogInformation("ContainerAppCorednsAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            _logger.LogInformation("ContainerAppCorednsAgentActivity completed with response.");
            return messages;
        }

        public async Task<string> GetPromptTextAsync(ContainerAppCorednsAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartySubAgents), "ACA", nameof(ContainerAppCorednsAgent), "ContainerAppCorednsAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
