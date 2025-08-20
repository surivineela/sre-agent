// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Data.Helpers;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

public class CosmosDbAgentTasksRepository(
    CosmosClient cosmosClient,
    string databaseName,
    ILogger<CosmosDbAgentTasksRepository> logger
) : IAgentTasksRepository
{
    public async Task<AgentTask?> GetAgentTaskAsync(Guid threadId, Guid agentTaskId)
    {
        logger.LogInternalInformation("Trying to get agent task: {AgentTaskId} for thread: {ThreadId}", agentTaskId, threadId);

        try
        {
            string threadIdStr = threadId.ToString();
            string agentTaskIdStr = agentTaskId.ToString();

            AgentTaskDocument? agentTaskDoc = await GetDocumentAsync<AgentTaskDocument>(agentTaskIdStr, threadIdStr);

            if (agentTaskDoc == null)
            {
                logger.LogInternalInformation("Agent task not found: {AgentTaskId} for thread: {ThreadId}", agentTaskId, threadId);
                return null;
            }

            // Convert to domain model
            var agentTask = agentTaskDoc.ToDomainModel();

            logger.LogInternalInformation("Successfully retrieved agent task: {AgentTaskId} for thread: {ThreadId}", agentTaskId, threadId);
            return agentTask;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInternalInformation("Agent task not found: {AgentTaskId} for thread: {ThreadId}", agentTaskId, threadId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error retrieving agent task {AgentTaskId} for thread {ThreadId}", agentTaskId, threadId);
            throw;
        }
    }

    public async Task<AgentTask> CreateAgentTaskAsync(AgentTask agentTask)
    {
        logger.LogInternalInformation("Creating agent task: {AgentTaskId} for thread: {ThreadId}", agentTask.Id, agentTask.ThreadId);

        try
        {
            // Ensure ID is set
            if (agentTask.Id == Guid.Empty)
            {
                agentTask = agentTask with { Id = Guid.NewGuid() };
            }

            // Set LastModified to current time
            agentTask = agentTask with { LastModified = DateTime.UtcNow };

            string threadIdStr = agentTask.ThreadId.ToString();

            // Create the agent task document
            AgentTaskDocument agentTaskDoc = AgentTaskDocument.FromDomainModel(agentTask);

            ItemResponse<AgentTaskDocument> response = await cosmosClient.GetContainer<AgentTaskDocument>(databaseName).CreateItemAsync(
                agentTaskDoc,
                new PartitionKey(agentTaskDoc.PartitionKey)
            );

            logger.LogInternalInformation("Successfully created agent task: {AgentTaskId} for thread: {ThreadId}", agentTask.Id, agentTask.ThreadId);

            // Return the task with the timestamp we set
            return agentTask;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogInternalWarning("Agent task already exists: {AgentTaskId} for thread: {ThreadId}", agentTask.Id, agentTask.ThreadId);
            throw new InvalidOperationException($"Agent task with ID {agentTask.Id} already exists", ex);
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error creating agent task {AgentTaskId} for thread {ThreadId}", agentTask.Id, agentTask.ThreadId);
            throw;
        }
    }

    public async Task<AgentTask> UpdateAgentTaskAsync(AgentTask agentTask)
    {
        logger.LogInternalInformation("Updating agent task: {AgentTaskId} for thread: {ThreadId}", agentTask.Id, agentTask.ThreadId);

        try
        {
            // Ensure ID is set
            if (agentTask.Id == Guid.Empty)
            {
                throw new ArgumentException("Agent task ID cannot be empty for update operation", nameof(agentTask));
            }

            string threadIdStr = agentTask.ThreadId.ToString();
            string agentTaskIdStr = agentTask.Id.ToString();

            // Check if the agent task exists
            AgentTaskDocument? existingAgentTask = await GetDocumentAsync<AgentTaskDocument>(agentTaskIdStr, threadIdStr);
            if (existingAgentTask == null)
            {
                logger.LogInternalWarning("Cannot update agent task: Agent task {AgentTaskId} not found in thread {ThreadId}",
                    agentTaskIdStr, threadIdStr);
                throw new InvalidOperationException($"Agent task with ID {agentTask.Id} not found");
            }

            // Set LastModified to current time
            agentTask = agentTask with { LastModified = DateTime.UtcNow };

            // Create the updated agent task document
            AgentTaskDocument agentTaskDoc = AgentTaskDocument.FromDomainModel(agentTask);

            // Replace the existing document with the updated one
            ItemResponse<AgentTaskDocument> response = await cosmosClient.GetContainer<AgentTaskDocument>(databaseName).ReplaceItemAsync(
                agentTaskDoc,
                agentTaskIdStr,
                new PartitionKey(threadIdStr)
            );

            logger.LogInternalInformation("Successfully updated agent task {AgentTaskId} in thread {ThreadId}", agentTaskIdStr, threadIdStr);

            // Return the task with the timestamp we set
            return agentTask;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInternalWarning("Cannot update agent task: Agent task {AgentTaskId} not found in thread {ThreadId}",
                agentTask.Id, agentTask.ThreadId);
            throw new InvalidOperationException($"Agent task with ID {agentTask.Id} not found", ex);
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error updating agent task {AgentTaskId} in thread {ThreadId}", agentTask.Id, agentTask.ThreadId);
            throw;
        }
    }

    public async Task<bool> DeleteAgentTaskAsync(Guid threadId, Guid agentTaskId)
    {
        logger.LogInternalInformation("Deleting agent task: {AgentTaskId} for thread: {ThreadId}", agentTaskId, threadId);

        try
        {
            string threadIdStr = threadId.ToString();
            string agentTaskIdStr = agentTaskId.ToString();

            // Check if the agent task exists before attempting to delete
            AgentTaskDocument? existingAgentTask = await GetDocumentAsync<AgentTaskDocument>(agentTaskIdStr, threadIdStr);
            if (existingAgentTask == null)
            {
                logger.LogInternalInformation("Agent task not found for deletion: {AgentTaskId} for thread: {ThreadId}", agentTaskId, threadId);
                return false;
            }

            // Delete the agent task document
            await cosmosClient.GetContainer<AgentTaskDocument>(databaseName).DeleteItemAsync<AgentTaskDocument>(
                agentTaskIdStr,
                new PartitionKey(threadIdStr)
            );

            logger.LogInternalInformation("Successfully deleted agent task {AgentTaskId} from thread {ThreadId}", agentTaskId, threadId);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInternalInformation("Agent task not found for deletion: {AgentTaskId} for thread: {ThreadId}", agentTaskId, threadId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error deleting agent task {AgentTaskId} from thread {ThreadId}", agentTaskId, threadId);
            throw;
        }
    }

    public async Task<HypothesisDetails?> GetHypothesisDetailsAsync(Guid agentTaskId, Guid hypothesisId)
    {
        logger.LogInternalInformation("Trying to get hypothesis details: {HypothesisId} for agent task: {AgentTaskId}", hypothesisId, agentTaskId);

        try
        {
            string agentTaskIdStr = agentTaskId.ToString();
            string hypothesisIdStr = hypothesisId.ToString();

            HypothesisDetailsDocument? hypothesisDetailsDoc = await GetDocumentAsync<HypothesisDetailsDocument>(hypothesisIdStr, agentTaskIdStr);

            if (hypothesisDetailsDoc == null)
            {
                logger.LogInternalInformation("Hypothesis details not found: {HypothesisId} for agent task: {AgentTaskId}", hypothesisId, agentTaskId);
                return null;
            }

            // Convert to domain model
            var hypothesisDetails = hypothesisDetailsDoc.ToDomainModel();

            logger.LogInternalInformation("Successfully retrieved hypothesis details: {HypothesisId} for agent task: {AgentTaskId}", hypothesisId, agentTaskId);
            return hypothesisDetails;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInternalInformation("Hypothesis details not found: {HypothesisId} for agent task: {AgentTaskId}", hypothesisId, agentTaskId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error retrieving hypothesis details {HypothesisId} for agent task {AgentTaskId}", hypothesisId, agentTaskId);
            throw;
        }
    }

    public async Task<HypothesisDetails> CreateHypothesisDetailsAsync(HypothesisDetails hypothesisDetails)
    {
        logger.LogInternalInformation("Creating hypothesis details: {HypothesisId} for agent task: {AgentTaskId}", hypothesisDetails.Id, hypothesisDetails.AgentTaskId);

        try
        {
            // Ensure ID is set
            if (hypothesisDetails.Id == Guid.Empty)
            {
                hypothesisDetails = hypothesisDetails with { Id = Guid.NewGuid() };
            }

            string agentTaskIdStr = hypothesisDetails.AgentTaskId.ToString();

            // Create the hypothesis details document
            HypothesisDetailsDocument hypothesisDetailsDoc = HypothesisDetailsDocument.FromDomainModel(hypothesisDetails);

            ItemResponse<HypothesisDetailsDocument> response = await cosmosClient.GetContainer<HypothesisDetailsDocument>(databaseName).CreateItemAsync(
                hypothesisDetailsDoc,
                new PartitionKey(hypothesisDetailsDoc.PartitionKey)
            );

            logger.LogInternalInformation("Successfully created hypothesis details: {HypothesisId} for agent task: {AgentTaskId}", hypothesisDetails.Id, hypothesisDetails.AgentTaskId);
            return response.Resource.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogInternalWarning("Hypothesis details already exists: {HypothesisId} for agent task: {AgentTaskId}", hypothesisDetails.Id, hypothesisDetails.AgentTaskId);
            throw new InvalidOperationException($"Hypothesis details with ID {hypothesisDetails.Id} already exists", ex);
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error creating hypothesis details {HypothesisId} for agent task {AgentTaskId}", hypothesisDetails.Id, hypothesisDetails.AgentTaskId);
            throw;
        }
    }

    public async Task<HypothesisDetails> UpdateHypothesisDetailsAsync(HypothesisDetails hypothesisDetails)
    {
        logger.LogInternalInformation("Updating hypothesis details: {HypothesisId} for agent task: {AgentTaskId}", hypothesisDetails.Id, hypothesisDetails.AgentTaskId);

        try
        {
            // Ensure ID is set
            if (hypothesisDetails.Id == Guid.Empty)
            {
                throw new ArgumentException("Hypothesis details ID cannot be empty for update operation", nameof(hypothesisDetails));
            }

            string agentTaskIdStr = hypothesisDetails.AgentTaskId.ToString();
            string hypothesisIdStr = hypothesisDetails.Id.ToString();

            // Check if the hypothesis details exists
            HypothesisDetailsDocument? existingHypothesisDetails = await GetDocumentAsync<HypothesisDetailsDocument>(hypothesisIdStr, agentTaskIdStr);
            if (existingHypothesisDetails == null)
            {
                logger.LogInternalWarning("Cannot update hypothesis details: Hypothesis details {HypothesisId} not found in agent task {AgentTaskId}",
                    hypothesisIdStr, agentTaskIdStr);
                throw new InvalidOperationException($"Hypothesis details with ID {hypothesisDetails.Id} not found");
            }

            // Create the updated hypothesis details document
            HypothesisDetailsDocument hypothesisDetailsDoc = HypothesisDetailsDocument.FromDomainModel(hypothesisDetails);

            // Replace the existing document with the updated one
            ItemResponse<HypothesisDetailsDocument> response = await cosmosClient.GetContainer<HypothesisDetailsDocument>(databaseName).ReplaceItemAsync(
                hypothesisDetailsDoc,
                hypothesisIdStr,
                new PartitionKey(agentTaskIdStr)
            );

            logger.LogInternalInformation("Successfully updated hypothesis details {HypothesisId} in agent task {AgentTaskId}", hypothesisIdStr, agentTaskIdStr);
            return response.Resource.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInternalWarning("Cannot update hypothesis details: Hypothesis details {HypothesisId} not found in agent task {AgentTaskId}",
                hypothesisDetails.Id, hypothesisDetails.AgentTaskId);
            throw new InvalidOperationException($"Hypothesis details with ID {hypothesisDetails.Id} not found", ex);
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error updating hypothesis details {HypothesisId} in agent task {AgentTaskId}", hypothesisDetails.Id, hypothesisDetails.AgentTaskId);
            throw;
        }
    }

    public async Task<bool> DeleteHypothesisDetailsAsync(Guid agentTaskId, Guid hypothesisId)
    {
        logger.LogInternalInformation("Deleting hypothesis details: {HypothesisId} for agent task: {AgentTaskId}", hypothesisId, agentTaskId);

        try
        {
            string agentTaskIdStr = agentTaskId.ToString();
            string hypothesisIdStr = hypothesisId.ToString();

            // Check if the hypothesis details exists before attempting to delete
            HypothesisDetailsDocument? existingHypothesisDetails = await GetDocumentAsync<HypothesisDetailsDocument>(hypothesisIdStr, agentTaskIdStr);
            if (existingHypothesisDetails == null)
            {
                logger.LogInternalInformation("Hypothesis details not found for deletion: {HypothesisId} for agent task: {AgentTaskId}", hypothesisId, agentTaskId);
                return false;
            }

            // Delete the hypothesis details document
            await cosmosClient.GetContainer<HypothesisDetailsDocument>(databaseName).DeleteItemAsync<HypothesisDetailsDocument>(
                hypothesisIdStr,
                new PartitionKey(agentTaskIdStr)
            );

            logger.LogInternalInformation("Successfully deleted hypothesis details {HypothesisId} from agent task {AgentTaskId}", hypothesisId, agentTaskId);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInternalInformation("Hypothesis details not found for deletion: {HypothesisId} for agent task: {AgentTaskId}", hypothesisId, agentTaskId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error deleting hypothesis details {HypothesisId} from agent task {AgentTaskId}", hypothesisId, agentTaskId);
            throw;
        }
    }

    #region Helper Methods

    private async Task<T?> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            ItemResponse<T> response = await cosmosClient.GetContainer<T>(databaseName).ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    public async Task<List<AgentTask>> GetAgentTasksAsync(Guid threadId)
    {
        logger.LogInternalInformation("Listing all agent tasks for thread: {ThreadId}", threadId);
        try
        {
            // filter out context thread because these threads are not task threads and id is not guid
            string threadIdStr = threadId.ToString();
            var query = new QueryDefinition(@"
                SELECT * FROM c
                WHERE c.documentType = 'AgentTask' AND c.threadId = @threadId")
                .WithParameter("@threadId", threadIdStr);

            var iterator = cosmosClient.GetContainer<AgentTaskDocument>(databaseName).GetItemQueryIterator<AgentTaskDocument>(query);
            List<AgentTask> agentTasks = new List<AgentTask>();
            while (iterator.HasMoreResults)
            {
                foreach (var item in await iterator.ReadNextAsync())
                {
                    // Convert to domain model (LastModified will be preserved from when it was saved)
                    var agentTask = item.ToDomainModel();
                    agentTasks.Add(agentTask);
                }
            }
            logger.LogInternalInformation("Successfully listed {Count} agent tasks for thread: {ThreadId}", agentTasks.Count, threadId);
            return agentTasks;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error listing agent tasks for thread {ThreadId}", threadId);
            throw;
        }
    }

    #endregion
}
