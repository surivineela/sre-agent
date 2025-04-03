// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Definitions;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Mocks
{
    public class MockPostToTeamsPlugin : IPostToTeamsPlugin
    {
        private readonly ILogger<MockPostToTeamsPlugin> _logger;

        public MockPostToTeamsPlugin(ILogger<MockPostToTeamsPlugin> logger)
        {
            _logger = logger;
        }

        public async Task<string> PostAsync(string message)
        {
            _logger.LogInformation("MockPostToTeamsPlugin: Posting message to Teams: {Message}", message);
            return "Message posted successfully";
        }

        public async Task<bool> PostTeamsMessage(string threadId, Activity message, string messageId = "")
        {
            _logger.LogInformation("MockPostToTeamsPlugin: Posting Teams message with threadId {ThreadId}: {Message}", threadId, message);
            return true;
        }

        public async Task<bool> CreateTeamsThread(string threadId, string initialMessage, string messageId)
        {
            _logger.LogInformation("MockPostToTeamsPlugin: Posting message to Teams with retry: {Message}", initialMessage);
            return true;
        }
    }
}

