using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Agent.Core.Extensions;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppsQuotaAgent
{
    public record ContainerAppsQuotaAgentActivityInput 
    {
        [Required]
        [Description("The Incidentid (IcM ID) associated with the issue. Example: '622811149'")]
        public string IncidentId { get; init; } = string.Empty;
    }

    [DurableTask]
    public class ContainerAppsQuotaAgentActivity : TaskActivity<ContainerAppsQuotaAgentActivityInput, List<ChatMessage>>
    {
        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppsQuotaAgentActivity> _logger;

        public ContainerAppsQuotaAgentActivity(IChatClient chatClient, ILogger<ContainerAppsQuotaAgentActivity> logger)
        {
            _logger = logger;
            _chatClient = chatClient;
        }
        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppsQuotaAgentActivityInput input)
        {
            _logger.LogInformation($"ContainerAppsQuotaAgentActivity started with input: {JsonSerializer.Serialize(input)}");

            var systemPrompt = await GetPromptTextAsync(input);

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - IcM ID: {input.IncidentId}
                    ")
                    ];

            _logger.LogInformation("ContainerAppsQuotaAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            _logger.LogInformation("ContainerAppsQuotaAgentActivity completed with response.");
            return messages;
        }



        public async Task<string> GetPromptTextAsync(ContainerAppsQuotaAgentActivityInput input)
        {
            // Read the system prompt from a file
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppsQuotaAgent), "ContainerAppsQuotaAgent.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
