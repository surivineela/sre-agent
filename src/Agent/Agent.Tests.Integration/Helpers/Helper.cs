using Agent.Runtime;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Agent.Tests.Integration.Helpers
{
    internal static class Helper
    {
        public static async Task SendMessageAndWait(IChatClient chatClient, string message, ITestOutputHelper _output, int delayInSeconds = 5)
        {
            await SendMessage(chatClient, message, _output);

            await Task.Delay(TimeSpan.FromSeconds(delayInSeconds));
        }

        public static async Task<ChatCompletion> SendMessage(IChatClient chatClient, string message, ITestOutputHelper _output)
        {
            _output.WriteLine($"Sending message: {message}");
            return await chatClient.CompleteAsync(message);
        }
    }
}
