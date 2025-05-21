// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Extensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppJobsAgent
{
    [DurableTask]
    public class ContainerAppJobsAgentActivity : TaskActivity<ContainerAppJobsAgentActivityInput, List<ChatMessage>>
    {
        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppJobsAgentActivity> _logger;

        public ContainerAppJobsAgentActivity(IChatClient chatClient, ILogger<ContainerAppJobsAgentActivity> logger)
        {
            _logger = logger;
            _chatClient = chatClient;
        }

        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppJobsAgentActivityInput input)
        {
            _logger.LogInformation($"JobsAgentActivity started with input: {JsonSerializer.Serialize(input)}");

            var systemPrompt = await GetPromptTextAsync();
        
            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - Container App Name: {input.JobName}
                    - Job Name: {input.JobName}
                    - Job Execution Id: {input.JobExecutionId}
                    - Resource Group Name: {input.ResourceGroupName}
                    - Managed Environment Name: {input.ManagedEnvironmentName}
                    - Managed Cluster Name: {input.ManagedClusterName}
                    - Subscription: {input.SubscriptionId}
                    - Region: {input.Region}
                    - From: {input.FromDate:O}
                    - To: {input.ToDate:O}
                    ")
                    ];

            _logger.LogInformation("JobsAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            _logger.LogInformation("JobsAgentActivity completed with response.");
            return messages;
        }

        public async Task<string> GetPromptTextAsync()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppJobsAgent), "ContainerAppJobsAgentPlan.txt");
            var systemPrompt = await File.ReadAllTextAsync(path);
            return systemPrompt;
        }
    }
}
