using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentRuntime
{
    public static class IntentClassification
    {
        [Function(nameof(ClassifyIntent))]
        public static async Task<string> ClassifyIntent([ActivityTrigger] string messageContent, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("ClassifyIntent");   
            var messages = new List<OpenAIMessage>();
            messages.Add(new OpenAIMessage()
            {
                Role = "system",
                Content = new List<OpenAIMessageContent> {
                    new OpenAIMessageContent()
                    {
                        Type = "text",
                        Text = await GetIntentClassificationPrompt()
                    }
                }
            });

            messages.Add(new OpenAIMessage()
            {
                Role = "user",
                Content = new List<OpenAIMessageContent> {
                    new OpenAIMessageContent()
                    {
                        Type = "text",
                        Text = messageContent
                    }
                }
            });

            string response = await OpenAIHelper.GetOpenAIResponseAsync(messages);
            return response;
        }
        private static Task<string> GetIntentClassificationPrompt()
        {
            return File.ReadAllTextAsync("IntentClassification/IntentClassificationPrompt.txt");
        }

    }
}
