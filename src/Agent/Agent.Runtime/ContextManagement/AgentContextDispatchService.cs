// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Data.Repositories;
using Agent.Runtime.V2;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.ContextManagement;

/// <summary>
/// Service for assigning agent contexts that need processing to worker instances
/// </summary>
public sealed class AgentContextDispatchService(
    ILogger<AgentContextDispatchService> logger,
    IInstanceManagementRepository instanceManagementRepository,
    IThreadRepository threadRepository,
    CosmosDBSettings cosmosDbSettings,
    CosmosClient cosmosClient,
    InstanceManagementSettings instanceManagementSettings
)
{
    private const string AgentAssignmentProcessorName = "AgentContextAssignment";

    private readonly string _databaseId = cosmosDbSettings.Docs.Database;

    private ChangeFeedProcessor? _changeFeedProcessor;

    private bool _isRunning = false;

    public async Task StartAsync(string instanceId)
    {
        if (!instanceManagementSettings.ProcessingEnabled)
        {
            return;
        }

        if (_isRunning)
        {
            return;
        }

        var agentContextContainer = cosmosClient.GetContainer(_databaseId, AgentDataConfiguration.AgentContextContainerName);
        var leaseContainer = cosmosClient.GetContainer(_databaseId, AgentDataConfiguration.LeaseContainerName);

        _changeFeedProcessor = agentContextContainer
            .GetChangeFeedProcessorBuilder<AgentContextDocument>(
                AgentAssignmentProcessorName,
                HandleAgentContextChanges)
            .WithErrorNotification(HandleError)
            .WithInstanceName(instanceId)
            .WithLeaseContainer(leaseContainer)
            .Build();

        await _changeFeedProcessor.StartAsync();

        _isRunning = true;
    }

    public async Task StopAsync()
    {
        if (_changeFeedProcessor != null)
        {
            await _changeFeedProcessor.StopAsync();
            _changeFeedProcessor = null;
        }

        _isRunning = false;
    }

    private async Task HandleAgentContextChanges(
        ChangeFeedProcessorContext context,
        IReadOnlyCollection<AgentContextDocument> changes,
        CancellationToken cancellationToken)
    {
        foreach (var agentContext in changes)
        {
            // if this handoff context was completed, remove the assignment info
            // do this before validating agent type, because the type will get reset back
            // to the delegating agent type when the handoff completed
            if (agentContext.HandoffState == ContextStateEnum.Completed || agentContext.HandoffState == ContextStateEnum.Failed)
            {
                // check if we need to delete the assignment doc
                if (!string.IsNullOrEmpty(agentContext.AssignedInstanceId))
                {
                    await instanceManagementRepository.DeleteAgentContextInstanceAssignmentAsync(agentContext.Id, agentContext.AssignedInstanceId);

                    await threadRepository
                        .UpdateAgentContextAssignmentInfoAsync(
                            Guid.Parse(agentContext.Id),
                            Guid.Parse(agentContext.ThreadId),
                            assignedInstanceId: null,
                            expiration: null);
                }

                continue;
            }

            // do not assign meta agents or durable agents for processing
            if (agentContext.AgentType == AgentTypeEnum.Meta
                || agentContext.AgentType == AgentTypeEnum.DTS)
            {
                continue;
            }

            // TODO: temporary condition, only handle onboarded types
            if (!SubAgentV2TypeMapping.IsSubAgentV2(agentContext.AgentType))
            {
                continue;
            }

            // check completion state if this wasn't a handoff
            if (agentContext.ContextState == ContextStateEnum.Completed || agentContext.ContextState == ContextStateEnum.Failed)
            {
                // check if we need to delete the assignment doc
                if (!string.IsNullOrEmpty(agentContext.AssignedInstanceId))
                {
                    await instanceManagementRepository.DeleteAgentContextInstanceAssignmentAsync(agentContext.Id, agentContext.AssignedInstanceId);

                    await threadRepository
                        .UpdateAgentContextAssignmentInfoAsync(
                            Guid.Parse(agentContext.Id),
                            Guid.Parse(agentContext.ThreadId),
                            assignedInstanceId: null,
                            expiration: null);
                }

                continue;
            }

            // check if assignment is needed
            if (string.IsNullOrEmpty(agentContext.AssignedInstanceId)
                || agentContext.AssignmentExpires == null
                || agentContext.AssignmentExpires < DateTimeOffset.UtcNow)
            {
                // 1. delete existing assignment document if needed
                if (!string.IsNullOrEmpty(agentContext.AssignedInstanceId))
                {
                    await instanceManagementRepository.DeleteAgentContextInstanceAssignmentAsync(agentContext.Id, agentContext.AssignedInstanceId);
                }

                // 2. fetch worker to assign agent context
                var selectedWorker = await SelectInstanceForAgentContextAsync();

                if (selectedWorker == null)
                {
                    logger.LogError("Failed to get select worker to assign agent context {agentContextId}", agentContext.Id);
                    continue;
                }

                var expiration = DateTimeOffset.UtcNow.AddSeconds(instanceManagementSettings.InstanceAssignmentTTLSeconds);

                // 3. update assignment info on agent context
                // this will trigger another change feed notification
                // but the assignment info will be updated, so this
                // logic is skipped
                var success = await threadRepository
                    .UpdateAgentContextAssignmentInfoAsync(
                        Guid.Parse(agentContext.Id),
                        Guid.Parse(agentContext.ThreadId),
                        selectedWorker.Id,
                        expiration);

                if (!success)
                {
                    logger.LogError("Failed to create agent context instance assignment for agent context {agentContextId}",
                        agentContext.Id);
                    continue;
                }

                // 4. create new assignment document
                await instanceManagementRepository
                    .CreateAgentContextInstanceAssignmentAsync(
                        new(
                            agentContext.Id,
                            agentContext.ThreadId,
                            selectedWorker.Id,
                            expiration
                        )
                    );
            }
        }
    }

    private Task HandleError(string leaseToken, Exception exception)
    {
        if (exception is ChangeFeedProcessorUserException userException)
        {
            logger.LogError(userException, "Lease {changeFeedLeaseToken} processing failed with unhandled exception from user delegate: {innerException}", leaseToken, userException.InnerException);
        }
        else
        {
            logger.LogError(exception, "Lease {changeFeedLeaseToken} processing failed, exception source not from user delegate", leaseToken);
        }

        // TODO: failure to assign a context needs to be retried

        return Task.CompletedTask;
    }

    private async Task<WorkerInstance?> SelectInstanceForAgentContextAsync()
    {
        // fetch all instances
        // this will be in order of least assignments to most
        var instances = await instanceManagementRepository.GetAllReadyWorkerInstancesAsync();

        return instances.FirstOrDefault();
    }
}
