// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Tests.Integration.Mocks
{
    public class MockCommunicationService : IAgentOutboundCommunicationService
    {
        private readonly ILogger _logger;
        public MockCommunicationService(ILogger logger)
        {
            _logger = logger;
        }

        public List<string> Messages { get; } = new List<string>();

        public Task<Guid> AppendAgentImageMessage(ThreadContext threadContext, string message)
        {
            throw new NotImplementedException();
        }

        public Task NotifyCompletionAsync(string threadId, string instanceId, string status, string? summary = null)
        {
            _logger.LogInformation($"ThreadId: {threadId}, InstanceId: {instanceId}, Status: {status}");
            Messages.Add(summary);
            return Task.CompletedTask;
        }

        public Task PostActivity(string threadId, Activity activity, string messageId = "")
        {
            _logger.LogInformation($"ThreadId: {threadId}, Activity: {activity.Text}");
            return Task.CompletedTask;
        }

        public Task UpdateThreadWithAgentMessageAsync(ThreadContext? threadContext, string agentId, ChatMessage message)
        {
            _logger.LogInformation($"ThreadId: {threadContext?.ThreadId}, AgentId: {agentId}, Message: {message.Text}");
            Messages.Add(message.Text);
            return Task.CompletedTask;
        }
    }
}

