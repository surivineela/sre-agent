using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Models;
using OperationalAgentRuntime.Skills;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentRuntime
{
    public class IntentClassification
    {
        private readonly IChatClient chatClient;

        public IntentClassification(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }

        [Function(nameof(ClassifyIntent))]
        public async Task<string> ClassifyIntent([ActivityTrigger] string messageContent, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("ClassifyIntent");

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, await GetIntentClassificationPrompt()),
                new ChatMessage(ChatRole.User, messageContent),
            };

            var res = await chatClient.CompleteAsync(messages);
            return res.Message.Text;
        }
        private static Task<string> GetIntentClassificationPrompt()
        {
            return File.ReadAllTextAsync("IntentClassification/IntentClassificationPrompt.txt");
        }

    }
}
