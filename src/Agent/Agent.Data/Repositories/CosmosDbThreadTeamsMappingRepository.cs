using System.Net;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

public class CosmosDbThreadTeamsMappingRepository : IThreadTeamsMappingRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbThreadTeamsMappingRepository> _logger;
    public CosmosDbThreadTeamsMappingRepository(CosmosClient cosmosClient, ILogger<CosmosDbThreadTeamsMappingRepository> logger, string databaseName, string containerName)
    {
        _container = cosmosClient.GetContainer(databaseName, containerName);
        _logger = logger;
    }

    public async Task<ThreadTeamsMapping> GetMappingByThreadIdAsync(string threadId)
    {

        try
        {
            // First get the thread document
            ThreadTeamsMappingDocument threadDoc = await GetDocumentAsync<ThreadTeamsMappingDocument>($"teams_{threadId}", $"teams_{threadId}");

            if (threadDoc == null)
                return null;

            // Convert to domain model
            return threadDoc.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ThreadTeamsMapping> AddMappingAsync(ThreadTeamsMapping mapping)
    {
        if (string.IsNullOrEmpty(mapping.Id) || string.IsNullOrEmpty(mapping.ConversationId))
        {
            throw new ArgumentException("ThreadId and TeamsConversationId cannot be null or empty");
        }

        // Create or update the thread mapping
        ThreadTeamsMappingDocument threadDoc = ThreadTeamsMappingDocument.FromDomainModel(mapping);
        await _container.UpsertItemAsync(threadDoc, new PartitionKey(threadDoc.PartitionKey));

        return mapping;
    }

    public async Task<bool> RemoveThreadMappingAsync(string threadId)
    {
        try
        {
            // Delete the mapping document
            await _container.DeleteItemAsync<ThreadTeamsMappingDocument>(
                $"teams_{threadId}",
                new PartitionKey($"teams_{threadId}")
            );
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<ThreadTeamsMapping> GetMappingByConversationIdAsync(string conversationId)
    {
        if (string.IsNullOrEmpty(conversationId))
        {
            throw new ArgumentException("Conversation ID cannot be null or empty", nameof(conversationId));
        }

        try
        {
            QueryDefinition query = new QueryDefinition(
                "SELECT * FROM c WHERE c.documentType = @documentType AND c.conversationId = @conversationId")
                .WithParameter("@documentType", "ThreadTeamsMapping")
                .WithParameter("@conversationId", conversationId);

            _logger.LogInformation("GetMappingByConversationIdAsync Query: {0}", query.QueryText);
            using FeedIterator<ThreadTeamsMappingDocument> resultSet = _container.GetItemQueryIterator<ThreadTeamsMappingDocument>(
                query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 1 }
            );

            if (resultSet.HasMoreResults)
            {
                FeedResponse<ThreadTeamsMappingDocument> response = await resultSet.ReadNextAsync();
                ThreadTeamsMappingDocument document = response.FirstOrDefault();
                return document?.ToDomainModel();
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ThreadTeamsMapping> GetFirstOrDefaultChannel()
    {
        try
        {
            QueryDefinition query = new QueryDefinition(
                "SELECT * FROM c WHERE c.documentType = @documentType AND c.threadId != '' AND c.serviceUrl != '' AND c.channelId != ''")
                .WithParameter("@documentType", "ThreadTeamsMapping");

            _logger.LogInformation("GetFirstOrDefaultChannel Query: {0}", query.QueryText);
            using FeedIterator<ThreadTeamsMappingDocument> resultSet = _container.GetItemQueryIterator<ThreadTeamsMappingDocument>(
                query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 1 }
            );

            if (resultSet.HasMoreResults)
            {
                FeedResponse<ThreadTeamsMappingDocument> response = await resultSet.ReadNextAsync();
                ThreadTeamsMappingDocument document = response.FirstOrDefault();
                return document?.ToDomainModel();
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // TODO(jianbosun): currently it just return all the mappings, need to add filtering logic for "Active" state
    public async Task<IEnumerable<ThreadTeamsMapping>> ListActiveConversationsAsync()
    {
        try
        {
            QueryDefinition query = new QueryDefinition(
                "SELECT * FROM c WHERE c.documentType = @documentType AND c.threadId != '' AND c.conversationId != '' AND c.serviceUrl != ''")
                .WithParameter("@documentType", "ThreadTeamsMapping");

            _logger.LogInformation("ListActiveConversationsAsync Query: {0}", query.QueryText);
            List<ThreadTeamsMapping> mappings = new List<ThreadTeamsMapping>();
            using FeedIterator<ThreadTeamsMappingDocument> resultSet = _container.GetItemQueryIterator<ThreadTeamsMappingDocument>(query);

            while (resultSet.HasMoreResults)
            {
                FeedResponse<ThreadTeamsMappingDocument> response = await resultSet.ReadNextAsync();
                mappings.AddRange(response.Select(doc => doc.ToDomainModel()));
            }

            return mappings;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Enumerable.Empty<ThreadTeamsMapping>();
        }
    }

    public async Task<bool> AddPostedMessageAsync(string threadId, string messageId)
    {
        if (string.IsNullOrEmpty(threadId) || string.IsNullOrEmpty(messageId))
        {
            throw new ArgumentException("ThreadId and MessageId cannot be null or empty");
        }

        try
        {
            // Use a patch operation to add the message ID to the array without retrieving the entire document
            PatchItemRequestOptions options = new PatchItemRequestOptions { FilterPredicate = "FROM c WHERE c.id = @id" };

            await _container.PatchItemAsync<ThreadTeamsMappingDocument>(
                $"teams_{threadId}",
                new PartitionKey($"teams_{threadId}"),
                new[]
                { 
                    // Use "PostedMessages" with capital P to match the C# property name
                    PatchOperation.Add("/PostedMessages/-", messageId)
                },
                options);

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<bool> AddPostedMessagesAsync(string threadId, IEnumerable<string> messageIds)
    {
        var id = $"teams_{threadId}";
        if (string.IsNullOrEmpty(threadId) || messageIds == null || !messageIds.Any())
        {
            throw new ArgumentException("ThreadId cannot be null/empty and messageIds must contain items");
        }

        try
        {
            // Get the current document to update it
            ThreadTeamsMappingDocument document = await GetDocumentAsync<ThreadTeamsMappingDocument>(id, id);
            if (document == null)
                return false;

            // Initialize the list if it doesn't exist
            var postedMessages = document.PostedMessages ?? new List<string>();

            // Add the new message IDs
            foreach (var messageId in messageIds)
            {
                if (!postedMessages.Contains(messageId))
                {
                    postedMessages.Add(messageId);
                }
            }

            // Create a patched document with the updated list
            var updatedDoc = document with { PostedMessages = postedMessages };

            // Save the updated document
            await _container.ReplaceItemAsync(updatedDoc, id, new PartitionKey(id));
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<IList<string>> GetPostedMessagesAsync(string threadId)
    {
        var id = $"teams_{threadId}";
        if (string.IsNullOrEmpty(threadId))
        {
            throw new ArgumentException("ThreadId cannot be null or empty");
        }

        try
        {
            ThreadTeamsMappingDocument document = await GetDocumentAsync<ThreadTeamsMappingDocument>(id, id);
            return document?.PostedMessages ?? new List<string>();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return new List<string>();
        }
    }

    #region Helper Methods

    private async Task<T> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            ItemResponse<T> response = await _container.ReadItemAsync<T>(
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

    #endregion
}