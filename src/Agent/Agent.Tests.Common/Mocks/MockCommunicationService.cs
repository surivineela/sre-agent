// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
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

        public Task<Guid> AppendAgentImageMessage(Guid threadId, string message, Guid messageId = default)
        {
            Messages.Add(message);
            return Task.FromResult(Guid.NewGuid());
        }

        public Task AppendAgentManualToolCallMessage(Guid threadId, List<ManualToolCall>? manualToolCalls, Guid? messageId = null, CancellationToken cancellationToken = default)
        {
            Messages.AddRange(manualToolCalls?.Select(call => call.FunctionCall.Name) ?? Enumerable.Empty<string>());
            return Task.FromResult(Guid.NewGuid());
        }

        public Task AppendAgentManualToolCallResult(Guid threadId, List<ManualToolCallResult>? manualToolCallResults, Guid? messageId = null, CancellationToken cancellationToken = default)
        {
            Messages.AddRange(manualToolCallResults?.Select(result => result.FunctionCall.Name) ?? Enumerable.Empty<string>());
            return Task.CompletedTask;
        }

        public Task AppendAgentStreamMessage(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, Guid? agentTaskId = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger?.LogInternalInformation($"Mock: Streaming message for thread {threadId} with type {type}: {message}");
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task AppendAgentTaskUpdate(Guid threadId, string taskData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger?.LogInternalInformation($"Mock: Task update for thread {threadId}: {taskData}");
            Messages.Add(taskData);
            return Task.CompletedTask;
        }

        public Task AppendAgentToolCallMessage(Guid threadId, AIFunction aiTool, Guid? messageId = null, string? callId = null, CancellationToken cancellationToken = default)
        {
            Messages.Add(aiTool.Name);
            return Task.CompletedTask;
        }

        public Task AppendAgentToolCallResult(Guid threadId, FunctionResultContent result, Guid? messageId = null, CancellationToken cancellationToken = default)
        {
            Messages.Add(result.Result?.ToString() ?? string.Empty);
            return Task.CompletedTask;
        }

        public Task AppendUserStreamMessage(Guid threadId, string displayName, string message, Guid messageId, string? userId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task NotifyAzCliUpdate(Guid threadId, AzCliExecution execution, Guid messageId = default)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, Command: {execution.Command}, Status: {execution.Status}");
            Messages.Add(execution.Command);
            return Task.CompletedTask;
        }

        public Task NotifyKubectlUpdate(Guid threadId, KubectlExecution execution, Guid messageId = default)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, Command: {execution.Command}, Status: {execution.Status}");
            Messages.Add(execution.Command);
            return Task.CompletedTask;
        }

        public Task NotifyApprovalUpdate(Guid threadId, Approval approval, Guid messageId = default)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, ApprovalId: {approval.Id}, Status: {approval.Status}");
            Messages.Add(approval.Description);
            return Task.CompletedTask;
        }

        public Task NotifyCompletionAsync(string threadId, string instanceId, string status, string? summary = null)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, InstanceId: {instanceId}, Status: {status}");
            Messages.Add(summary ?? string.Empty);
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

        public Task NotifyGenericAgentMessage(Guid threadId, Message message, StreamMessageType? type)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, Message: {message.Text}");
            Messages.Add(message.Text);
            return Task.CompletedTask;
        }

        public Task NotifyThreadEvent(Guid threadId, Core.Models.Api.v1.Thread thread)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}");
            return Task.CompletedTask;
        }

        public Task NotifyActionAsync(Guid threadId, Core.Models.Api.v1.Action action)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, Action: {action.Title}");
            return Task.CompletedTask;
        }

        public Task NotifyIncidentStatusMetrics(Guid threadId, IncidentStatusMetrics metrics, Guid? messageId = null)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, Metrics: {metrics}");
            return Task.CompletedTask;
        }

        public Task PostActivity(string threadId, Activity activity, string messageId = "")
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, Activity: {activity.Text}");
            return Task.CompletedTask;
        }

        public Task SignalProcessingComplete(Guid threadId, Guid? messageId = null, CancellationToken cancellationToken = default)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}");
            return Task.CompletedTask;
        }

        public Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message, Guid? messageId = null, StreamMessageType? type = null, Guid? agentTaskId = null)
        {
            _logger?.LogInternalInformation($"ThreadId: {threadId}, OrchestrationInstanceId: {orchestrationInstanceId}, Message: {message.Text}");
            Messages.Add(message.Text);
            return Task.CompletedTask;
        }

        public Task UpdateThreadWithAgentMessageAsync(AgentContext context, ChatMessage message, Guid? messageId = null)
        {
            _logger?.LogInternalInformation($"ThreadId: {context.ThreadId}, Message: {message.Text}");
            Messages.Add(message.Text);
            return Task.CompletedTask;
        }
    }
}

