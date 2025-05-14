using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Agent.Core.Extensions;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppQuotaAgent
{
    public record ContainerAppQuotaAgentActivityInput 
    {
        [Required]
        [Description("The Incidentid (IcM ID) associated with the issue. Example: '622811149'")]
        public string IncidentId { get; init; } = string.Empty;
    }

    [DurableTask]
    public class ContainerAppQuotaAgentActivity : TaskActivity<ContainerAppQuotaAgentActivityInput, List<ChatMessage>>
    {
        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppQuotaAgentActivity> _logger;

        public ContainerAppQuotaAgentActivity(IChatClient chatClient, ILogger<ContainerAppQuotaAgentActivity> logger)
        {
            _logger = logger;
            _chatClient = chatClient;
        }
        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppQuotaAgentActivityInput input)
        {
            _logger.LogInformation($"ContainerAppQuotaAgentActivity started with input: {JsonSerializer.Serialize(input)}");

            var systemPrompt = await GetPromptTextAsync(input);

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - IcM ID: {input.IncidentId}
                    ")
                    ];

            _logger.LogInformation("ContainerAppQuotaAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            _logger.LogInformation("ContainerAppQuotaAgentActivity completed with response.");
            return messages;
        }



        public async Task<string> GetPromptTextAsync(ContainerAppQuotaAgentActivityInput input)
        {
            // Read the system prompt from a file
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppQuotaAgent), "ContainerAppQuotaAgent.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
