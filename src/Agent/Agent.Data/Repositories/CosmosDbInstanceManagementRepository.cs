// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Data.Helpers;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

public class CosmosDbInstanceManagementRepository(
    CosmosClient cosmosClient,
    string databaseName,
    ILogger<CosmosDbInstanceManagementRepository> logger,
    InstanceManagementSettings instanceManagementSettings
) : IInstanceManagementRepository
{
    #region Leader Lease Management
    /// <inheritdoc/>
    public async Task<LeaderLease?> GetLeaderLeaseAsync()
    {
        var (leaderLeaseDoc, _) = await GetLeaderLeaseDocumentWithEtagAsync();

        return leaderLeaseDoc?.ToDomainModel();
    }

    /// <inheritdoc/>
    public async Task<(LeaderLease lease, bool isAcquired)> TryAcquireLeaderLeaseAsync(string instanceId)
    {
        logger.LogInternalInformation("Attempting to acquire leader lease for instance {InstanceId}", instanceId);

        var (leaderLeaseDoc, etag) = await GetLeaderLeaseDocumentWithEtagAsync();

        if (leaderLeaseDoc == null)
        {
            logger.LogInternalInformation("No existing leader lease document found");

            // create new leader lease
            leaderLeaseDoc = new LeaderLeaseDocument
            {
                LeaseHolder = instanceId,
                LeaseExpiration = DateTimeOffset.UtcNow.AddSeconds(instanceManagementSettings.LeaderLeaseTTLSeconds)
            };

            try
            {
                ItemResponse<LeaderLeaseDocument> response = await cosmosClient.GetContainer<LeaderLeaseDocument>(databaseName).CreateItemAsync(
                    leaderLeaseDoc,
                    new PartitionKey(leaderLeaseDoc.PartitionKey)
                );

                logger.LogInternalInformation("Acquired leader lease for instance {InstanceId}", instanceId);

                return (response.Resource.ToDomainModel(), true);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // someone else got the lease first, return the updated lease entry
                var leaderLease = await GetLeaderLeaseAsync();
                logger.LogInternalInformation("Failed to acquire leader lease for instance {InstanceId}, acquired by {LeaseHolder}", instanceId, leaderLease?.LeaseHolder);

                return (leaderLease!, false);
            }
        }
        else if (leaderLeaseDoc.LeaseExpiration < DateTimeOffset.UtcNow
            || string.IsNullOrEmpty(leaderLeaseDoc.LeaseHolder))
        {
            if (string.IsNullOrEmpty(leaderLeaseDoc.LeaseHolder))
            {
                logger.LogInternalInformation("Leader lease has been released, attempting to acquire for instance {InstanceId}", instanceId);
            }
            else
            {
                logger.LogInternalInformation("Leader lease held by {LeaseHolder} has expired, attempting to acquire for instance {InstanceId}", leaderLeaseDoc.LeaseHolder, instanceId);
            }

            // expired, try to acquire
            leaderLeaseDoc.LeaseHolder = instanceId;
            leaderLeaseDoc.LeaseExpiration = DateTimeOffset.UtcNow.AddSeconds(instanceManagementSettings.LeaderLeaseTTLSeconds);

            try
            {
                ItemResponse<LeaderLeaseDocument> response = await cosmosClient.GetContainer<LeaderLeaseDocument>(databaseName).ReplaceItemAsync(
                    leaderLeaseDoc,
                    leaderLeaseDoc.Id,
                    new PartitionKey(leaderLeaseDoc.PartitionKey),
                    new ItemRequestOptions
                    {
                        IfMatchEtag = etag
                    }
                );

                logger.LogInternalInformation("Acquired leader lease for instance {InstanceId}", instanceId);

                return (response.Resource.ToDomainModel(), true);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // someone else got the lease first, return the updated lease entry
                var leaderLease = await GetLeaderLeaseAsync();
                logger.LogInternalInformation("Failed to acquire leader lease for instance {InstanceId}, acquired by {LeaseHolder}", instanceId, leaderLease?.LeaseHolder);

                return (leaderLease!, false);
            }
        }
        else if (leaderLeaseDoc.LeaseHolder == instanceId)
        {
            // lease exists and is not expired, but is held by the current instance
            return (leaderLeaseDoc.ToDomainModel(), true);
        }

        // lease exists and is not expired and held by another instance
        return (leaderLeaseDoc.ToDomainModel(), false);
    }

    /// <inheritdoc/>
    public async Task<bool> ReleaseLeaderLeaseAsync(string instanceId)
    {
        logger.LogInternalInformation("Attempting to release leader lease for instance {InstanceId}", instanceId);

        var (leaderLeaseDoc, _) = await GetLeaderLeaseDocumentWithEtagAsync();

        if (leaderLeaseDoc == null)
        {
            logger.LogInternalInformation("No existing leader lease document found");
            return false;
        }
        else if (leaderLeaseDoc.LeaseHolder == instanceId)
        {
            // update document to remove lease holder
            leaderLeaseDoc.LeaseHolder = string.Empty;

            await cosmosClient.GetContainer<LeaderLeaseDocument>(databaseName).ReplaceItemAsync(
                leaderLeaseDoc,
                leaderLeaseDoc.Id,
                new PartitionKey(leaderLeaseDoc.PartitionKey)
            );

            logger.LogInternalInformation("Released leader lease for instance {InstanceId}", instanceId);

            return true;
        }

        logger.LogInternalWarning("Leader lease held by {LeaseHolder}, cannot release from instance {InstanceId}", leaderLeaseDoc.LeaseHolder, instanceId);

        return false;
    }

    /// <inheritdoc/>
    public async Task<(LeaderLease lease, bool isRenewed)> RenewLeaderLeaseAsync(string instanceId)
    {
        logger.LogInternalInformation("Attempting to renew leader lease for instance {InstanceId}", instanceId);

        var (leaderLeaseDoc, _) = await GetLeaderLeaseDocumentWithEtagAsync();

        if (leaderLeaseDoc == null)
        {
            logger.LogInternalInformation("No existing leader lease document found");

            throw new InvalidOperationException("No existing leader lease document found");
        }
        else if (leaderLeaseDoc.LeaseHolder == instanceId)
        {
            // update document to renew lease
            leaderLeaseDoc.LeaseExpiration = DateTimeOffset.UtcNow.AddSeconds(instanceManagementSettings.LeaderLeaseTTLSeconds);

            ItemResponse<LeaderLeaseDocument> response = await cosmosClient.GetContainer<LeaderLeaseDocument>(databaseName).ReplaceItemAsync(
                leaderLeaseDoc,
                leaderLeaseDoc.Id,
                new PartitionKey(leaderLeaseDoc.PartitionKey)
            );

            return (response.Resource.ToDomainModel(), true);
        }

        logger.LogInternalWarning("Leader lease held by {LeaseHolder}, cannot renew from instance {InstanceId}", leaderLeaseDoc.LeaseHolder, instanceId);

        return (leaderLeaseDoc.ToDomainModel(), false);
    }

    private async Task<(LeaderLeaseDocument? document, string? etag)> GetLeaderLeaseDocumentWithEtagAsync()
    {
        try
        {
            return await GetDocumentWithEtagAsync<LeaderLeaseDocument>(Constants.LeaderLeaseName, Constants.LeaderLeaseName);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return (null, null);
        }
    }
    #endregion

    #region Instance Lifetime
    /// <inheritdoc/>
    public async Task<bool> RegisterWorkerInstanceAsync(WorkerInstance instance)
    {
        try
        {
            await cosmosClient.GetContainer<WorkerInstanceDocument>(databaseName).CreateItemAsync(
                WorkerInstanceDocument.FromDomainModel(instance),
                new PartitionKey(instance.Id)
            );

            return true;
        }
        catch (CosmosException ex)
        {
            logger.LogInternalError(ex, "Cosmos exception occurred while registering worker instance {InstanceId}", instance.Id);

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<WorkerInstance?> GetWorkerInstanceAsync(string instanceId)
    {
        var workerInstance = await GetWorkerInstanceDocumentAsync(instanceId);

        return workerInstance?.ToDomainModel();
    }

    /// <inheritdoc/>
    public async Task<List<WorkerInstance>> GetAllReadyWorkerInstancesAsync()
    {
        //QueryDefinition query = new QueryDefinition(
        //        "SELECT * FROM c WHERE c.documentType = @documentType " +
        //        "AND c.healthState = @healthState " +
        //        "ORDER BY c.currentAgentCount ASC")
        //        .WithParameter("@documentType", "WorkerInstance")
        //        .WithParameter("@healthState", 0);

        //using FeedIterator<WorkerInstanceDocument> resultSet =
        //    cosmosClient.GetContainer<WorkerInstanceDocument>(databaseName).GetItemQueryIterator<WorkerInstanceDocument>(query);

        var workerHearbeatThreshold = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(10));

        var query = cosmosClient.GetContainer<WorkerInstanceDocument>(databaseName)
            .GetItemLinqQueryable<WorkerInstanceDocument>()
            .Where(i => i.DocumentType == "WorkerInstance" && i.HealthState == WorkerInstanceHealthState.Ready && i.LastHeartbeat > workerHearbeatThreshold)
            .OrderBy(i => i.CurrentAgentCount);

        using var resultSet = query.ToFeedIterator();

        List<WorkerInstance> returnResult = [];

        while (resultSet.HasMoreResults)
        {
            FeedResponse<WorkerInstanceDocument> response = await resultSet.ReadNextAsync();
            returnResult.AddRange(response.Select(d => d.ToDomainModel()));
        }

        return returnResult;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateWorkerInstanceAsync(WorkerInstance instance)
    {
        try
        {
            await cosmosClient.GetContainer<WorkerInstanceDocument>(databaseName).ReplaceItemAsync(
                WorkerInstanceDocument.FromDomainModel(instance),
                instance.Id,
                new PartitionKey(instance.Id)
            );

            return true;
        }
        catch (CosmosException ex)
        {
            logger.LogInternalError(ex, "Cosmos exception occurred while updating worker instance {InstanceId}", instance.Id);

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UnregisterWorkerInstanceAsync(string instanceId)
    {
        try
        {
            await cosmosClient.GetContainer<WorkerInstanceDocument>(databaseName).DeleteItemAsync<WorkerInstanceDocument>(
                instanceId,
                new PartitionKey(instanceId)
            );

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return true;
        }
        catch (CosmosException ex)
        {
            logger.LogInternalError(ex, "Cosmos exception occurred while unregistering worker instance {InstanceId}", instanceId);

            return false;
        }
    }

    private async Task<WorkerInstanceDocument?> GetWorkerInstanceDocumentAsync(string instanceId)
    {
        try
        {
            return await GetDocumentAsync<WorkerInstanceDocument>(instanceId, instanceId);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    #endregion

    #region Instance Assignments
    /// <inheritdoc/>
    public async Task CreateAgentContextInstanceAssignmentAsync(AgentContextInstanceAssignment assignment)
    {
        try
        {
            AgentContextInstanceAssignmentDocument document = AgentContextInstanceAssignmentDocument.FromDomainModel(assignment);

            var response = await cosmosClient.GetContainer<AgentContextInstanceAssignmentDocument>(databaseName)
                .CreateItemAsync(document, new PartitionKey(document.PartitionKey));
        }
        catch (CosmosException e)
        {
            logger.LogInternalError(e, "Failed to create agent context instance assignment document for agent context {AgentContextId} and instance {InstanceId}",
                assignment.AgentContextId, assignment.InstanceId);

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task UpdateAgentContextInstanceAssignmentAsync(AgentContextInstanceAssignment assignment)
    {
        try
        {
            AgentContextInstanceAssignmentDocument document = AgentContextInstanceAssignmentDocument.FromDomainModel(assignment);

            var response = await cosmosClient.GetContainer<AgentContextInstanceAssignmentDocument>(databaseName)
                .UpsertItemAsync(document, new PartitionKey(document.PartitionKey));
        }
        catch (CosmosException e)
        {
            logger.LogInternalError(e, "Failed to update agent context instance assignment document for agent context {AgentContextId} and instance {InstanceId}",
                assignment.AgentContextId, assignment.InstanceId);

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAgentContextInstanceAssignmentAsync(string agentContextId, string instanceId)
    {
        try
        {
            string id = AgentContextInstanceAssignmentDocument.GenerateId(agentContextId, instanceId);

            await cosmosClient.GetContainer<AgentContextInstanceAssignmentDocument>(databaseName)
                .DeleteItemAsync<AgentContextInstanceAssignmentDocument>(id, new PartitionKey(instanceId));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }
        catch (CosmosException ex)
        {
            logger.LogInternalError(ex, "Failed to delete agent context instance assignment for agent context {AgentContextId}, instance id {InstanceId}", agentContextId, instanceId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<AgentContextInstanceAssignment>> GetAssignmentsForInstanceAsync(string instanceId)
    {
        var query = cosmosClient.GetContainer<AgentContextInstanceAssignmentDocument>(databaseName)
            .GetItemLinqQueryable<AgentContextInstanceAssignmentDocument>()
            .Where(a => a.PartitionKey == instanceId) // assignment docs are partitioned by instance id
            .OrderBy(a => a.Expires);

        using var iterator = query.ToFeedIterator();

        List<AgentContextInstanceAssignment> result = [];

        while (iterator.HasMoreResults)
        {
            var assignments = await iterator.ReadNextAsync();

            result.AddRange(assignments.Select(a => a.ToDomainModel()));
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAllAssignmentsForThreadAsync(Guid threadId)
    {
        try
        {
            string threadIdStr = threadId.ToString();
            var container = cosmosClient.GetContainer<AgentContextInstanceAssignmentDocument>(databaseName);

            var query = container.GetItemLinqQueryable<AgentContextInstanceAssignmentDocument>()
                .Where(d => d.DocumentType == "AgentContextInstanceAssignment" && d.ThreadId == threadIdStr);

            using var iterator = query.ToFeedIterator();

            while (iterator.HasMoreResults)
            {
                foreach (var assignment in await iterator.ReadNextAsync())
                {
                    await container.DeleteItemAsync<AgentContextInstanceAssignmentDocument>(
                        assignment.Id,
                        new PartitionKey(assignment.PartitionKey)
                    );
                }
            }

            logger.LogInternalInformation("Successfully deleted all agent context instance assignments for thread {ThreadId}", threadId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to delete agent context instance assignments for thread {ThreadId}", threadId);
            return false;
        }
    }
    #endregion

    #region Helper Methods
    private async Task<T> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        ItemResponse<T> response = await cosmosClient.GetContainer<T>(databaseName).ReadItemAsync<T>(
            id,
            new PartitionKey(partitionKey)
        );

        return response.Resource;
    }

    private async Task<(T document, string etag)> GetDocumentWithEtagAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        ItemResponse<T> response = await cosmosClient.GetContainer<T>(databaseName).ReadItemAsync<T>(
            id,
            new PartitionKey(partitionKey)
        );

        return (response.Resource, response.ETag);
    }
    #endregion
}
