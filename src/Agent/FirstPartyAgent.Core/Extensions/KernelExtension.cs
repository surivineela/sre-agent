// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models;
using Azure.Identity;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace FirstPartyAgent.Core.Extensions
{
    public static class KernelExtensions
    {
        public static async Task LogInformation(this Kernel kernel, string info, ILogger logger, ITeamsClient teamsClient = null, ISessionMessageService sessionMessageService = null)
        {
            logger.LogInformation(info);
            var sessionId = kernel.Data.ContainsKey("sessionId") ? (string)kernel.Data["sessionId"] : string.Empty;

            if (teamsClient != null && teamsClient.IsEnabled() && teamsClient.SendLogsToTeams())
            {
                string agentMode = kernel.Data.TryGetValue("agentMode", out var val) ? val.ToString() : AgentMode.None.ToString();
                var teamsMessage = new TeamsMessage(info, null);
                teamsMessage.MessageId = sessionId;
                await teamsClient.PostMessageOnTeams(agentMode, teamsMessage).ConfigureAwait(false);
            }

            if (sessionMessageService != null)
            {
                if(!string.IsNullOrWhiteSpace(sessionId))
                {
                    var publisher = sessionMessageService.GetPublisher(sessionId);
                    if(publisher != null)
                    {
                        await publisher(info).ConfigureAwait(false);
                    }
                }
            }
        }

        public static async Task<string> RunAsync(this Kernel kernel, string systemPrompt)
        {
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            var history = new ChatHistory();
            history.AddSystemMessage(systemPrompt);


            var result = await chatCompletionService.GetChatMessageContentAsync(
                history,
                executionSettings: new AzureOpenAIPromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
                    MaxTokens = 4096,
                    Temperature = 0.5
                },
                kernel: kernel);

            return result.Content;

        }
    }
}

