// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Tests.Common.Mocks
{
    public class MockCommunicationService : IAgentOutboundCommunicationService
    {
        private readonly ILogger? _logger;
        public MockCommunicationService(ILogger? logger)
        {
            _logger = logger;
        }

        public List<string> Messages { get; } = new List<string>();

        public Task<Guid> AppendAgentApprovalMessage(Guid threadId, Approval approval)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, Approval id: {approval.Id}, Approval status: {approval.Status}");
            Messages.Add(approval.Description);
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<Guid> AppendAgentImageMessage(Guid threadId, string message)
        {
            throw new NotImplementedException();
        }

        public Task AppendAgentStreamMessage(Guid threadId, string message, StreamMessageType type, Guid? messageId = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger?.LogInternalInformation($"Mock: Streaming message for thread {threadId} with type {type}: {message}");
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task NotifyCompletionAsync(string threadId, string instanceId, string status, string? summary = null)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, InstanceId: {instanceId}, Status: {status}");
            Messages.Add(summary);
            return Task.CompletedTask;
        }

        public Task NotifyCompletionAsync(AgentContext context, string subAgentIdentifier, string status, string? summary = null)
        {
            _logger?.LogInternalInformation($"ThreadId: {context.ThreadId}, SubAgentIdentifier: {subAgentIdentifier}, Status: {status}");

            if (summary != null)
            {
                Messages.Add(summary);
            }

            return Task.CompletedTask;
        }

        public Task PostActivity(string threadId, Activity activity, string messageId = "")
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, Activity: {activity.Text}");
            return Task.CompletedTask;
        }

        public Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string agentId, ChatMessage message)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, AgentId: {agentId}, Message: {message.Text}");
            Messages.Add(message.Text);
            return Task.CompletedTask;
        }

        public Task UpdateThreadWithAgentMessageAsync(AgentContext context, ChatMessage message)
        {
            _logger?.LogInternalInformation($"ThreadId: {context.ThreadId}, Message: {message.Text}");
            Messages.Add(message.Text);
            return Task.CompletedTask;
        }
    }
}

