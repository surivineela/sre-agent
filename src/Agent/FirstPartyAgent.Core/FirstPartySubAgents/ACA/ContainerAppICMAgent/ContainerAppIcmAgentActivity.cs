using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Extensions;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppIcmAgent
{
    // ICM Agent Input doesn't inherit from BaseContainerAppIssueActivityInput because it only needs IcmId. 
    public record ContainerAppIcmAgentActivityInput {

        [Description("The Incident ID (IcM ID) associated with the issue. Example: '622811149'")]
        public string? IcmId { get; init; } = string.Empty;
    }

    [DurableTask]
    public class ContainerAppIcmAgentActivity : TaskActivity<ContainerAppIcmAgentActivityInput, List<ChatMessage>>
    {

        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppIcmAgentActivity> _logger;

        public ContainerAppIcmAgentActivity(IChatClient chatClient, ILogger<ContainerAppIcmAgentActivity> logger)
        {
            _logger = logger;
            _chatClient = chatClient;
        }

        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppIcmAgentActivityInput input)
        {
            _logger.LogInformation($"ContainerAppIcmAgentActivity started with input: {JsonSerializer.Serialize(input)}");


            var systemPrompt = await GetPromptTextAsync(input);

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - IcM ID: {input.IcmId}
                    ")
                    ];

            _logger.LogInformation("ContainerAppIcmAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            _logger.LogInformation("ContainerAppIcmAgentActivity completed with response.");
            return messages;
        }

        public async Task<string> GetPromptTextAsync(ContainerAppIcmAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartySubAgents), "ACA", nameof(ContainerAppIcmAgent), "ContainerAppIcmAgentPlan.txt");
            try
            {
                var systemPrompt = File.ReadAllText(path);
                return systemPrompt;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read system prompt from file at path: {Path}", path);
                throw;
            }
        }
    }
}
