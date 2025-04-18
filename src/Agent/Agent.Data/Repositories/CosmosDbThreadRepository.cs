// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Threading;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Action = Agent.Core.Models.Api.v1.Action;
using Container = Microsoft.Azure.Cosmos.Container;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Data.Repositories;

// Rest of the file remains unchanged
public class CosmosDbThreadRepository : IThreadRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosDbThreadRepository> _logger;

    // Set EnsureStableOrdering to false to avoid odata overwrites `order by` clause.
    // Because when odata sees `top` and `skip` but `orderby` is not defined, it will implicitly overwrite existing `order by` to `order by id asc`
    // to keep the order stable.
    // We disable this to allow using our own `order by` clause for pagination.
    // If `order by` is defined in odata query, it will overwrite our own `order by` clause.
    // For example, to get oldest 10 threads, a client can call `/api/v1/threads/{threadid}/messages?top=10` to get the oldest 10 threads, because the default order is `order by timeStamp asc`.
    // For example, to get latest 10 threads, a client can call `/api/v1/threads/{threadid}/messages?top=10&orderby=timeStamp+desc` to get the latest 10 threads.
    // For pagination, we can use `top` and `skip` to get the next page of threads.
    private static readonly ODataQuerySettings oDataQuerySettings = new(){ EnsureStableOrdering = false };

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

            // last message may be null if thread was created before we started saving last message id
            // & a new message has not been added to the thread
            Message lastMessageDocDomainModel;
            if (threadDoc.LastMessageId == null)
            {
                _logger.LogInformation("last message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadDoc.Id);
                lastMessageDocDomainModel = null;
            }
            else
            {
                MessageDocument lastMessageDoc = await GetDocumentAsync<MessageDocument>(
                    threadDoc.LastMessageId,
                    threadDoc.Id
                );
                lastMessageDocDomainModel = lastMessageDoc == null ? null : lastMessageDoc.ToDomainModel();
            }

            // Convert to domain model
            return threadDoc.ToDomainModel(startMessageDoc.ToDomainModel(), lastMessageDocDomainModel);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Thread>> GetThreadsBySourceAsync(ODataQueryOptions? queryOptins, ThreadSource? source = null)
    {
        var threads = new List<Thread>();
        // Query for thread documents
        var query = _container.GetItemLinqQueryable<ThreadDocument>()
            .Where(t => t.DocumentType == "Thread");

        // Add filter by ThreadSource if specified
        if (source.HasValue)
        {
            query = query.Where(t => t.Source == source.Value);
        }

        // Sort by creation timestamp
        query = query.OrderBy(t => t.CreatedTimestamp);

        if (queryOptins is not null)
        {
            query = queryOptins.ApplyTo(query, oDataQuerySettings) as IOrderedQueryable<ThreadDocument>;
        }

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
                // last message may be null if thread was created before we started saving last message id
                // & a new message has not been added to the thread
                Message lastMessageDocDomainModel;
                if (threadDoc.LastMessageId == null)
                {
                    _logger.LogInformation("last message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadDoc.Id);
                    lastMessageDocDomainModel = null;
                }
                else
                {
                    MessageDocument lastMessageDoc = await GetDocumentAsync<MessageDocument>(
                        threadDoc.LastMessageId,
                        threadDoc.Id
                    );
                    lastMessageDocDomainModel = lastMessageDoc == null ? null : lastMessageDoc.ToDomainModel();
                }
                if (startMessageDoc != null)
                {
                    threads.Add(threadDoc.ToDomainModel(startMessageDoc.ToDomainModel(), lastMessageDocDomainModel));
                }
            }
        }
        return threads;
    }

    public async Task<IEnumerable<Thread>> GetThreadsAsync(ODataQueryOptions? queryOptins)
    {
        var threads = new List<Thread>();

        // Query for thread documents
        var query = _container.GetItemLinqQueryable<ThreadDocument>()
            .Where(t => t.DocumentType == "Thread")
            .OrderBy(t => t.CreatedTimestamp);

        if (queryOptins is not null)
        {
            query = queryOptins.ApplyTo(query, oDataQuerySettings) as IOrderedQueryable<ThreadDocument>;
        }

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

                // last message may be null if thread was created before we started saving last message id
                // & a new message has not been added to the thread
                Message lastMessageDocDomainModel;
                if (threadDoc.LastMessageId == null)
                {
                    _logger.LogInformation("last message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadDoc.Id);
                    lastMessageDocDomainModel = null;
                }
                else
                {
                    MessageDocument lastMessageDoc = await GetDocumentAsync<MessageDocument>(
                        threadDoc.LastMessageId,
                        threadDoc.Id
                    );
                    lastMessageDocDomainModel = lastMessageDoc == null ? null : lastMessageDoc.ToDomainModel();
                }

                if (startMessageDoc != null)
                {
                    threads.Add(threadDoc.ToDomainModel(startMessageDoc.ToDomainModel(), lastMessageDocDomainModel));
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

        if (thread.LastMessage.Id == Guid.Empty)
            thread = thread with
            {
                LastMessage = thread.LastMessage with { Id = Guid.NewGuid() }
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

            // Get the last message to construct the complete Thread domain model

            // last message may be null if thread was created before we started saving last message id
            // & a new message has not been added to the thread
            Message lastMessageDocDomainModel;
            if (threadDoc.LastMessageId == null)
            {
                _logger.LogInformation("last message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadDoc.Id);
                lastMessageDocDomainModel = null;
            }
            else
            {
                MessageDocument lastMessageDoc = await GetDocumentAsync<MessageDocument>(
                    threadDoc.LastMessageId,
                    threadDoc.Id
                );
                lastMessageDocDomainModel = lastMessageDoc == null ? null : lastMessageDoc.ToDomainModel();
            }

            if (startMessageDoc == null)
            {
                _logger.LogWarning("Start message {MessageId} not found for thread {ThreadId}",
                    threadDoc.MessageId, threadId);

                // Return a partial Thread model without the start message
                return new Thread(
                    Id: threadId,
                    Title: newTitle,
                    StartMessage: null,
                    LastMessage: lastMessageDocDomainModel,
                    CreatedTimestamp: threadDoc.CreatedTimestamp,
                    ModifiedTimestamp: updatedThreadDoc.ModifiedTimestamp
                );
            }

            // Return the complete updated Thread domain model
            _logger.LogInformation("Successfully updated title for thread {ThreadId}", threadId);
            return updatedThreadDoc.ToDomainModel(startMessageDoc.ToDomainModel(), lastMessageDocDomainModel);
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

    public async Task<IEnumerable<Message>> GetMessagesAsync(Guid threadId, ODataQueryOptions? queryOptions)
    {
        var messages = new List<Message>();
        string threadIdStr = threadId.ToString();

        var query = _container.GetItemLinqQueryable<MessageDocument>()
            .Where(m => m.DocumentType == "Message" && m.ThreadId == threadIdStr)
            .OrderBy(m => m.TimeStamp);

        _logger.LogDebug("Query text before odata ApplyTo: {QueryText}", query.ToQueryDefinition().QueryText); // Log the query text
        if (queryOptions is not null)
        {
            query = queryOptions.ApplyTo(query, oDataQuerySettings) as IOrderedQueryable<MessageDocument>;
        }

        _logger.LogDebug("Query text after odata ApplyTo: {QueryText}", query.ToQueryDefinition().QueryText); // Log the query text

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

        // Update the threads latest message
        try
        {
            ThreadDocument threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);
            if (threadDoc != null)
            {
                ThreadDocument updatedThreadDoc = threadDoc with { LastMessageId = message.Id.ToString() };
                await _container.ReplaceItemAsync(
                    updatedThreadDoc,
                    updatedThreadDoc.Id,
                    new PartitionKey(updatedThreadDoc.PartitionKey)
                );
            }
        }
        catch
        {
            // Log the error but don't fail the operation
            Console.WriteLine($"Error updating thread latest message: {message.Id}");
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

    #region ThreadContext Operations
    public async Task<ThreadContext> GetThreadContextAsync(Guid threadId)
    {
        try
        {
            string threadIdStr = threadId.ToString();
            var threadContextDocId = ThreadContextDocument.GetId(threadIdStr);

            ThreadContextDocument threadContextDoc = await GetDocumentAsync<ThreadContextDocument>(threadContextDocId, threadContextDocId);

            return threadContextDoc?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<ThreadContext>> GetThreadContextsAsync(ODataQueryOptions? queryOptions)
    {
        var threads = new List<ThreadContext>();

        // Query for thread documents
        var query = _container.GetItemLinqQueryable<ThreadContextDocument>()
            .Where(t => t.DocumentType == "ThreadContext")
            .OrderBy(t => t.Id); // to support pagination the order must be stable. There's no timestamp in the thread context document

        if (queryOptions is not null)
        {
            query = queryOptions.ApplyTo(query, oDataQuerySettings) as IOrderedQueryable<ThreadContextDocument>;
        }

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var threadContextDoc in await iterator.ReadNextAsync())
            {
                threads.Add(threadContextDoc.ToDomainModel());
            }
        }

        return threads;
    }

    public async Task<ThreadContext> AddThreadContextAsync(ThreadContext threadContext)
    {
        // Ensure IDs are set
        if (threadContext.ThreadId == Guid.Empty)
            threadContext = new ThreadContext(Guid.NewGuid(), threadContext.AgentTypeEnum);

        // Then create the thread
        ThreadContextDocument threadContextDoc = ThreadContextDocument.FromDomainModel(threadContext);
        await _container.CreateItemAsync(threadContextDoc, new PartitionKey(threadContextDoc.PartitionKey));

        return threadContext;
    }

    public async Task<ThreadContext> UpdateThreadContextAsync(ThreadContext threadContext)
    {
        // Ensure IDs are set
        if (threadContext.ThreadId == Guid.Empty)
            return null;

        // Then create the thread
        ThreadContextDocument threadContextDoc = ThreadContextDocument.FromDomainModel(threadContext);
        await _container.UpsertItemAsync(threadContextDoc, new PartitionKey(threadContextDoc.PartitionKey));

        return threadContext;
    }

    public async Task<bool> DeleteThreadContextAsync(Guid threadId)
    {
        string threadIdStr = threadId.ToString();
        var threadContextDocId = ThreadContextDocument.GetId(threadIdStr);


        try
        {
            // Finally delete the thread
            await _container.DeleteItemAsync<ThreadDocument>(
                threadContextDocId,
                new PartitionKey(threadContextDocId)
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

    public async Task<IEnumerable<Action>> GetActionsAsync(Guid threadId, ODataQueryOptions? queryOptions)
    {
        var actions = new List<Action>();
        string threadIdStr = threadId.ToString();

        var query = _container.GetItemLinqQueryable<ActionDocument>()
            .Where(a => a.DocumentType == "Action" && a.ThreadId == threadIdStr)
            .OrderByDescending(a => a.TimeStamp);

        if (queryOptions is not null)
        {
            query = queryOptions.ApplyTo(query, oDataQuerySettings) as IOrderedQueryable<ActionDocument>;
        }

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

    #region Message Operations

    public async Task<MessageFeedback> GetMessageFeedbackAsync(Guid threadId, Guid messageFeedbackId)
    {
        try
        {
            string threadIdStr = threadId.ToString();
            string messageFeedbackIdStr = messageFeedbackId.ToString();

            MessageFeedbackDocument messageFeedbackDoc = await GetDocumentAsync<MessageFeedbackDocument>(messageFeedbackIdStr, threadIdStr);

            return messageFeedbackDoc?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<MessageFeedback>> GetMessageFeedbacksAsync(Guid threadId, ODataQueryOptions? queryOptions)
    {
        var messageFeedbacks = new List<MessageFeedback>();
        string threadIdStr = threadId.ToString();

        var query = _container.GetItemLinqQueryable<MessageFeedbackDocument>()
            .Where(m => m.DocumentType == "MessageFeedback" && m.ThreadId == threadIdStr)
            .OrderBy(m => m.TimeStamp);

        if (queryOptions is not null)
        {
            query = queryOptions.ApplyTo(query, oDataQuerySettings) as IOrderedQueryable<MessageFeedbackDocument>;
        }

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var messageFeedbackDoc in await iterator.ReadNextAsync())
            {
                messageFeedbacks.Add(messageFeedbackDoc.ToDomainModel());
            }
        }

        return messageFeedbacks;
    }

    public async Task<MessageFeedback> GetMessageFeedbackNeedingRCAAsync()
    {
        var messageFeedbacks = new List<MessageFeedback>();

        var query = _container.GetItemLinqQueryable<MessageFeedbackDocument>()
            .Where(m => m.DocumentType == "MessageFeedback")
            .OrderBy(m => m.TimeStamp);

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var messageFeedbackDoc in await iterator.ReadNextAsync())
            {
                if (!string.IsNullOrEmpty(messageFeedbackDoc.RootCause))
                {
                    continue;
                }

                return messageFeedbackDoc.ToDomainModel();
            }
        }

        return null;
    }

    public async Task<MessageFeedback> AddOrUpdateMessageFeedbackAsync(Guid threadId, MessageFeedback messageFeedback)
    {
        // Ensure ID is set
        if (messageFeedback.Id == Guid.Empty)
            messageFeedback = messageFeedback with { Id = Guid.NewGuid() };

        string threadIdStr = threadId.ToString();

        // Create the message document
        MessageFeedbackDocument messageFeedbackDoc = MessageFeedbackDocument.FromDomainModel(messageFeedback, threadIdStr);
        await _container.UpsertItemAsync(messageFeedbackDoc, new PartitionKey(messageFeedbackDoc.PartitionKey));

        return messageFeedback;
    }

    public async Task<bool> DeleteMessageFeedbackAsync(Guid threadId, Guid messageFeedbackId)
    {
        string threadIdStr = threadId.ToString();
        string messageFeedbackIdStr = messageFeedbackId.ToString();

        try
        {
            // Delete the message
            await _container.DeleteItemAsync<MessageFeedbackDocument>(
                messageFeedbackIdStr,
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

    #region AgentContext Operations
    public async Task<AgentContext> GetAgentContextAsync(Guid agentContextId, Guid threadId)
    {
        try
        {
            string threadIdStr = threadId.ToString();
            string agentContextIdStr = agentContextId.ToString();

            AgentContextDocument agentContextDocument = await GetDocumentAsync<AgentContextDocument>(agentContextIdStr, threadIdStr);

            return agentContextDocument?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<AgentContext>> GetAgentContextsForThreadAsync(Guid threadId)
    {
        var agentContexts = new List<AgentContext>();
        string threadIdStr = threadId.ToString();
        var query = _container.GetItemLinqQueryable<AgentContextDocument>()
            .Where(m => m.DocumentType == "AgentContext" && m.ThreadId == threadIdStr);

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var agentContextDoc in await iterator.ReadNextAsync())
            {
                agentContexts.Add(agentContextDoc.ToDomainModel());
            }
        }
        return agentContexts;
    }

    public async Task<AgentContext> CreateAgentContextAsync(AgentContext agentContext)
    {
        // Ensure IDs are set
        if (agentContext.Id == Guid.Empty)
        {
            agentContext = agentContext with { Id = Guid.NewGuid() };
        }

        if (agentContext.ThreadId == Guid.Empty)
        {
            return null;
        }

        // Create the sub-agent thread document
        AgentContextDocument agentContextDoc = AgentContextDocument.FromDomainModel(agentContext);
        await _container.CreateItemAsync(agentContextDoc, new PartitionKey(agentContextDoc.PartitionKey));
        return agentContext;
    }

    public async Task<bool> DeleteAgentContextAsync(Guid agentContextId, Guid threadId)
    {
        string threadIdStr = threadId.ToString();
        string agentContextIdStr = agentContextId.ToString();

        try
        {
            // Delete the message
            await _container.DeleteItemAsync<AgentContextDocument>(
                agentContextIdStr,
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

    #region ReasoningMessage Operations
    public async Task<ReasoningMessage> GetReasoningMessageAsync(Guid reasoningMessageId, Guid agentContextId)
    {
        try
        {
            string agentContextIdStr = agentContextId.ToString();
            string reasoningMessageIdStr = reasoningMessageId.ToString();

            ReasoningMessageDocument reasoningMessageDocument = await GetDocumentAsync<ReasoningMessageDocument>(reasoningMessageIdStr, agentContextIdStr);

            return reasoningMessageDocument?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ReasoningMessage> CreateReasoningMessageAsync(ReasoningMessage reasoningMessage)
    {
        // Ensure IDs are set
        if (reasoningMessage.Id == Guid.Empty)
        {
            reasoningMessage = reasoningMessage with { Id = Guid.NewGuid() };
        }

        if (reasoningMessage.AgentContextId == Guid.Empty)
        {
            return null;
        }

        // Create the sub-agent thread document
        ReasoningMessageDocument reasoningMessageDoc = ReasoningMessageDocument.FromDomainModel(reasoningMessage);
        await _container.CreateItemAsync(reasoningMessageDoc, new PartitionKey(reasoningMessageDoc.PartitionKey));
        return reasoningMessage;
    }

    public async Task<bool> DeleteReasoningMessageAsync(Guid reasoningMessageId, Guid agentContextId)
    {
        string agentContextIdStr = agentContextId.ToString();
        string reasoningMessageIdStr = reasoningMessageId.ToString();

        try
        {
            // Delete the message
            await _container.DeleteItemAsync<ReasoningMessageDocument>(
                reasoningMessageIdStr,
                new PartitionKey(agentContextIdStr)
            );

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
    #endregion
}
