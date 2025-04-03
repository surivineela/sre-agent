using System.Net;
using System.Text;
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
        if (mapping is null)
        {
            throw new ArgumentException("ThreadId and TeamsConversationId cannot be null or empty");
        }

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

    // Refactored to query unposted messages first, then get relevant thread mappings
    public async Task<IEnumerable<ThreadTeamsMapping>> ListActiveConversationsAsync()
    {
        try
        {
            // Step 1: Find messages from the last 5 minutes that haven't been posted to Teams yet
            DateTime cutoffTime = DateTime.UtcNow.AddMinutes(-5);

            // Query for messages not yet posted to teams
            QueryDefinition messageQuery = new QueryDefinition(
                @"SELECT DISTINCT c.threadId 
                  FROM c 
                  WHERE c.documentType = 'Message' 
                    AND c.author.role = 1
                    AND c.timeStamp >= @cutoffTime
                    AND IS_DEFINED(c.posted) AND c.posted.teams = false")
                .WithParameter("@cutoffTime", cutoffTime);

            _logger.LogDebug("Query for unposted messages: {0}", messageQuery.QueryText);

            // Get distinct thread IDs with unposted messages
            HashSet<string> threadsWithUnpostedMessages = new HashSet<string>();
            using FeedIterator<dynamic> messageIterator = _container.GetItemQueryIterator<dynamic>(messageQuery);

            while (messageIterator.HasMoreResults)
            {
                FeedResponse<dynamic> messageResponse = await messageIterator.ReadNextAsync();
                foreach (var item in messageResponse)
                {
                    string threadId = item.threadId.ToString();
                    threadsWithUnpostedMessages.Add(threadId);
                }
            }

            if (!threadsWithUnpostedMessages.Any())
            {
                _logger.LogDebug("No threads found with unposted messages in the last 30 minutes");
                return Enumerable.Empty<ThreadTeamsMapping>();
            }
            _logger.LogInformation("Found {0} threads with unposted messages", threadsWithUnpostedMessages.Count);

            // Step 2: Get thread mappings for only those threads that have unposted messages
            List<ThreadTeamsMapping> mappings = new List<ThreadTeamsMapping>();

            // For optimal performance with potentially large sets of thread IDs, 
            // process them in batches with IN operator
            const int batchSize = 50; // Cosmos DB query has limits on parameter size

            for (int i = 0; i < threadsWithUnpostedMessages.Count; i += batchSize)
            {
                // Take a batch of thread IDs
                string[] threadBatch = threadsWithUnpostedMessages
                    .Skip(i)
                    .Take(batchSize)
                    .ToArray();

                // Use a parameterized query with the IN operator
                StringBuilder queryBuilder = new StringBuilder(
                    "SELECT * FROM c WHERE c.documentType = @documentType " +
                    "AND c.threadId IN (");

                // Build the parameter list for the IN clause
                for (int j = 0; j < threadBatch.Length; j++)
                {
                    queryBuilder.Append($"@threadId{j}");
                    if (j < threadBatch.Length - 1)
                        queryBuilder.Append(", ");
                }
                queryBuilder.Append(") AND c.conversationId != '' AND c.serviceUrl != ''");

                QueryDefinition threadQuery = new QueryDefinition(queryBuilder.ToString())
                    .WithParameter("@documentType", "ThreadTeamsMapping");

                // Add the thread ID parameters
                for (int j = 0; j < threadBatch.Length; j++)
                {
                    threadQuery = threadQuery.WithParameter($"@threadId{j}", threadBatch[j]);

                    _logger.LogDebug("Thread {0} has unposted messages", threadBatch[j]);
                }

                _logger.LogDebug("Thread mapping query batch {0}: {1}", i / batchSize, threadQuery.QueryText);

                // Execute the query for this batch of thread IDs
                using FeedIterator<ThreadTeamsMappingDocument> threadIterator =
                    _container.GetItemQueryIterator<ThreadTeamsMappingDocument>(threadQuery);

                while (threadIterator.HasMoreResults)
                {
                    FeedResponse<ThreadTeamsMappingDocument> response = await threadIterator.ReadNextAsync();
                    mappings.AddRange(response.Select(doc => doc.ToDomainModel()));
                }
            }

            _logger.LogInformation("Found {0} active thread mappings for threads with unposted messages", mappings.Count);
            return mappings;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogError(ex, "Error querying for active conversations");
            return Enumerable.Empty<ThreadTeamsMapping>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ListActiveConversationsAsync");
            return Enumerable.Empty<ThreadTeamsMapping>();
        }
    }

    public async Task<bool> AddPostedMessagesAsync(string threadId, IEnumerable<string> messageIds)
    {
        if (string.IsNullOrEmpty(threadId) || messageIds == null || !messageIds.Any())
        {
            throw new ArgumentException("ThreadId cannot be null/empty and messageIds must contain items");
        }

        try
        {
            TransactionalBatch batch = _container.CreateTransactionalBatch(new PartitionKey(threadId));

            foreach (var messageId in messageIds)
            {
                batch.PatchItem(messageId, new[] { PatchOperation.Add("/posted/teams", true) });
            }

            TransactionalBatchResponse response = await batch.ExecuteAsync();

            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
            {
                _logger.LogError("Failed to patch items in batch for thread {ThreadId}", threadId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating posted messages for thread {ThreadId}", threadId);
            return false;
        }
    }

    public async Task<IList<string>> GetPostedMessagesAsync(string threadId)
    {
        if (string.IsNullOrEmpty(threadId))
        {
            throw new ArgumentException("ThreadId cannot be null or empty");
        }

        try
        {
            // Query for messages that are marked as posted to Teams
            QueryDefinition query = new QueryDefinition(
                "SELECT c.id FROM c WHERE c.documentType = @documentType AND c.threadId = @threadId AND IS_DEFINED(c.Posted) AND c.Posted.Teams = true")
                .WithParameter("@documentType", "Message")
                .WithParameter("@threadId", threadId);

            _logger.LogDebug("GetPostedMessagesAsync Query: {0}", query.QueryText);
            List<string> postedMessageIds = new List<string>();
            using FeedIterator<dynamic> resultSet = _container.GetItemQueryIterator<dynamic>(query);

            while (resultSet.HasMoreResults)
            {
                FeedResponse<dynamic> response = await resultSet.ReadNextAsync();
                foreach (var item in response)
                {
                    postedMessageIds.Add(item.id.ToString());
                }
            }

            return postedMessageIds;
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