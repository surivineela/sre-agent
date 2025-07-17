// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Logging;
using Agent.Plugins.Interface;
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

        public Task<string> PostAsync(string message)
        {
            _logger.LogInternalInformation("MockPostToTeamsPlugin: Posting message to Teams: {Message}", message);
            return Task.FromResult("Message posted successfully");
        }

        public Task<bool> PostTeamsMessage(string threadId, Activity message, string messageId = "")
        {
            _logger.LogInternalInformation("MockPostToTeamsPlugin: Posting Teams message with threadId {ThreadId}: {Message}", threadId, message);
            return Task.FromResult(true);
        }

        public Task<bool> CreateTeamsThread(string threadId, string initialMessage, string messageId)
        {
            _logger.LogInternalInformation("MockPostToTeamsPlugin: Posting message to Teams with retry: {Message}", initialMessage);
            return Task.FromResult(true);
        }
    }
}

