// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Runtime;
using Agent.Tests.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
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

        public static async Task<ChatResponse> SendMessage(IChatClient chatClient, string message, ITestOutputHelper _output)
        {
            _output.WriteLine($"Sending message: {message}");
            return await chatClient.GetResponseAsync(message);
        }

    }
}

