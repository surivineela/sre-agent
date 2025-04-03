// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Agent.Data.Repositories;

public class CosmosDbThreadOrchestrationMappingRepository : IThreadOrchestrationMappingRepository
{
    private readonly Container _container;

    public CosmosDbThreadOrchestrationMappingRepository(CosmosClient cosmosClient, string databaseName, string containerName)
    {
        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    public async Task<IEnumerable<ThreadOrchestrationMapping>> GetMappingsByThreadIdAsync(string threadId)
    {
        var mappings = new List<ThreadOrchestrationMapping>();

        // This needs to change if we decide to associate multiple orchestrations with a thread
        string compositeId = $"mapping_{threadId}";

        var query = _container.GetItemLinqQueryable<ThreadOrchestrationMappingDocument>()
            .Where(m => m.DocumentType == "ThreadOrchestrationMapping" && m.Id == compositeId);

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var mappingDoc in await iterator.ReadNextAsync())
            {
                mappings.Add(mappingDoc.ToDomainModel());
            }
        }

        return mappings;
    }

    public async Task<ThreadOrchestrationMapping> AddThreadMappingAsync(ThreadOrchestrationMapping mapping)
    {
        if (string.IsNullOrEmpty(mapping.ThreadId) || string.IsNullOrEmpty(mapping.OrchestrationInstanceId))
        {
            throw new ArgumentException("ThreadId and OrchestrationInstanceId cannot be null or empty");
        }

        try
        {
            // Generate the document ID based on threadId
            string documentId = $"mapping_{mapping.ThreadId}";

            // Check if document already exists
            try
            {
                // Try to read the existing document directly
                ItemResponse<ThreadOrchestrationMappingDocument> existingDoc =
                    await _container.ReadItemAsync<ThreadOrchestrationMappingDocument>(
                        documentId,
                        new PartitionKey(mapping.ThreadId));

                if (existingDoc.Resource != null)
                {
                    // Document exists - preserve its original creation timestamp
                    mapping = mapping with
                    {
                        CreatedTimestamp = existingDoc.Resource.CreatedTimestamp,
                        ModifiedTimestamp = DateTime.UtcNow
                    };
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Document doesn't exist, set creation time if not already set
                if (mapping.CreatedTimestamp == default)
                {
                    mapping = mapping with { CreatedTimestamp = DateTime.UtcNow };
                }

                mapping = mapping with { ModifiedTimestamp = DateTime.UtcNow };
            }

            // Create or update the document
            ThreadOrchestrationMappingDocument mappingDoc = ThreadOrchestrationMappingDocument.FromDomainModel(mapping);
            await _container.UpsertItemAsync(mappingDoc, new PartitionKey(mappingDoc.PartitionKey));

            return mapping;
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            throw new InvalidOperationException(
                $"Failed to add/update mapping for thread {mapping.ThreadId} to orchestration {mapping.OrchestrationInstanceId}",
                ex);
        }
    }

    public async Task<bool> RemoveThreadMappingAsync(string threadId)
    {
        try
        {
            // Query for all mappings with this thread ID
            var query = _container.GetItemLinqQueryable<ThreadOrchestrationMappingDocument>()
                .Where(m => m.DocumentType == "ThreadOrchestrationMapping" && m.ThreadId == threadId);

            using var iterator = query.ToFeedIterator();
            bool anyDeleted = false;

            string compositeId = $"mapping_{threadId}";

            while (iterator.HasMoreResults)
            {
                foreach (var mappingDoc in await iterator.ReadNextAsync())
                {
                    await _container.DeleteItemAsync<ThreadOrchestrationMappingDocument>(
                        compositeId,
                        new PartitionKey(mappingDoc.PartitionKey)
                    );
                    anyDeleted = true;
                }
            }

            return anyDeleted;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<bool> RemoveThreadMappingAsync(string threadId, string orchestrationInstanceId)
    {
        try
        {
            string compositeId = $"mapping_{threadId}";

            // Delete the mapping document
            await _container.DeleteItemAsync<ThreadOrchestrationMappingDocument>(
                compositeId,
                new PartitionKey(threadId)
            );
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<IEnumerable<ThreadOrchestrationMapping>> GetAllThreadMappingsAsync()
    {
        var mappings = new List<ThreadOrchestrationMapping>();

        var query = _container.GetItemLinqQueryable<ThreadOrchestrationMappingDocument>()
            .Where(m => m.DocumentType == "ThreadOrchestrationMapping");

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var mappingDoc in await iterator.ReadNextAsync())
            {
                mappings.Add(mappingDoc.ToDomainModel());
            }
        }

        return mappings;
    }
}
