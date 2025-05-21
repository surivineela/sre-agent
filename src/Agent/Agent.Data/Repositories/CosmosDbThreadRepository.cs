// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Data.Helpers;
using Agent.Logging;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Action = Agent.Core.Models.Api.v1.Action;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Data.Repositories;

// Rest of the file remains unchanged
public class CosmosDbThreadRepository : IThreadRepository
{
    private readonly ILogger<CosmosDbThreadRepository> _logger;

    private readonly string _databaseName;

    private readonly CosmosClient _client;

    // Set EnsureStableOrdering to false to avoid odata overwrites `order by` clause.
    // Because when odata sees `top` and `skip` but `orderby` is not defined, it will implicitly overwrite existing `order by` to `order by id asc`
    // to keep the order stable.
    // We disable this to allow using our own `order by` clause for pagination.
    // If `order by` is defined in odata query, it will overwrite our own `order by` clause.
    // For example, to get oldest 10 threads, a client can call `/api/v1/threads/{threadid}/messages?top=10` to get the oldest 10 threads, because the default order is `order by timeStamp asc`.
    // For example, to get latest 10 threads, a client can call `/api/v1/threads/{threadid}/messages?top=10&orderby=timeStamp+desc` to get the latest 10 threads.
    // For pagination, we can use `top` and `skip` to get the next page of threads.
    private static readonly ODataQuerySettings oDataQuerySettings = new() { EnsureStableOrdering = false, TimeZone = TimeZoneInfo.Utc };

    public CosmosDbThreadRepository(CosmosClient cosmosClient, string databaseName, ILogger<CosmosDbThreadRepository> logger)
    {
        _logger = logger;
        _databaseName = databaseName;
        _client = cosmosClient;
    }

    #region Thread Operations

    public async Task<Thread> GetThreadAsync(Guid threadId)
    {
        _logger.LogInternalInformation("Trying to get thread: {Id}", threadId);
        try
        {
            // First get the thread document
            string threadIdStr = threadId.ToString();
            ThreadDocument threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc == null)
            {
                _logger.LogInternalInformation("Thread not found: {Id}", threadId);
                return null;
            }

            // Then get the start message
            MessageDocument startMessageDoc = await GetDocumentAsync<MessageDocument>(threadDoc.MessageId, threadIdStr);

            if (startMessageDoc == null)
            {
                _logger.LogInternalInformation("Start message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadId);
                return null;
            }

            // last message may be null if thread was created before we started saving last message id
            // & a new message has not been added to the thread
            Message lastMessageDocDomainModel;
            if (threadDoc.LastMessageId == null)
            {
                _logger.LogInternalInformation("last message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadDoc.Id);
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
            var thread = threadDoc.ToDomainModel(startMessageDoc.ToDomainModel(), lastMessageDocDomainModel);

            if (!string.IsNullOrEmpty(threadDoc.IncidentId))
            {
                thread.Status = new Status
                {
                    IncidentStatus = new IncidentStatus
                    {
                        IncidentId = threadDoc.IncidentId,
                    }
                };
            }

            // add thread status
            thread.Status = await GetThreadStatus(thread);

            return thread;
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
        var query = _client.GetContainer<ThreadDocument>(_databaseName).GetItemLinqQueryable<ThreadDocument>()
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
                    _logger.LogInternalInformation("last message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadDoc.Id);
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
                    var thread = threadDoc.ToDomainModel(startMessageDoc.ToDomainModel(), lastMessageDocDomainModel);

                    if (!string.IsNullOrEmpty(threadDoc.IncidentId))
                    {
                        thread.Status = new Status
                        {
                            IncidentStatus = new IncidentStatus
                            {
                                IncidentId = threadDoc.IncidentId,
                            }
                        };
                    }
                    threads.Add(thread);
                }
            }

        }

        foreach (var thread in threads)
        {
            thread.Status = await GetThreadStatus(thread);
        }

        return threads;
    }

    public async Task<IEnumerable<Thread>> GetThreadsAsync(ODataQueryOptions? queryOptions, ActionSeverity? severity = null)
    {
        var threads = new List<Thread>();

        // Query for thread documents
        var query = _client.GetContainer<ThreadDocument>(_databaseName).GetItemLinqQueryable<ThreadDocument>()
            .Where(t => t.DocumentType == "Thread")
            .OrderBy(t => t.CreatedTimestamp);

        if (queryOptions is not null)
        {
            query = queryOptions.ApplyTo(query, oDataQuerySettings) as IOrderedQueryable<ThreadDocument>;
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
                    _logger.LogInternalInformation("last message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadDoc.Id);
                    lastMessageDocDomainModel = null;
                }
                else
                {
                    MessageDocument lastMessageDoc = await GetDocumentAsync<MessageDocument>(
                        threadDoc.LastMessageId,
                        threadDoc.Id
                    );
                    lastMessageDocDomainModel = lastMessageDoc == null ? null : lastMessageDoc.ToDomainModel(isDailyReport: lastMessageDoc.IsDailyReport);
                }

                if (startMessageDoc != null)
                {
                    var thread = threadDoc.ToDomainModel(startMessageDoc.ToDomainModel(isDailyReport: startMessageDoc.IsDailyReport), lastMessageDocDomainModel);

                    if (!string.IsNullOrEmpty(threadDoc.IncidentId))
                    {
                        thread.Status = new Status
                        {
                            IncidentStatus = new IncidentStatus
                            {
                                IncidentId = threadDoc.IncidentId,
                            }
                        };
                    }

                    threads.Add(thread);
                }
            }
        }

        // add thread action & incident status info
        foreach (var thread in threads)
        {
            thread.Status = await GetThreadStatus(thread);
        }

        // Filter threads by severity if specified
        if (severity is not null)
        {
            if (severity == ActionSeverity.Critical)
            {
                threads = threads.Where(t => t.Status.ActionsStatus?.HasCriticalActions == true).ToList();
            }
            else if (severity == ActionSeverity.Warning)
            {
                threads = threads.Where(t => t.Status.ActionsStatus?.HasWarningActions == true).ToList();
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

        threadDoc.IncidentId = thread.Status?.IncidentStatus?.IncidentId ?? string.Empty;

        await _client.GetContainer<ThreadDocument>(_databaseName).CreateItemAsync(threadDoc, new PartitionKey(threadDoc.PartitionKey));

        return thread;
    }

    public async Task<bool> DeleteThreadAsync(Guid threadId)
    {
        string threadIdStr = threadId.ToString();

        var container = _client.GetContainer<ThreadDocument>(_databaseName);

        try
        {
            // Delete all messages in the thread first
            var messagesQuery = container.GetItemLinqQueryable<MessageDocument>()
                .Where(m => m.DocumentType == "Message" && m.ThreadId == threadIdStr);

            using var iterator = messagesQuery.ToFeedIterator();

            while (iterator.HasMoreResults)
            {
                foreach (var message in await iterator.ReadNextAsync())
                {
                    await container.DeleteItemAsync<MessageDocument>(
                        message.Id,
                        new PartitionKey(message.PartitionKey)
                    );
                }
            }

            // Delete all actions in the thread
            var actionsQuery = container.GetItemLinqQueryable<ActionDocument>()
                .Where(a => a.DocumentType == "Action" && a.ThreadId == threadIdStr);

            using var actionIterator = actionsQuery.ToFeedIterator();

            while (actionIterator.HasMoreResults)
            {
                foreach (var action in await actionIterator.ReadNextAsync())
                {
                    await container.DeleteItemAsync<ActionDocument>(
                        action.Id,
                        new PartitionKey(action.PartitionKey)
                    );
                }
            }

            // Delete all teams conversation mapping in the thread
            var teamsQuery = container.GetItemLinqQueryable<ThreadTeamsMappingDocument>()
                .Where(a => a.DocumentType == "ThreadTeamsMapping" && a.ThreadId == threadIdStr);

            using var teamsIterator = teamsQuery.ToFeedIterator();

            while (teamsIterator.HasMoreResults)
            {
                foreach (var teamsMapping in await teamsIterator.ReadNextAsync())
                {
                    await container.DeleteItemAsync<ThreadTeamsMappingDocument>(
                        teamsMapping.Id,
                        new PartitionKey(teamsMapping.PartitionKey)
                    );
                }
            }

            // Finally delete the thread
            await container.DeleteItemAsync<ThreadDocument>(
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
                _logger.LogInternalWarning("Cannot update title: Thread {ThreadId} not found", threadId);
                return null;
            }

            // Update the title and modified timestamp
            ThreadDocument updatedThreadDoc = threadDoc with
            {
                Title = newTitle,
                ModifiedTimestamp = DateTime.UtcNow
            };

            // Save the updated document
            var response = await _client.GetContainer<ThreadDocument>(_databaseName).ReplaceItemAsync(
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
                _logger.LogInternalInformation("last message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadDoc.Id);
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
                _logger.LogInternalWarning("Start message {MessageId} not found for thread {ThreadId}",
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
            _logger.LogInternalInformation("Successfully updated title for thread {ThreadId}", threadId);
            return updatedThreadDoc.ToDomainModel(startMessageDoc.ToDomainModel(), lastMessageDocDomainModel);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Cannot update title: Thread {ThreadId} not found", threadId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating title for thread {ThreadId}", threadId);
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

        var query = _client.GetContainer<MessageDocument>(_databaseName).GetItemLinqQueryable<MessageDocument>()
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
                var messageDocWithApproval = messageDoc;
                // Replace the approval with the Approval doc in Cosmos
                if (messageDoc.Approval != null)
                {
                    //var approvalDoc = _client.GetContainer<ApprovalDocument>(_databaseName).GetItemLinqQueryable<ApprovalDocument>()
                    //    .Where(a => a.Id == messageDoc.Approval.Id.ToString()).FirstOrDefault();
                    var approvalQuery = _client.GetContainer<ApprovalDocument>(_databaseName)
                            .GetItemLinqQueryable<ApprovalDocument>()
                             .Where(a => a.Id == messageDoc.Approval.Id.ToString());

                    using var approvalIterator = approvalQuery.ToFeedIterator();
                    ApprovalDocument? approvalDoc = null;
                    if (approvalIterator.HasMoreResults)
                    {
                        var approvalResults = await approvalIterator.ReadNextAsync();
                        approvalDoc = approvalResults.FirstOrDefault();
                    }

                    messageDocWithApproval = new MessageDocument(messageDoc.Id,
                        messageDoc.ThreadId,
                        messageDoc.TimeStamp,
                        messageDoc.Author,
                        messageDoc.Text,
                        messageDoc.IsImageContent,
                        messageDoc.Posted,
                        approvalDoc?.ToDomainModel(),
                        messageDoc.IncidentDiscussionId,
                        messageDoc.IsDailyReport);
                }
                messages.Add(messageDocWithApproval.ToDomainModel(isDailyReport: messageDoc.IsDailyReport));
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

        var container = _client.GetContainer<MessageDocument>(_databaseName);

        await container.CreateItemAsync(messageDoc, new PartitionKey(messageDoc.PartitionKey));

        // Update the thread's modified timestamp
        try
        {
            ThreadDocument threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);
            if (threadDoc != null)
            {
                ThreadDocument updatedThreadDoc = threadDoc with { ModifiedTimestamp = DateTime.UtcNow };
                await container.ReplaceItemAsync(
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
                await container.ReplaceItemAsync(
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

    public async Task<Message> UpdateMessageAsync(Guid threadId, Message message)
    {
        if (message.Id == Guid.Empty)
        {
            _logger.LogInternalError("Cannot update message: Message ID is empty");
            throw new ArgumentException("Message ID cannot be empty for update operation", nameof(message));
        }

        string threadIdStr = threadId.ToString();
        string messageIdStr = message.Id.ToString();

        try
        {
            // Check if the message exists
            MessageDocument existingMessage = await GetDocumentAsync<MessageDocument>(messageIdStr, threadIdStr);
            if (existingMessage == null)
            {
                _logger.LogInternalWarning("Cannot update message: Message {MessageId} not found in thread {ThreadId}",
                    messageIdStr, threadIdStr);
                return null;
            }

            // Create the updated message document
            MessageDocument messageDoc = MessageDocument.FromDomainModel(message, threadIdStr);

            // Replace the existing document with the updated one
            var container = _client.GetContainer<MessageDocument>(_databaseName);
            await container.ReplaceItemAsync(
                messageDoc,
                messageIdStr,
                new PartitionKey(threadIdStr)
            );

            _logger.LogInternalInformation("Successfully updated message {MessageId} in thread {ThreadId}", messageIdStr, threadIdStr);
            return message;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Cannot update message: Message {MessageId} not found in thread {ThreadId}",
                messageIdStr, threadIdStr);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating message {MessageId} in thread {ThreadId}", messageIdStr, threadIdStr);
            throw;
        }
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
            await _client.GetContainer<MessageDocument>(_databaseName).DeleteItemAsync<MessageDocument>(
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
        var query = _client.GetContainer<ThreadContextDocument>(_databaseName).GetItemLinqQueryable<ThreadContextDocument>()
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
        await _client.GetContainer<ThreadContextDocument>(_databaseName).CreateItemAsync(threadContextDoc, new PartitionKey(threadContextDoc.PartitionKey));

        return threadContext;
    }

    public async Task<ThreadContext> UpdateThreadContextAsync(ThreadContext threadContext)
    {
        // Ensure IDs are set
        if (threadContext.ThreadId == Guid.Empty)
            return null;

        // Then create the thread
        ThreadContextDocument threadContextDoc = ThreadContextDocument.FromDomainModel(threadContext);
        await _client.GetContainer<ThreadContextDocument>(_databaseName).UpsertItemAsync(threadContextDoc, new PartitionKey(threadContextDoc.PartitionKey));

        return threadContext;
    }

    public async Task<bool> DeleteThreadContextAsync(Guid threadId)
    {
        string threadIdStr = threadId.ToString();
        var threadContextDocId = ThreadContextDocument.GetId(threadIdStr);


        try
        {
            // Finally delete the thread
            await _client.GetContainer<ThreadContextDocument>(_databaseName).DeleteItemAsync<ThreadDocument>(
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

        var query = _client.GetContainer<ActionDocument>(_databaseName).GetItemLinqQueryable<ActionDocument>()
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

    public async Task<Action> AddOrUpdateActionAsync(Guid threadId, Action action)
    {
        // Ensure ID is set
        if (action.Id == Guid.Empty)
            action = action with { Id = Guid.NewGuid() };

        string threadIdStr = threadId.ToString();

        // Create the action document
        ActionDocument actionDoc = ActionDocument.FromDomainModel(action, threadIdStr);
        await _client.GetContainer<ActionDocument>(_databaseName).UpsertItemAsync(actionDoc, new PartitionKey(actionDoc.PartitionKey));

        return action;
    }
    public async Task<Action> GetActionAsync(Guid threadId, Guid actionId)
    {
        string threadIdStr = threadId.ToString();
        string actionIdStr = actionId.ToString();

        try
        {
            // Query for the specific action by its ID and thread ID
            var query = _client.GetContainer<ActionDocument>(_databaseName).GetItemLinqQueryable<ActionDocument>()
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
            _logger.LogInternalError(ex, "Error retrieving action {ActionId} for thread {ThreadId}", actionId, threadId);
            throw;
        }
    }

    public async Task<IEnumerable<Action>> GetAllActionsAsync()
    {
        var actions = new List<Action>();

        var query = _client.GetContainer<ActionDocument>(_databaseName).GetItemLinqQueryable<ActionDocument>()
            .Where(a => a.DocumentType == "Action")
            .OrderByDescending(a => a.TimeStamp);

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

    public async Task<IEnumerable<string>> GetThreadIdsWithActionSeverityAsync(ActionSeverity? severity)
    {
        var threadIds = new List<string>();

        var query = _client.GetContainer<ActionDocument>(_databaseName).GetItemLinqQueryable<ActionDocument>()
            .Where(a => a.DocumentType == "Action" && a.Severity == severity)
            .OrderByDescending(a => a.TimeStamp);

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var actionDoc in await iterator.ReadNextAsync())
            {
                threadIds.Add(actionDoc.ThreadId);
            }
        }
        return threadIds;
    }


    #endregion

    #region Helper Methods

    private async Task<T> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            ItemResponse<T> response = await _client.GetContainer<T>(_databaseName).ReadItemAsync<T>(
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

    private async Task<(T document, string etag)> GetDocumentWithEtagAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            ItemResponse<T> response = await _client.GetContainer<T>(_databaseName).ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey)
            );
            return (response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return (default, null);
        }
    }

    private async Task<Status?> GetThreadStatus(Thread thread)
    {
        Status status = null;

        // update Actions Status Properties for each thread
        var threadIdsWithCriticalActions = await GetThreadIdsWithActionSeverityAsync(ActionSeverity.Critical);
        var threadIdsWithWarningActions = await GetThreadIdsWithActionSeverityAsync(ActionSeverity.Warning);

        // Check if the thread has critical or warning actions
        bool hasCriticalActions = threadIdsWithCriticalActions.Contains(thread.Id.ToString());
        bool hasWarningActions = threadIdsWithWarningActions.Contains(thread.Id.ToString());

        status = new Status
        {
            ActionsStatus = new ActionsStatus
            {
                HasCriticalActions = hasCriticalActions,
                HasWarningActions = hasWarningActions
            }
        };

        // add incident status
        if (thread.Source == ThreadSource.Incident)
        {
            if (thread.Status != null && !string.IsNullOrEmpty(thread.IncidentSource?.IncidentId))
            {
                // check for incident in cosmos and apply status
                // check pager duty first
                PagerDutyIncidentDocument pagerDutyIncident = await GetDocumentAsync<PagerDutyIncidentDocument>(thread.IncidentSource.IncidentId, thread.IncidentSource.IncidentId);

                if (pagerDutyIncident != null)
                {
                    status.IncidentStatus = new IncidentStatus
                    {
                        IncidentId = thread.IncidentSource.IncidentId,
                        Status = pagerDutyIncident.Status
                    };
                }
                else
                {
                    // check azmon incident
                    AzMonitorAlertDocument azMonIncident = await GetDocumentAsync<AzMonitorAlertDocument>(thread.Status?.IncidentStatus?.IncidentId, thread.Status?.IncidentStatus?.IncidentId);
                    if (azMonIncident != null)
                    {
                        status.IncidentStatus = new IncidentStatus
                        {
                            IncidentId = thread.Status?.IncidentStatus?.IncidentId,
                            Status = azMonIncident.Status
                        };
                    }
                }
            }
        }

        return status;
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

        var query = _client.GetContainer<MessageFeedbackDocument>(_databaseName).GetItemLinqQueryable<MessageFeedbackDocument>()
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

        var query = _client.GetContainer<MessageFeedbackDocument>(_databaseName).GetItemLinqQueryable<MessageFeedbackDocument>()
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
        await _client.GetContainer<MessageFeedbackDocument>(_databaseName).UpsertItemAsync(messageFeedbackDoc, new PartitionKey(messageFeedbackDoc.PartitionKey));

        return messageFeedback;
    }

    public async Task<bool> DeleteMessageFeedbackAsync(Guid threadId, Guid messageFeedbackId)
    {
        string threadIdStr = threadId.ToString();
        string messageFeedbackIdStr = messageFeedbackId.ToString();

        try
        {
            // Delete the message
            await _client.GetContainer<MessageFeedbackDocument>(_databaseName).DeleteItemAsync<MessageFeedbackDocument>(
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
    public async Task<AgentContext?> GetAgentContextAsync(Guid agentContextId, Guid threadId)
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
        var query = _client.GetContainer<AgentContextDocument>(_databaseName).GetItemLinqQueryable<AgentContextDocument>()
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

    public async Task<IEnumerable<AgentContext>> GetAllAgentContextsAsync()
    {
        var agentContexts = new List<AgentContext>();
        var query = _client.GetContainer<AgentContextDocument>(_databaseName).GetItemLinqQueryable<AgentContextDocument>()
            .Where(m => m.DocumentType == "AgentContext");

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
        await _client.GetContainer<AgentContextDocument>(_databaseName).CreateItemAsync(agentContextDoc, new PartitionKey(agentContextDoc.PartitionKey));
        return agentContext;
    }

    public async Task<AgentContext> UpdateAgentContextAsync(AgentContext agentContext)
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
        await _client.GetContainer<AgentContextDocument>(_databaseName).UpsertItemAsync(agentContextDoc, new PartitionKey(agentContextDoc.PartitionKey));
        return agentContext;
    }

    public async Task<bool> UpdateAgentContextAssignmentInfoAsync(
        Guid agentContextId,
        Guid threadId,
        string? assignedInstanceId,
        DateTimeOffset? expiration)
    {
        try
        {
            TransactionalBatch batch = _client.GetContainer<AgentContextDocument>(_databaseName)
                .CreateTransactionalBatch(new PartitionKey(threadId.ToString()));

            batch.PatchItem(agentContextId.ToString(), [
                PatchOperation.Set(AgentContextDocument.AssignedInstancePatchPath, assignedInstanceId),
                PatchOperation.Set(AgentContextDocument.AssignmentExpiresPatchPath, expiration)
            ]);

            TransactionalBatchResponse response = await batch.ExecuteAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInternalError("Failed to patch assignment info for agent context {agentContextId} with thread id {threadId}, status code: {statusCode}",
                    agentContextId, threadId, response.StatusCode);

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating assignment info for agent context {agentContextId} with thread id {threadId}",
                agentContextId, threadId);

            return false;
        }
    }

    public async Task<bool> DeleteAgentContextAsync(Guid agentContextId, Guid threadId)
    {
        string threadIdStr = threadId.ToString();
        string agentContextIdStr = agentContextId.ToString();

        try
        {
            // Delete the message
            await _client.GetContainer<AgentContextDocument>(_databaseName).DeleteItemAsync<AgentContextDocument>(
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
        await _client.GetContainer<ReasoningMessageDocument>(_databaseName).CreateItemAsync(reasoningMessageDoc, new PartitionKey(reasoningMessageDoc.PartitionKey));
        return reasoningMessage;
    }

    public async Task<bool> DeleteReasoningMessageAsync(Guid reasoningMessageId, Guid agentContextId)
    {
        string agentContextIdStr = agentContextId.ToString();
        string reasoningMessageIdStr = reasoningMessageId.ToString();

        try
        {
            // Delete the message
            await _client.GetContainer<ReasoningMessageDocument>(_databaseName).DeleteItemAsync<ReasoningMessageDocument>(
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

    #region AgentChatHistory Operations
    public async Task<AgentChatHistory> GetAgentChatHistoryAsync(Guid agentContextId)
    {
        try
        {
            string agentContextIdStr = agentContextId.ToString();

            AgentChatHistoryDocument agentChatHistoryDocument = await GetDocumentAsync<AgentChatHistoryDocument>(AgentChatHistoryDocument.GetDocumentId(agentContextIdStr), agentContextIdStr);

            return agentChatHistoryDocument?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<AgentChatHistory> CreateAgentChatHistoryAsync(AgentChatHistory agentChatHistory)
    {
        // Ensure IDs are set
        if (agentChatHistory.AgentContextId == Guid.Empty)
        {
            return null;
        }

        // Create the sub-agent thread document
        AgentChatHistoryDocument agentChatHistoryDoc = AgentChatHistoryDocument.FromDomainModel(agentChatHistory);
        await _client.GetContainer<AgentChatHistoryDocument>(_databaseName).CreateItemAsync(agentChatHistoryDoc, new PartitionKey(agentChatHistoryDoc.PartitionKey));
        return agentChatHistory;
    }

    public async Task<AgentChatHistory> UpdateAgentChatHistoryAsync(AgentChatHistory agentChatHistory)
    {
        // Ensure IDs are set
        if (agentChatHistory.AgentContextId == Guid.Empty)
        {
            return null;
        }

        // Create the sub-agent thread document
        AgentChatHistoryDocument agentChatHistoryDoc = AgentChatHistoryDocument.FromDomainModel(agentChatHistory);
        await _client.GetContainer<AgentChatHistoryDocument>(_databaseName).UpsertItemAsync(agentChatHistoryDoc, new PartitionKey(agentChatHistoryDoc.PartitionKey));
        return agentChatHistory;
    }

    public async Task<AgentChatHistory> AddReasoningMessagesToChatHistoryAsync(AgentChatHistory agentChatHistory, params IEnumerable<ReasoningMessage> reasoningMessages)
    {
        // Ensure IDs are set
        if (agentChatHistory.AgentContextId == Guid.Empty)
        {
            return null;
        }

        // get existing document from cosmos with etag
        var (existingDocument, etag) = await GetDocumentWithEtagAsync<AgentChatHistoryDocument>(
            AgentChatHistoryDocument.GetDocumentId(agentChatHistory.AgentContextId.ToString()),
            agentChatHistory.AgentContextId.ToString()
        );

        if (existingDocument == null)
        {
            try
            {
                foreach (var message in reasoningMessages)
                {
                    agentChatHistory.ReasoningMessageIds.Add(message.Id);

                    if (message.Role == ReasoningMessageRoleEnum.User)
                    {
                        agentChatHistory.LatestUserMessageId = message.Id;
                    }
                }

                AgentChatHistoryDocument agentChatHistoryDoc = AgentChatHistoryDocument.FromDomainModel(agentChatHistory);
                var createResponse = await _client.GetContainer<AgentChatHistoryDocument>(_databaseName).CreateItemAsync(agentChatHistoryDoc, new PartitionKey(agentChatHistoryDoc.PartitionKey));
                return createResponse.Resource.ToDomainModel();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // another thread created the document at the same time, fetch it and continue
                (existingDocument, etag) = await GetDocumentWithEtagAsync<AgentChatHistoryDocument>(
                    AgentChatHistoryDocument.GetDocumentId(agentChatHistory.AgentContextId.ToString()),
                    agentChatHistory.AgentContextId.ToString()
                );
            }
        }

        // retry a few times
        const int retryLimit = 3;
        for (int i = 0; i < retryLimit; i++)
        {
            try
            {
                foreach (var message in reasoningMessages)
                {
                    existingDocument.ReasoningMessageIds.Add(message.Id.ToString());

                    if (message.Role == ReasoningMessageRoleEnum.User)
                    {
                        existingDocument.LatestUserMessageId = message.Id.ToString();
                    }
                }

                var updateResponse = await _client.GetContainer<AgentChatHistoryDocument>(_databaseName).ReplaceItemAsync(
                    existingDocument,
                    existingDocument.Id,
                    new PartitionKey(existingDocument.PartitionKey),
                    new ItemRequestOptions { IfMatchEtag = etag }
                );

                return updateResponse.Resource.ToDomainModel();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                if (i == retryLimit - 1)
                {
                    // log the error and throw an exception
                    _logger.LogInternalError("Failed to update agent chat history for agent context {AgentContextId} after {RetryCount} attempts", agentChatHistory.AgentContextId, retryLimit);
                    throw;
                }

                // another thread updated the document since we fetched it, re-fetch the document and retry
                (existingDocument, etag) = await GetDocumentWithEtagAsync<AgentChatHistoryDocument>(
                    AgentChatHistoryDocument.GetDocumentId(agentChatHistory.AgentContextId.ToString()),
                    agentChatHistory.AgentContextId.ToString()
                );
            }
        }

        throw new Exception("Failed to update agent chat history after multiple attempts.");
    }

    public async Task<bool> DeleteAgentChatHistoryAsync(Guid agentContextId)
    {
        string agentContextIdStr = agentContextId.ToString();

        try
        {
            // Delete the message
            await _client.GetContainer<AgentChatHistoryDocument>(_databaseName).DeleteItemAsync<AgentChatHistoryDocument>(
                AgentChatHistoryDocument.GetDocumentId(agentContextIdStr),
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

    #region ApprovalV2 Operations
    public async Task<ApprovalV2> GetApprovalV2Async(Guid approvalIdV2, Guid agentContextId)
    {
        try
        {
            string approvalIdStr = approvalIdV2.ToString();
            string agentContextIdStr = agentContextId.ToString();

            ApprovalV2Document approvalV2Document = await GetDocumentAsync<ApprovalV2Document>(approvalIdStr, agentContextIdStr);

            return approvalV2Document?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<ApprovalV2>> GetAllApprovalV2sAsync()
    {
        var approvalV2s = new List<ApprovalV2>();
        var query = _client.GetContainer<ApprovalV2Document>(_databaseName).GetItemLinqQueryable<ApprovalV2Document>()
            .Where(m => m.DocumentType == "ApprovalV2");

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var approvalV2Docuemnt in await iterator.ReadNextAsync())
            {
                approvalV2s.Add(approvalV2Docuemnt.ToDomainModel());
            }
        }
        return approvalV2s;
    }

    public async Task<ApprovalV2> CreateApprovalV2Async(ApprovalV2 approvalV2)
    {
        // Ensure IDs are set
        if (approvalV2.Id == Guid.Empty)
        {
            approvalV2 = approvalV2 with { Id = Guid.NewGuid() };
        }

        if (approvalV2.AgentContextId == Guid.Empty)
        {
            return null;
        }

        // Create the sub-agent thread document
        ApprovalV2Document approvalV2Document = ApprovalV2Document.FromDomainModel(approvalV2);
        await _client.GetContainer<ApprovalV2Document>(_databaseName).CreateItemAsync(approvalV2Document, new PartitionKey(approvalV2Document.PartitionKey));
        return approvalV2;
    }

    public async Task<ApprovalV2> UpdateApprovalV2Async(ApprovalV2 approvalV2)
    {
        // Ensure IDs are set
        if (approvalV2.Id == Guid.Empty)
        {
            approvalV2 = approvalV2 with { Id = Guid.NewGuid() };
        }

        if (approvalV2.AgentContextId == Guid.Empty)
        {
            return null;
        }

        // Create the sub-agent thread document
        ApprovalV2Document approvalV2Document = ApprovalV2Document.FromDomainModel(approvalV2);
        await _client.GetContainer<ApprovalV2Document>(_databaseName).UpsertItemAsync(approvalV2Document, new PartitionKey(approvalV2Document.PartitionKey));
        return approvalV2;
    }

    public async Task<Approval> CreateApprovalAsync(Approval approval)
    {
        // Ensure IDs are set
        if (approval.Id == Guid.Empty)
        {
            approval = approval with { Id = Guid.NewGuid() };
        }

        if (string.IsNullOrEmpty(approval.ThreadId))
        {
            return null;
        }

        ApprovalDocument approvalDocument = ApprovalDocument.FromDomainModel(approval);
        await _client.GetContainer<ApprovalDocument>(_databaseName).CreateItemAsync(approvalDocument, new PartitionKey(approvalDocument.PartitionKey));

        return approval;
    }

    public async Task<Approval> GetApprovalAsync(Guid threadId, Guid approvalId)
    {
        try
        {
            string id = approvalId.ToString();
            string partitionKey = threadId.ToString();

            ApprovalDocument approvalDocument = await GetDocumentAsync<ApprovalDocument>(id, partitionKey);

            return approvalDocument?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // there might be multiple approvals with same title due to oboToken expiration
    // always get the approval with latest created timestamp
    public async Task<Approval> GetApprovalAsync(Guid threadId, string title)
    {
        try
        {
            string partitionKey = threadId.ToString();

            var query = _client.GetContainer<ApprovalDocument>(_databaseName).GetItemLinqQueryable<ApprovalDocument>()
                .Where(d => d.Title == title && d.ThreadId == partitionKey)
                .OrderByDescending(d => d.CreatedTimestamp)
                .Take(1);

            using (var iterator = query.ToFeedIterator())
            {
                if (!iterator.HasMoreResults)
                {
                    return null;
                }

                var results = await iterator.ReadNextAsync();
                if (results.Count == 0)
                {
                    return null;
                }

                return results.First().ToDomainModel();
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Approval> UpdateApprovalAsync(Approval approval)
    {
        var approvalDocument = ApprovalDocument.FromDomainModel(approval);
        await _client.GetContainer<ApprovalDocument>(_databaseName).UpsertItemAsync(approvalDocument, new PartitionKey(approvalDocument.PartitionKey));

        return approval;
    }

    public async Task<IList<Approval>> GetApprovalsAsync(Guid threadId)
    {
        try
        {
            string partitionKey = threadId.ToString();

            var query = _client.GetContainer<ApprovalDocument>(_databaseName).GetItemLinqQueryable<ApprovalDocument>()
                .Where(d => d.ThreadId == partitionKey && d.DocumentType == ApprovalDocument.DocumentTypeName);

            var approvals = new List<Approval>();
            using (var iterator = query.ToFeedIterator())
            {
                while (iterator.HasMoreResults)
                {
                    var results = await iterator.ReadNextAsync();
                    approvals.AddRange(results.Select(d => d.ToDomainModel()));
                }
            }

            return approvals;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    #endregion

    #region GitHubAccessToken Operations
    public async Task<GitHubAccessToken> GetGitHubAccessTokenAsync()
    {
        try
        {
            GitHubAccessTokenDocument gitHubAccessTokenDocument = await GetDocumentAsync<GitHubAccessTokenDocument>("GitHubAccessToken", "GitHubAccessToken");
            return gitHubAccessTokenDocument?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<GitHubAccessToken> CreateOrUpdateGitHubAccessTokenAsync(GitHubAccessToken gitHubAccessToken)
    {
        // Create the GitHub access token document
        GitHubAccessTokenDocument gitHubAccessTokenDoc = GitHubAccessTokenDocument.FromDomainModel(gitHubAccessToken);
        await _client.GetContainer<GitHubAccessTokenDocument>(_databaseName).UpsertItemAsync(gitHubAccessTokenDoc, new PartitionKey(gitHubAccessTokenDoc.PartitionKey));
        return gitHubAccessToken;
    }

    public async Task<bool> DeleteGitHubAccessTokenAsync()
    {
        try
        {
            // Delete the message
            await _client.GetContainer<GitHubAccessTokenDocument>(_databaseName).DeleteItemAsync<GitHubAccessTokenDocument>("GitHubAccessToken", new PartitionKey("GitHubAccessToken"));

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
    #endregion
}
