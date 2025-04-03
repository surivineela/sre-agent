// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Net;
using Container = Microsoft.Azure.Cosmos.Container;
using Thread = Agent.Core.Models.Api.v1.Thread;
using Action = Agent.Core.Models.Api.v1.Action;
using Agent.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

// Rest of the file remains unchanged
public class CosmosDbThreadRepository : IThreadRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbThreadRepository> _logger;

    public CosmosDbThreadRepository(CosmosClient cosmosClient, string databaseName, string containerName, ILogger<CosmosDbThreadRepository> logger)
    {
        _container = cosmosClient.GetContainer(databaseName, containerName);
        _logger = logger;
    }

    #region Thread Operations

    public async Task<Thread> GetThreadAsync(Guid threadId)
    {
        _logger.LogInformation("Trying to get thread: {Id}", threadId);
        try
        {
            // First get the thread document
            string threadIdStr = threadId.ToString();
            ThreadDocument threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc == null)
            {
                _logger.LogInformation("Thread not found: {Id}", threadId);
                return null;
            }

            // Then get the start message
            MessageDocument startMessageDoc = await GetDocumentAsync<MessageDocument>(threadDoc.MessageId, threadIdStr);

            if (startMessageDoc == null)
            {
                _logger.LogInformation("Start message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadId);
                return null;
            }

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

            // Delete all teams conversation mapping in the thread
            var teamsQuery = _container.GetItemLinqQueryable<ThreadTeamsMappingDocument>()
                .Where(a => a.DocumentType == "ThreadTeamsMapping" && a.ThreadId == threadIdStr);

            using var teamsIterator = teamsQuery.ToFeedIterator();

            while (teamsIterator.HasMoreResults)
            {
                foreach (var teamsMapping in await teamsIterator.ReadNextAsync())
                {
                    await _container.DeleteItemAsync<ThreadTeamsMappingDocument>(
                        teamsMapping.Id,
                        new PartitionKey(teamsMapping.PartitionKey)
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

    public async Task<Thread> UpdateThreadTitleAsync(Guid threadId, string newTitle)
    {
        string threadIdStr = threadId.ToString();

        try
        {
            // Get the current thread document
            ThreadDocument threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc == null)
            {
                _logger.LogWarning("Cannot update title: Thread {ThreadId} not found", threadId);
                return null;
            }

            // Update the title and modified timestamp
            ThreadDocument updatedThreadDoc = threadDoc with
            {
                Title = newTitle,
                ModifiedTimestamp = DateTime.UtcNow
            };

            // Save the updated document
            var response = await _container.ReplaceItemAsync(
                updatedThreadDoc,
                updatedThreadDoc.Id,
                new PartitionKey(updatedThreadDoc.PartitionKey)
            );

            // Get the start message to construct the complete Thread domain model
            MessageDocument startMessageDoc = await GetDocumentAsync<MessageDocument>(
                threadDoc.MessageId,
                threadIdStr
            );

            if (startMessageDoc == null)
            {
                _logger.LogWarning("Start message {MessageId} not found for thread {ThreadId}",
                    threadDoc.MessageId, threadId);

                // Return a partial Thread model without the start message
                return new Thread(
                    Id: threadId,
                    Title: newTitle,
                    StartMessage: null,
                    CreatedTimestamp: threadDoc.CreatedTimestamp,
                    ModifiedTimestamp: updatedThreadDoc.ModifiedTimestamp
                );
            }

            // Return the complete updated Thread domain model
            _logger.LogInformation("Successfully updated title for thread {ThreadId}", threadId);
            return updatedThreadDoc.ToDomainModel(startMessageDoc.ToDomainModel());
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Cannot update title: Thread {ThreadId} not found", threadId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating title for thread {ThreadId}", threadId);
            throw;
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
        if (message.Posted == null)
            message = message with { Posted = new Posted(false) };

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
    public async Task<Action> GetActionAsync(Guid threadId, Guid actionId)
    {
        string threadIdStr = threadId.ToString();
        string actionIdStr = actionId.ToString();

        try
        {
            // Query for the specific action by its ID and thread ID
            var query = _container.GetItemLinqQueryable<ActionDocument>()
                .Where(a => a.DocumentType == "Action" &&
                      a.ThreadId == threadIdStr &&
                      a.Id == actionIdStr);

            using var iterator = query.ToFeedIterator();

            if (iterator.HasMoreResults)
            {
                var results = await iterator.ReadNextAsync();
                var actionDoc = results.FirstOrDefault();

                if (actionDoc != null)
                {
                    return actionDoc.ToDomainModel();
                }
            }

            // No action found with the given ID in the thread
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving action {ActionId} for thread {ThreadId}", actionId, threadId);
            throw;
        }
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

