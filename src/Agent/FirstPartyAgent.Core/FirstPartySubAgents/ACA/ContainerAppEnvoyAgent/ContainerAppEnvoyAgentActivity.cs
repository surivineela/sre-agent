using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Extensions;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvoyAgent
{
    // [MENDATORY]
    public record ContainerAppEnvoyAgentActivityInput : BaseContainerAppIssueActivityInput 
    {
        [Description("The name of the container app.")]
        public string ContainerAppName { get; init; } = string.Empty;       
    }

    // [MENDATORY]
    [DurableTask]
    public class ContainerAppEnvoyAgentActivity : TaskActivity<ContainerAppEnvoyAgentActivityInput, List<ChatMessage>>
    {
        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppEnvoyAgentActivity> _logger;
        public ContainerAppEnvoyAgentActivity(IChatClient chatClient, ILogger<ContainerAppEnvoyAgentActivity> logger) 
        {
            _chatClient = chatClient;
            _logger = logger;
        }

        
        
        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppEnvoyAgentActivityInput input)
        {
           
            _logger.LogInformation($"ContainerAppRevisionAgentActivity started with input: {JsonSerializer.Serialize(input)}");


            var systemPrompt = await GetPromptTextAsync(input);

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - Container App Name: {input.ContainerAppName}
                    - Region: {input.Region}
                    - From: {input.FromDate:O}
                    - To: {input.ToDate:O}
                    ")
                    ];

            _logger.LogInformation("ContainerAppEnvoyAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            _logger.LogInformation("ContainerAppEnvoyAgentActivity completed with response.");
            return messages;

        }
        public  async Task<string> GetPromptTextAsync(ContainerAppEnvoyAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppEnvoyAgent), "ContainerAppEnvoyAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
