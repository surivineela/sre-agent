using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Plugins.Definitions;
using Kusto.Data;
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

        public async Task<bool> PostTeamsMessage(string threadId, Activity message)
        {
            _logger.LogInformation("MockPostToTeamsPlugin: Posting Teams message with threadId {ThreadId}: {Message}", threadId, message);
            return true;
        }

        public async Task<bool> PostToTeamsWithRetry(string message)
        {
            _logger.LogInformation("MockPostToTeamsPlugin: Posting message to Teams with retry: {Message}", message);
            return true;
        }
    }
}
