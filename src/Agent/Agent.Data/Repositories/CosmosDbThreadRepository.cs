using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Net;
using Container = Microsoft.Azure.Cosmos.Container;
using Thread = Agent.Core.Models.Api.v1.Thread;
using Action = Agent.Core.Models.Api.v1.Action;
using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;

namespace Agent.Data.Repositories;

// Rest of the file remains unchanged
public class CosmosDbThreadRepository : IThreadRepository
{
    private readonly Container _container;

    public CosmosDbThreadRepository(CosmosClient cosmosClient, string databaseName, string containerName)
    {
        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    #region Thread Operations

    public async Task<Thread> GetThreadAsync(Guid threadId)
    {
        try
        {
            // First get the thread document
            string threadIdStr = threadId.ToString();
            ThreadDocument threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc == null)
                return null;

            // Then get the start message
            MessageDocument startMessageDoc = await GetDocumentAsync<MessageDocument>(threadDoc.MessageId, threadIdStr);

            if (startMessageDoc == null)
                return null;

            // Convert to domain model
            return threadDoc.ToDomainModel(startMessageDoc.ToDomainModel());
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Thread>> GetThreadsAsync(string? filter = null, int? skip = null, int? take = null)
    {
        var threads = new List<Thread>();

        // Query for thread documents
        var query = _container.GetItemLinqQueryable<ThreadDocument>()
            .Where(t => t.DocumentType == "Thread");

        // Apply OData filters here if needed
        // This is a simplified example without full OData support

        if (skip.HasValue)
            query = query.Skip(skip.Value);

        if (take.HasValue)
            query = query.Take(take.Value);

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var threadDoc in await iterator.ReadNextAsync())
            {
                // Get the start message for each thread
                MessageDocument startMessageDoc = await GetDocumentAsync<MessageDocument>(
                    threadDoc.MessageId,
                    threadDoc.Id
                );

                if (startMessageDoc != null)
                {
                    threads.Add(threadDoc.ToDomainModel(startMessageDoc.ToDomainModel()));
                }
            }
        }

        return threads;
    }

    public async Task<Thread> CreateThreadAsync(Thread thread)
    {
        // Ensure IDs are set
        if (thread.Id == Guid.Empty)
            thread = thread with { Id = Guid.NewGuid() };

        if (thread.StartMessage.Id == Guid.Empty)
            thread = thread with
            {
                StartMessage = thread.StartMessage with { Id = Guid.NewGuid() }
            };

        // Create the message first
        string threadIdStr = thread.Id.ToString();
        MessageDocument messageDoc = MessageDocument.FromDomainModel(thread.StartMessage, threadIdStr);
        await _container.CreateItemAsync(messageDoc, new PartitionKey(messageDoc.PartitionKey));

        // Then create the thread
        ThreadDocument threadDoc = ThreadDocument.FromDomainModel(thread);
        await _container.CreateItemAsync(threadDoc, new PartitionKey(threadDoc.PartitionKey));

        return thread;
    }

    public async Task<bool> DeleteThreadAsync(Guid threadId)
    {
        string threadIdStr = threadId.ToString();

        try
        {
            // Delete all messages in the thread first
            var messagesQuery = _container.GetItemLinqQueryable<MessageDocument>()
                .Where(m => m.DocumentType == "Message" && m.ThreadId == threadIdStr);

            using var iterator = messagesQuery.ToFeedIterator();

            while (iterator.HasMoreResults)
            {
                foreach (var message in await iterator.ReadNextAsync())
                {
                    await _container.DeleteItemAsync<MessageDocument>(
                        message.Id,
                        new PartitionKey(message.PartitionKey)
                    );
                }
            }

            // Delete all actions in the thread
            var actionsQuery = _container.GetItemLinqQueryable<ActionDocument>()
                .Where(a => a.DocumentType == "Action" && a.ThreadId == threadIdStr);

            using var actionIterator = actionsQuery.ToFeedIterator();

            while (actionIterator.HasMoreResults)
            {
                foreach (var action in await actionIterator.ReadNextAsync())
                {
                    await _container.DeleteItemAsync<ActionDocument>(
                        action.Id,
                        new PartitionKey(action.PartitionKey)
                    );
                }
            }

            // Finally delete the thread
            await _container.DeleteItemAsync<ThreadDocument>(
                threadIdStr,
                new PartitionKey(threadIdStr)
            );

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    #endregion

    #region Message Operations

    public async Task<Message> GetMessageAsync(Guid threadId, Guid messageId)
    {
        try
        {
            string threadIdStr = threadId.ToString();
            string messageIdStr = messageId.ToString();

            MessageDocument messageDoc = await GetDocumentAsync<MessageDocument>(messageIdStr, threadIdStr);

            return messageDoc?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Message>> GetMessagesAsync(Guid threadId, string filter = null, int? skip = null, int? take = null)
    {
        var messages = new List<Message>();
        string threadIdStr = threadId.ToString();

        var query = _container.GetItemLinqQueryable<MessageDocument>()
            .Where(m => m.DocumentType == "Message" && m.ThreadId == threadIdStr)
            .OrderBy(m => m.TimeStamp);

        // Apply OData filters here if needed

        if (skip.HasValue)
            query = (IOrderedQueryable<MessageDocument>)query.Skip(skip.Value);

        if (take.HasValue)
            query = (IOrderedQueryable<MessageDocument>)query.Take(take.Value);

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var messageDoc in await iterator.ReadNextAsync())
            {
                messages.Add(messageDoc.ToDomainModel());
            }
        }

        return messages;
    }

    public async Task<Message> AddMessageAsync(Guid threadId, Message message)
    {
        // Ensure ID is set
        if (message.Id == Guid.Empty)
            message = message with { Id = Guid.NewGuid() };

        string threadIdStr = threadId.ToString();

        // Create the message document
        MessageDocument messageDoc = MessageDocument.FromDomainModel(message, threadIdStr);
        await _container.CreateItemAsync(messageDoc, new PartitionKey(messageDoc.PartitionKey));

        // Update the thread's modified timestamp
        try
        {
            ThreadDocument threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);
            if (threadDoc != null)
            {
                ThreadDocument updatedThreadDoc = threadDoc with { ModifiedTimestamp = DateTime.UtcNow };
                await _container.ReplaceItemAsync(
                    updatedThreadDoc,
                    updatedThreadDoc.Id,
                    new PartitionKey(updatedThreadDoc.PartitionKey)
                );
            }
        }
        catch (CosmosException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
        {
            // Log the error but don't fail the operation
            Console.WriteLine($"Error updating thread timestamp: {ex.Message}");
        }

        return message;
    }

    public async Task<bool> DeleteMessageAsync(Guid threadId, Guid messageId)
    {
        string threadIdStr = threadId.ToString();
        string messageIdStr = messageId.ToString();

        try
        {
            // Check if this is a start message
            ThreadDocument threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);
            if (threadDoc != null && threadDoc.MessageId == messageIdStr)
            {
                // Can't delete start message without deleting thread
                return false;
            }

            // Delete the message
            await _container.DeleteItemAsync<MessageDocument>(
                messageIdStr,
                new PartitionKey(threadIdStr)
            );

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    #endregion

    #region Action Operations

    public async Task<IEnumerable<Action>> GetActionsAsync(Guid threadId, int? skip = null, int? take = null)
    {
        var actions = new List<Action>();
        string threadIdStr = threadId.ToString();

        var query = _container.GetItemLinqQueryable<ActionDocument>()
            .Where(a => a.DocumentType == "Action" && a.ThreadId == threadIdStr)
            .OrderByDescending(a => a.TimeStamp);

        if (skip.HasValue)
            query = (IOrderedQueryable<ActionDocument>)query.Skip(skip.Value);

        if (take.HasValue)
            query = (IOrderedQueryable<ActionDocument>)query.Take(take.Value);

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var actionDoc in await iterator.ReadNextAsync())
            {
                actions.Add(actionDoc.ToDomainModel());
            }
        }
        return actions;
    }

    public async Task<Action> AddActionAsync(Guid threadId, Action action)
    {
        // Ensure ID is set
        if (action.Id == Guid.Empty)
            action = action with { Id = Guid.NewGuid() };

        string threadIdStr = threadId.ToString();

        // Create the action document
        ActionDocument actionDoc = ActionDocument.FromDomainModel(action, threadIdStr);
        await _container.CreateItemAsync(actionDoc, new PartitionKey(actionDoc.PartitionKey));

        return action;
    }

    #endregion

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
