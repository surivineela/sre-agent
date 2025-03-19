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

            _logger.LogDebug("GetMappingByConversationIdAsync Query: {0}", query.QueryText);
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

            _logger.LogDebug("GetFirstOrDefaultChannel Query: {0}", query.QueryText);
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

    // Instead of querying message counts one by one, we'll get all counts in one query
    public async Task<IEnumerable<ThreadTeamsMapping>> ListActiveConversationsAsync()
    {
        try
        {
            // Get all active thread mappings first
            QueryDefinition query = new QueryDefinition(
                "SELECT * FROM c WHERE c.documentType = @documentType AND c.threadId != '' AND c.conversationId != '' AND c.serviceUrl != ''")
                .WithParameter("@documentType", "ThreadTeamsMapping");

            _logger.LogDebug("ListActiveConversationsAsync Query: {0}", query.QueryText);
            List<ThreadTeamsMapping> allMappings = new List<ThreadTeamsMapping>();
            using FeedIterator<ThreadTeamsMappingDocument> resultSet = _container.GetItemQueryIterator<ThreadTeamsMappingDocument>(query);

            while (resultSet.HasMoreResults)
            {
                FeedResponse<ThreadTeamsMappingDocument> response = await resultSet.ReadNextAsync();
                allMappings.AddRange(response.Select(doc => doc.ToDomainModel()));
            }

            if (!allMappings.Any())
            {
                return Enumerable.Empty<ThreadTeamsMapping>();
            }

            // Query for all message counts in a single operation, author.role = 1 indicates a bot
            QueryDefinition countQuery = new QueryDefinition(
                "SELECT c.threadId, COUNT(1) as messageCount FROM c WHERE c.documentType = 'Message' AND c.author.role = 1 GROUP BY c.threadId");

            _logger.LogDebug("Message counts query: {0}", countQuery.QueryText);

            Dictionary<string, int> messageCounts = new Dictionary<string, int>();
            using FeedIterator<dynamic> countIterator = _container.GetItemQueryIterator<dynamic>(countQuery);

            while (countIterator.HasMoreResults)
            {
                FeedResponse<dynamic> countResponse = await countIterator.ReadNextAsync();
                foreach (var item in countResponse)
                {
                    string threadId = item.threadId.ToString();
                    int count = (int)item.messageCount;
                    messageCounts[threadId] = count;
                }
            }

            // Find mappings with unposted messages by joining the results in memory
            List<ThreadTeamsMapping> mappingsWithUnpostedMessages = new List<ThreadTeamsMapping>();

            foreach (var mapping in allMappings)
            {
                // Get message count for this thread, or 0 if not found
                int totalMessages = messageCounts.TryGetValue(mapping.ThreadId, out int count) ? count : 0;
                int postedMessages = mapping.PostedMessages?.Count ?? 0;

                // If there are more messages than have been posted, this thread has unposted messages
                if (totalMessages > postedMessages)
                {
                    _logger.LogInformation("Thread {0} has unposted messages: {1} total, {2} posted",
                        mapping.ThreadId, totalMessages, postedMessages);
                    mappingsWithUnpostedMessages.Add(mapping);
                }
            }

            return mappingsWithUnpostedMessages;
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
            _logger.LogError(ex, "Failed to add posted messages for thread {ThreadId}", threadId);
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