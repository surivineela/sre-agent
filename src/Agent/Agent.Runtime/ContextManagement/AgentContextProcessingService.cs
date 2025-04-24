// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.ContextManagement;

public sealed class AgentContextProcessingService(
    ILogger<AgentContextProcessingService> logger,
    ILoggerFactory loggerFactory,
    IInstanceManagementRepository instanceManagementRepository,
    IThreadRepository threadRepository,
    InstanceManagementSettings instanceManagementSettings,
    [FromKeyedServices("function-invocation-enabled")] IChatClient chatClient,
    IServiceProvider serviceProvider,
    IAgentOutboundCommunicationService outboundCommunicationService
)
{
    private readonly ConcurrentDictionary<string, ReasoningLoopProcessor> _agentContexts = new();

    public int RunningCount => _agentContexts.Count;

    public async Task HandleUpdateAsync(IEnumerable<AgentContextInstanceAssignment> assignments)
    {
        foreach (var assignment in assignments)
        {
            // extend assignment expiration
            var extendedAssignment = assignment with
            {
                Expires = DateTimeOffset.UtcNow.AddSeconds(instanceManagementSettings.InstanceAssignmentTTLSeconds)
            };

            await instanceManagementRepository.UpdateAgentContextInstanceAssignmentAsync(extendedAssignment);

            // create new processor if needed

            if (_agentContexts.TryGetValue(extendedAssignment.AgentContextId, out var entry) && entry != null)
            {
                // loop already running, do nothing
                continue;
            }

            // start loop
            var processor = StartReasoningLoop(extendedAssignment);
            _ = _agentContexts.AddOrUpdate(extendedAssignment.AgentContextId, processor, (_, _) => processor);
        }
    }

    private ReasoningLoopProcessor StartReasoningLoop(AgentContextInstanceAssignment assignment)
    {
        ReasoningLoopProcessor processor = new(
            assignment,
            threadRepository,
            chatClient,
            loggerFactory,
            serviceProvider,
            outboundCommunicationService,
            instanceManagementSettings.ReasoningLoopMaxRetryCount);

        processor.OnReasoningFinished += HandleReasoningComplete;

        Task.Run(processor.RunLoopAsync);

        logger.LogInformation(
            "Started new reasoning loop processor for agent context {AgentContextId} on Instance {InstanceId}",
            assignment.AgentContextId, assignment.InstanceId);

        return processor;
    }

    private void HandleReasoningComplete(object? sender, string agentContextId)
    {
        ReasoningLoopProcessor notifyingProcessor = sender as ReasoningLoopProcessor ?? throw new InvalidOperationException();

        if (_agentContexts.TryGetValue(agentContextId, out var entry) && entry != null && entry.Equals(notifyingProcessor))
        {
            // assignment document will be removed by the dispatch service, just clean up our own state here
            _agentContexts.Remove(agentContextId, out _);
        }
    }
}
