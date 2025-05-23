// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Extensions;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.Common;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCustomerLogsAgent
{
    public enum MissingLogsType
    {
        ContainerAppSystemLogs = 1,
        ContainerAppConsoleLogs = 2,
        Both = 3
    }

    public record ContainerAppCustomerLogsAgentActivityInput : BaseContainerAppIssueActivityInput
    {

        [Description("The name of the container app. Skip if not provided.")]
        public string ContainerAppName { get; init; } = string.Empty;

        [Description("The type of missing logs. It can be ContainerAppSystemLogs, ContainerAppConsoleLogs or Both.")]
        public MissingLogsType MissingLogsType { get; init; } = MissingLogsType.Both;
    }

    [DurableTask]
    public class ContainerAppCustomerLogsAgentActivity : TaskActivity<ContainerAppCustomerLogsAgentActivityInput, List<ChatMessage>>
    {

        private readonly IChatClient _chatClient;
        private readonly ILogger<ContainerAppCustomerLogsAgentActivity> _logger;

        public ContainerAppCustomerLogsAgentActivity(IChatClient chatClient, ILogger<ContainerAppCustomerLogsAgentActivity> logger)
        {
            _logger = logger;
            _chatClient = chatClient;
        }

        public override async Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppCustomerLogsAgentActivityInput input)
        {
            _logger.LogInformation($"ContainerAppCustomerLogsAgentActivity started with input: {JsonSerializer.Serialize(input)}");


            var systemPrompt = await GetPromptTextAsync(input);

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.System, @$"
                    Input information
                    - Container App Name: {input.ContainerAppName}
                    - Resource Group Name: {input.ResourceGroupName}
                    - Managed Environment Name: {input.ManagedEnvironmentName}
                    - Subscription Id: {input.SubscriptionId}
                    - Region: {input.Region}
                    - From: {input.FromDate:O}
                    - To: {input.ToDate:O}
                    - Missing Logs Type: {input.MissingLogsType}    
                    ")
                    ];

            _logger.LogInformation("ContainerAppCustomerLogsAgentActivity sending messages to chat client.");
            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            _logger.LogInformation("ContainerAppCustomerLogsAgentActivity completed with response.");
            return messages;
        }

        public async Task<string> GetPromptTextAsync(ContainerAppCustomerLogsAgentActivityInput input)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartySubAgents), "ACA", nameof(ContainerAppCustomerLogsAgent), "ContainerAppCustomerLogsAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            return systemPrompt;
        }
    }
}
