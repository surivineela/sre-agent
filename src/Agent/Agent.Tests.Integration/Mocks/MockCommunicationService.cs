using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Runtime.Communication;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Tests.Integration.Mocks
{
    public class MockCommunicationService : ISubAgentOutboundCommunicationService
    {
        private readonly ILogger _logger;
        public MockCommunicationService(ILogger logger)
        {
            _logger = logger;
        }

        public List<string> Messages { get; } = new List<string>();

        public Task NotifyCompletionAsync(string threadId, string instanceId, string status, string? summary = null)
        {
            _logger.LogInformation($"ThreadId: {threadId}, InstanceId: {instanceId}, Status: {status}");
            Messages.Add(summary);
            return Task.CompletedTask;
        }

        public Task UpdateThreadWithAgentMessageAsync(string threadId, string agentId, ChatMessage message)
        {
            _logger.LogInformation($"ThreadId: {threadId}, AgentId: {agentId}, Message: {message.Text}");
            Messages.Add(message.Text);
            return Task.CompletedTask;
        }
    }
}
