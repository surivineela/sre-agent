// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Data.Helpers;
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
    private static readonly ODataQuerySettings oDataQuerySettings = new() { EnsureStableOrdering = false, TimeZone = TimeZoneInfo.Utc, HandleNullPropagation = HandleNullPropagationOption.False };

    public CosmosDbThreadRepository(CosmosClient cosmosClient, string databaseName, ILogger<CosmosDbThreadRepository> logger)
    {
        _logger = logger;
        _databaseName = databaseName;
        _client = cosmosClient;
    }

    #region Thread Operations

    public async Task<Thread?> GetThreadAsync(Guid threadId)
    {
        _logger.LogInternalInformation("Trying to get thread: {Id}", threadId);
        try
        {
            // First get the thread document
            var threadIdStr = threadId.ToString();
            var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc == null)
            {
                _logger.LogInternalInformation("Thread not found: {Id}", threadId);
                return null;
            }

            // Then get the start message (if it exists)
            MessageDocument? startMessageDoc = null;
            if (!string.IsNullOrEmpty(threadDoc.MessageId))
            {
                startMessageDoc = await GetDocumentAsync<MessageDocument>(threadDoc.MessageId, threadIdStr);
            }

            if (startMessageDoc == null)
            {
                _logger.LogInternalInformation("Start message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadId);
                return null;
            }

            // last message may be null if thread was created before we started saving last message id
            // & a new message has not been added to the thread
            Message? lastMessageDocDomainModel;
            if (threadDoc.LastMessageId == null || string.IsNullOrEmpty(threadDoc.LastMessageId))
            {
                _logger.LogInternalInformation("last message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadDoc.Id);
                lastMessageDocDomainModel = null;
            }
            else
            {
                var lastMessageDoc = await GetDocumentAsync<MessageDocument>(
                    threadDoc.LastMessageId,
                    threadDoc.Id
                );
                lastMessageDocDomainModel = lastMessageDoc?.ToDomainModel();
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

    public async Task<IEnumerable<Thread>> GetThreadsBySourceAsync(ODataQueryOptions? queryOptins, ThreadSource? source = null, IncidentType? incidentType = null, DateTime? createdAfter = null)
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

        if (incidentType.HasValue)
        {
            query = query.Where(t => t.IncidentSource != null && t.IncidentSource.IncidentType == incidentType.Value);
        }

        if (createdAfter.HasValue)
        {
            query = query.Where(t => t.CreatedTimestamp >= createdAfter.Value);
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
                var startMessageDoc = await GetDocumentAsync<MessageDocument>(
                    threadDoc.MessageId,
                    threadDoc.Id
                );
                // last message may be null if thread was created before we started saving last message id
                // & a new message has not been added to the thread
                Message? lastMessageDocDomainModel;
                if (threadDoc.LastMessageId == null)
                {
                    _logger.LogInternalInformation("last message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadDoc.Id);
                    lastMessageDocDomainModel = null;
                }
                else
                {
                    var lastMessageDoc = await GetDocumentAsync<MessageDocument>(
                        threadDoc.LastMessageId,
                        threadDoc.Id
                    );
                    lastMessageDocDomainModel = lastMessageDoc?.ToDomainModel();
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

    public async Task<IEnumerable<Thread>> GetThreadsAsync(ODataQueryOptions? queryOptions, ActionSeverity? severity = null, ThreadType? threadType = ThreadType.Prod, bool? favorite = null)
    {
        var threads = new List<Thread>();

        // Query for thread documents
        // Note: We filter by DocumentType to exclude SessionInsightDocument and other document types
        // that share the same container but have different schemas
        var query = _client.GetContainer<ThreadDocument>(_databaseName).GetItemLinqQueryable<ThreadDocument>()
            .Where(t => t.DocumentType == "Thread")
            .OrderBy(t => t.CreatedTimestamp);

        if (threadType is not null)
        {
            if (threadType == ThreadType.Prod)
            {
                query = query.Where(t => t.ThreadType.IsDefined() == false || t.ThreadType == null || t.ThreadType == threadType) as IOrderedQueryable<ThreadDocument>;
            }
            else
            {
                query = query.Where(t => t.ThreadType == threadType) as IOrderedQueryable<ThreadDocument>;
            }
        }

        if ((query is not null) && (favorite is not null))
        {
            if (favorite == true)
            {
                query = query.Where(t => t.Favorite == true) as IOrderedQueryable<ThreadDocument>;
            }
            else
            {
                query = query.Where(t => t.Favorite.IsDefined() == false || t.Favorite == null || t.Favorite == false) as IOrderedQueryable<ThreadDocument>;
            }
        }

        if (query is not null && queryOptions is not null)
        {
            query = queryOptions.ApplyTo(query, oDataQuerySettings) as IOrderedQueryable<ThreadDocument>;
        }

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var threadDoc in await iterator.ReadNextAsync())
            {
                // Skip threads with null IDs or MessageIds
                // This can happen when SessionInsightDocument objects are incorrectly deserialized as ThreadDocument
                // since they share the same container but have different schemas
                if (string.IsNullOrEmpty(threadDoc.Id))
                {
                    _logger.LogInternalWarning(
                        "Skipping document with null or empty ID. MessageId: {MessageId}, DocumentType: {DocumentType}. " +
                        "This may indicate a SessionInsightDocument was deserialized as ThreadDocument.",
                        threadDoc.MessageId,
                        threadDoc.DocumentType);
                    continue;
                }

                if (string.IsNullOrEmpty(threadDoc.MessageId))
                {
                    _logger.LogInternalWarning(
                        "Skipping document with null or empty MessageId. ThreadId: {ThreadId}, DocumentType: {DocumentType}. " +
                        "This may indicate a SessionInsightDocument was deserialized as ThreadDocument.",
                        threadDoc.Id,
                        threadDoc.DocumentType);
                    continue;
                }

                // Get the start message for each thread
                var startMessageDoc = await GetDocumentAsync<MessageDocument>(
                    threadDoc.MessageId,
                    threadDoc.Id
                );

                // last message may be null if thread was created before we started saving last message id
                // & a new message has not been added to the thread
                Message? lastMessageDocDomainModel;
                if (string.IsNullOrEmpty(threadDoc.LastMessageId))
                {
                    _logger.LogInternalInformation("last message {startMessageId} not found for thread: {Id}", threadDoc.MessageId, threadDoc.Id);
                    lastMessageDocDomainModel = null;
                }
                else
                {
                    var lastMessageDoc = await GetDocumentAsync<MessageDocument>(
                        threadDoc.LastMessageId,
                        threadDoc.Id
                    );
                    lastMessageDocDomainModel = lastMessageDoc?.ToDomainModel(isDailyReport: lastMessageDoc.IsDailyReport);
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
                                Status = threadDoc.IncidentStatus
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
                threads = threads.Where(t => t?.Status?.ActionsStatus?.HasCriticalActions == true).ToList();
            }
            else if (severity == ActionSeverity.Warning)
            {
                threads = threads.Where(t => t?.Status?.ActionsStatus?.HasWarningActions == true).ToList();
            }
        }

        return threads;
    }

    public async Task<IncidentThreadCounts> GetThreadsCountByStatusAsync(ODataQueryOptions? queryOptions = null)
    {
        // Query for thread documents
        var query = _client.GetContainer<ThreadDocument>(_databaseName).GetItemLinqQueryable<ThreadDocument>()
            .Where(t => t.DocumentType == "Thread")
            .Where(t => t.Source == ThreadSource.Incident)
            .Where(t => t.ThreadType.IsDefined() == false || t.ThreadType == null || t.ThreadType == ThreadType.Prod);

        if (query is not null && queryOptions is not null)
        {
            query = queryOptions.ApplyTo(query, oDataQuerySettings) as IOrderedQueryable<ThreadDocument>;
        }

        var incidentStatusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var investigationStatusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var threadDoc in await iterator.ReadNextAsync())
            {
                var incidentStatus = threadDoc.IncidentStatus ?? string.Empty;
                if (incidentStatusCounts.ContainsKey(incidentStatus))
                {
                    incidentStatusCounts[incidentStatus]++;
                }
                else
                {
                    incidentStatusCounts[incidentStatus] = 1;
                }

                var investigationStatus = threadDoc.IncidentDetails?.InvestigationStatus.ToString() ?? string.Empty;
                if (investigationStatusCounts.ContainsKey(investigationStatus))
                {
                    investigationStatusCounts[investigationStatus]++;
                }
                else
                {
                    investigationStatusCounts[investigationStatus] = 1;
                }
            }
        }

        var incidentStatusCountsList = incidentStatusCounts.Select(kvp => new StatusCount(kvp.Key, kvp.Value)).ToList();
        var investigationStatusCountsList = investigationStatusCounts.Select(kvp => new StatusCount(kvp.Key, kvp.Value)).ToList();

        return new IncidentThreadCounts(incidentStatusCountsList, investigationStatusCountsList);
    }

    public async Task<IEnumerable<Thread>> GetThreadsModifiedBetweenAsync(DateTime earliestInclusive, DateTime latestInclusive, ThreadType? threadType = ThreadType.Prod, bool? favorite = null)
    {
        var threads = new List<Thread>();

        // Query for thread documents and push modified timestamp filtering to Cosmos DB
        IQueryable<ThreadDocument> query = _client.GetContainer<ThreadDocument>(_databaseName).GetItemLinqQueryable<ThreadDocument>()
            .Where(t => t.DocumentType == "Thread" && t.ModifiedTimestamp >= earliestInclusive && t.ModifiedTimestamp <= latestInclusive)
            .OrderBy(t => t.CreatedTimestamp);

        if (threadType is not null)
        {
            if (threadType == ThreadType.Prod)
            {
                query = query.Where(t => t.ThreadType.IsDefined() == false || t.ThreadType == null || t.ThreadType == threadType);
            }
            else
            {
                query = query.Where(t => t.ThreadType == threadType);
            }
        }

        if (favorite is not null)
        {
            if (favorite == true)
            {
                query = query.Where(t => t.Favorite == true);
            }
            else
            {
                query = query.Where(t => t.Favorite.IsDefined() == false || t.Favorite == false);
            }
        }

        using var iterator = query.ToFeedIterator();

        // Only fetch thread documents and map to lightweight Thread domain models.
        // Avoid per-thread reads for start/last messages and avoid per-thread GetThreadStatus calls
        // to minimize Cosmos DB requests. Callers who need full message data should use GetThreadsAsync or GetThreadAsync.
        while (iterator.HasMoreResults)
        {
            foreach (var threadDoc in await iterator.ReadNextAsync())
            {
                // Map to a lightweight Thread domain model without fetching messages.
                var thread = threadDoc.ToDomainModel();

                // If incident info exists on the document, ensure IncidentStatus is preserved (ToDomainModel already sets it),
                // but keep Status null so callers can choose to populate it if needed.
                // We do not call GetThreadStatus here to avoid extra reads.

                threads.Add(thread);
            }
        }

        return threads;
    }

    public async Task<Thread> CreateThreadAsync(Thread thread)
    {
        // Ensure IDs are set
        if (thread.Id == Guid.Empty)
        {
            thread = thread with { Id = Guid.NewGuid() };
        }

        if (thread.StartMessage?.Id == Guid.Empty)
        {
            thread = thread with
            {
                StartMessage = thread.StartMessage with { Id = Guid.NewGuid() }
            };
        }

        if (thread?.LastMessage?.Id == Guid.Empty)
        {
            thread = thread with
            {
                LastMessage = thread.LastMessage with { Id = Guid.NewGuid() }
            };
        }

        if (thread == null)
        {
            _logger.LogInternalError("Input Thread is null in Creating Threads");
            throw new InvalidOperationException("thread is null");
        }

        // Then create the thread
        ThreadDocument threadDoc = ThreadDocument.FromDomainModel(thread);

        threadDoc.IncidentId = thread.Status?.IncidentStatus?.IncidentId ?? string.Empty;

        await _client.GetContainer<ThreadDocument>(_databaseName).CreateItemAsync(threadDoc, new PartitionKey(threadDoc.PartitionKey));

        return thread;
    }

    public async Task<bool> DeleteThreadAsync(Guid threadId)
    {
        var threadIdStr = threadId.ToString();

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

    public async Task<Thread?> UpdateThreadTitleAsync(Guid threadId, string newTitle, bool? updateModifiedTimestamp = true)
    {
        var threadIdStr = threadId.ToString();

        try
        {
            // Get the current thread document
            var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc == null)
            {
                _logger.LogInternalWarning("Cannot update title: Thread {ThreadId} not found", threadId);
                return null;
            }

            // Update the title
            var updatedThreadDoc = threadDoc with
            {
                Title = newTitle
            };

            // Update the modified timestamp if required
            if (updateModifiedTimestamp != false)
            {
                updatedThreadDoc = updatedThreadDoc with
                {
                    ModifiedTimestamp = DateTime.UtcNow
                };
            }

            // Save the updated document
            await _client.GetContainer<ThreadDocument>(_databaseName).ReplaceItemAsync(
                updatedThreadDoc,
                updatedThreadDoc.Id,
                new PartitionKey(updatedThreadDoc.PartitionKey)
            );

            var updatedThread = await GetThreadAsync(threadId);

            // Return the complete updated Thread domain model
            _logger.LogInternalInformation("Successfully updated title for thread {ThreadId}", threadId);
            return updatedThread;
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

    async Task<Thread?> IThreadRepository.UpdateThreadFeatureSetAsync(Guid threadId, FeatureConfig featureConfig)
    {
        try
        {
            var threadIdStr = threadId.ToString();

            // Get the current thread document
            var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc is null)
            {
                _logger.LogInternalWarning($"Cannot update featureSet: Thread {threadId} not found");
                return null;
            }

            // Update the feature config and modified timestamp
            var updatedThreadDoc = threadDoc with
            {
                FeatureConfig = featureConfig,
                ModifiedTimestamp = DateTime.UtcNow
            };

            // Save the updated document
            await _client.GetContainer<ThreadDocument>(_databaseName).ReplaceItemAsync(
                updatedThreadDoc,
                updatedThreadDoc.Id,
                new PartitionKey(updatedThreadDoc.PartitionKey)
            );

            _logger.LogInternalInformation($"Successfully updated featureSet for thread {threadId}. " +
                $"New Feature Config {WebJsonSerializer.Serialize(featureConfig)}");

            var updatedThread = await GetThreadAsync(threadId);
            return updatedThread;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning($"Cannot update featureSet: Thread {threadId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error updating featureSet for thread {threadId}");
        }

        return null;
    }

    public async Task<Thread?> UpdateThreadReadMarkAsync(Guid threadId, DateTime lastReadTime)
    {
        var threadIdStr = threadId.ToString();

        try
        {
            // Get the current thread document
            var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc == null)
            {
                _logger.LogInternalWarning("Cannot update thread: Thread {ThreadId} not found", threadId);
                return null;
            }

            // Create updated thread document with properties from the input thread
            var updatedThreadDoc = threadDoc with
            {
                LastReadTime = lastReadTime,
            };

            // Save the updated document
            var response = await _client.GetContainer<ThreadDocument>(_databaseName).ReplaceItemAsync(
                updatedThreadDoc,
                threadIdStr,
                new PartitionKey(threadIdStr)
            );

            // Get the updated thread with all of its data
            var updatedThread = await GetThreadAsync(threadId);
            return updatedThread;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning(ex, "Thread {ThreadId} not found during update", threadId);
            return null;
        }
    }

    public async Task<Thread?> UpdateThreadEvaluatedTimestampAsync(Guid threadId, DateTime evaluatedTimestamp)
    {
        var threadIdStr = threadId.ToString();

        try
        {
            // Get the current thread document
            var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc == null)
            {
                _logger.LogInternalWarning("Cannot update evaluated timestamp: Thread {ThreadId} not found", threadId);
                return null;
            }

            // Create updated thread document with new evaluated timestamp
            var updatedThreadDoc = threadDoc with
            {
                EvaluatedTimestamp = evaluatedTimestamp
            };

            // ToDo: All updates should honor etag
            // Save the updated document
            await _client.GetContainer<ThreadDocument>(_databaseName).ReplaceItemAsync(
                updatedThreadDoc,
                threadIdStr,
                new PartitionKey(threadIdStr)
            );

            // Get the updated thread with all of its data
            var updatedThread = await GetThreadAsync(threadId);
            return updatedThread;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Cannot update evaluated timestamp: Thread {ThreadId} not found", threadId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating evaluated timestamp for thread {ThreadId}", threadId);
            throw;
        }
    }

    public async Task<Thread?> UpdateTrajectoryGeneratedTimestampAsync(Guid threadId, DateTime evaluatedTimestamp)
    {
        var threadIdStr = threadId.ToString();

        try
        {
            // Get the current thread document
            var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc == null)
            {
                _logger.LogInternalWarning("Cannot update trajectory generated timestamp: Thread {ThreadId} not found", threadId);
                return null;
            }

            // Create updated thread document with new trajectory generated timestamp
            var updatedThreadDoc = threadDoc with
            {
                TrajectoryGeneratedTimestamp = evaluatedTimestamp
            };

            // Save the updated document
            await _client.GetContainer<ThreadDocument>(_databaseName).ReplaceItemAsync(
                updatedThreadDoc,
                threadIdStr,
                new PartitionKey(threadIdStr)
            );

            // Get the updated thread with all of its data
            var updatedThread = await GetThreadAsync(threadId);
            return updatedThread;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Cannot update trajectory generated timestamp: Thread {ThreadId} not found", threadId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating trajectory generated timestamp for thread {ThreadId}", threadId);
            throw;
        }
    }

    public async Task<Thread?> UpdateThreadAgentModeAsync(Guid threadId, string? agentMode)
    {
        var threadIdStr = threadId.ToString();

        try
        {
            // Get the current thread document
            var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc == null)
            {
                _logger.LogInternalWarning("Cannot update thread: Thread {ThreadId} not found", threadId);
                return null;
            }

            // Create updated thread document with new agent mode
            var updatedThreadDoc = threadDoc with
            {
                AgentMode = agentMode,
            };

            // Save the updated document
            var response = await _client.GetContainer<ThreadDocument>(_databaseName).ReplaceItemAsync(
                updatedThreadDoc,
                threadIdStr,
                new PartitionKey(threadIdStr)
            );

            // Update all agent contexts for this thread with the new agent mode
            var agentContexts = await GetAgentContextsForThreadAsync(threadId);
            foreach (var agentContext in agentContexts)
            {
                var updatedAgentContext = agentContext with { AgentMode = agentMode };
                await UpdateAgentContextAsync(updatedAgentContext);
            }

            // Get the updated thread with all of its data
            var updatedThread = await GetThreadAsync(threadId);
            return updatedThread;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning(ex, "Thread {ThreadId} not found during agent mode update", threadId);
            return null;
        }
    }

    public async Task<Thread?> UpdateThreadFavoriteAsync(Guid threadId, bool favorite)
    {
        var threadIdStr = threadId.ToString();

        try
        {
            // Get the current thread document
            var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);

            if (threadDoc == null)
            {
                _logger.LogInternalWarning("Cannot update thread's favorite property: Thread {ThreadId} not found", threadId);
                return null;
            }

            // Update the favorite property
            var updatedThreadDoc = threadDoc with
            {
                Favorite = favorite,
            };

            // Save the updated document
            await _client.GetContainer<ThreadDocument>(_databaseName).ReplaceItemAsync(
                updatedThreadDoc,
                updatedThreadDoc.Id,
                new PartitionKey(updatedThreadDoc.PartitionKey)
            );

            var updatedThread = await GetThreadAsync(threadId);

            // Return the complete updated Thread domain model
            _logger.LogInternalInformation("Successfully updated favorite property for thread {ThreadId}", threadId);
            return updatedThread;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Cannot update favorite property: Thread {ThreadId} not found", threadId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating favorite property for thread {ThreadId}", threadId);
            throw;
        }
    }

    #endregion

    #region Message Operations

    public async Task<Message?> GetMessageAsync(Guid threadId, Guid messageId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var messageIdStr = messageId.ToString();

            var messageDoc = await GetDocumentAsync<MessageDocument>(messageIdStr, threadIdStr);

            return messageDoc?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    public async Task<int> GetUnreadMessagesCountAsync(Guid threadId, DateTime? lastReadTime)
    {
        _logger.LogInternalInformation("Getting unread message count for thread: {Id}", threadId);

        try
        {
            var threadIdStr = threadId.ToString();

            // Build the query based on lastReadTime
            var query = _client.GetContainer<MessageDocument>(_databaseName).GetItemLinqQueryable<MessageDocument>()
                .Where(m => m.DocumentType == "Message" && m.ThreadId == threadIdStr);

            if (lastReadTime != null)
            {
                // Add filter for lastReadTime if it exists
                query = query.Where(m => m.TimeStamp > lastReadTime.Value);
            }

            query = query.OrderBy(m => m.TimeStamp);

            // Use ToFeedIterator and count asynchronously
            int count = await query.CountAsync();
            return count;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning(ex, "Thread not found when getting unread message count: {Id}", threadId);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting unread message count for thread: {Id}", threadId);
            throw;
        }
    }

    public async Task<IEnumerable<Message>> GetMessagesAsync(Guid threadId, ODataQueryOptions? queryOptions)
    {
        var messages = new List<Message>();
        var threadIdStr = threadId.ToString();

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
                // skip empty messages
                if (messageDoc.IsEmpty)
                {
                    continue;
                }

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

                    messageDocWithApproval = messageDoc with
                    {
                        Approval = approvalDoc?.ToDomainModel()
                    };
                }

                if (messageDoc.AzCliExecution != null)
                {
                    var executionQuery = _client.GetContainer<CliExecutionDocument>(_databaseName)
                        .GetItemLinqQueryable<CliExecutionDocument>()
                        .Where(e => e.Id == messageDoc.AzCliExecution.Id.ToString());

                    using var executionIterator = executionQuery.ToFeedIterator();
                    CliExecutionDocument? executionDoc = null;
                    if (executionIterator.HasMoreResults)
                    {
                        var executionResults = await executionIterator.ReadNextAsync();
                        executionDoc = executionResults.FirstOrDefault();
                    }

                    if (executionDoc != null)
                    {
                        messageDocWithApproval = messageDoc with
                        {
                            AzCliExecution = executionDoc.ToDomainModel(),
                            KubectlExecution = null,
                            PsqlExecution = null
                        };
                    }
                }

                if (messageDoc.KubectlExecution != null)
                {
                    var executionQuery = _client.GetContainer<KubectlExecutionDocument>(_databaseName)
                        .GetItemLinqQueryable<KubectlExecutionDocument>()
                        .Where(e => e.Id == messageDoc.KubectlExecution.Id.ToString());

                    using var executionIterator = executionQuery.ToFeedIterator();
                    KubectlExecutionDocument? executionDoc = null;
                    if (executionIterator.HasMoreResults)
                    {
                        var executionResults = await executionIterator.ReadNextAsync();
                        executionDoc = executionResults.FirstOrDefault();
                    }

                    if (executionDoc != null)
                    {
                        messageDocWithApproval = messageDoc with
                        {
                            AzCliExecution = null,
                            KubectlExecution = executionDoc.ToDomainModel()
                        };
                    }
                }

                if (messageDoc.PsqlExecution != null)
                {
                    var executionQuery = _client.GetContainer<CliExecutionDocument>(_databaseName)
                        .GetItemLinqQueryable<CliExecutionDocument>()
                        .Where(e => e.Id == messageDoc.PsqlExecution.Id.ToString());

                    using var executionIterator = executionQuery.ToFeedIterator();
                    CliExecutionDocument? executionDoc = null;
                    if (executionIterator.HasMoreResults)
                    {
                        var executionResults = await executionIterator.ReadNextAsync();
                        executionDoc = executionResults.FirstOrDefault();
                    }

                    if (executionDoc != null)
                    {
                        messageDocWithApproval = messageDoc with
                        {
                            AzCliExecution = null,
                            KubectlExecution = null,
                            PsqlExecution = executionDoc.ToPsqlDomainModel()
                        };
                    }
                }
                messages.Add(messageDocWithApproval.ToDomainModel(isDailyReport: messageDoc.IsDailyReport));
            }
        }

        return messages;
    }

    public async Task<IEnumerable<Message>> GetMessagesWithApprovalAsync(Guid threadId)
    {
        var messages = new List<Message>();
        var threadIdStr = threadId.ToString();

        var query = _client.GetContainer<MessageDocument>(_databaseName).GetItemLinqQueryable<MessageDocument>()
            .Where(m => m.DocumentType == "Message" && m.ThreadId == threadIdStr && m.Approval != null)
            .OrderBy(m => m.TimeStamp);

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

                    messageDocWithApproval = messageDoc with
                    {
                        Approval = approvalDoc?.ToDomainModel()
                    };
                }
                messages.Add(messageDocWithApproval.ToDomainModel(isDailyReport: messageDoc.IsDailyReport));
            }
        }

        return messages;
    }

    public async Task<IEnumerable<Message>> GetMessagesWithAzCliExecutionAsync(Guid threadId)
    {
        var messages = new List<Message>();
        var threadIdStr = threadId.ToString();

        var query = _client.GetContainer<MessageDocument>(_databaseName).GetItemLinqQueryable<MessageDocument>()
            .Where(m => m.DocumentType == "Message" && m.ThreadId == threadIdStr && m.AzCliExecution != null)
            .OrderBy(m => m.TimeStamp);

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var messageDoc in await iterator.ReadNextAsync())
            {
                var messageDocWithApproval = messageDoc;

                if (messageDoc.AzCliExecution != null)
                {
                    var executionQuery = _client.GetContainer<CliExecutionDocument>(_databaseName)
                        .GetItemLinqQueryable<CliExecutionDocument>()
                        .Where(e => e.Id == messageDoc.AzCliExecution.Id.ToString());

                    using var executionIterator = executionQuery.ToFeedIterator();
                    CliExecutionDocument? executionDoc = null;
                    if (executionIterator.HasMoreResults)
                    {
                        var executionResults = await executionIterator.ReadNextAsync();
                        executionDoc = executionResults.FirstOrDefault();
                    }

                    if (executionDoc != null)
                    {
                        messageDocWithApproval = messageDoc with
                        {
                            AzCliExecution = executionDoc.ToDomainModel(),
                            KubectlExecution = null
                        };
                    }
                }

                messages.Add(messageDocWithApproval.ToDomainModel(isDailyReport: messageDoc.IsDailyReport));
            }
        }

        return messages;
    }

    public async Task<IEnumerable<Message>> GetMessagesWithKubectlAsync(Guid threadId)
    {
        var messages = new List<Message>();
        var threadIdStr = threadId.ToString();

        var query = _client.GetContainer<MessageDocument>(_databaseName).GetItemLinqQueryable<MessageDocument>()
            .Where(m => m.DocumentType == "Message" && m.ThreadId == threadIdStr && m.KubectlExecution != null)
            .OrderBy(m => m.TimeStamp);

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var messageDoc in await iterator.ReadNextAsync())
            {
                var messageDocWithApproval = messageDoc;

                if (messageDoc.KubectlExecution != null)
                {
                    var executionQuery = _client.GetContainer<KubectlExecutionDocument>(_databaseName)
                        .GetItemLinqQueryable<KubectlExecutionDocument>()
                        .Where(e => e.Id == messageDoc.KubectlExecution.Id.ToString());

                    using var executionIterator = executionQuery.ToFeedIterator();
                    KubectlExecutionDocument? executionDoc = null;
                    if (executionIterator.HasMoreResults)
                    {
                        var executionResults = await executionIterator.ReadNextAsync();
                        executionDoc = executionResults.FirstOrDefault();
                    }

                    if (executionDoc != null)
                    {
                        messageDocWithApproval = messageDoc with
                        {
                            AzCliExecution = null,
                            KubectlExecution = executionDoc.ToDomainModel()
                        };
                    }
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
        {
            message = message with { Id = Guid.NewGuid() };
        }

        var threadIdStr = threadId.ToString();
        if (message.Posted == null)
        {
            message = message with { Posted = new Posted(false) };
        }

        // Create the message document
        MessageDocument messageDoc = MessageDocument.FromDomainModel(message, threadIdStr);

        var container = _client.GetContainer<MessageDocument>(_databaseName);

        await container.CreateItemAsync(messageDoc, new PartitionKey(messageDoc.PartitionKey));

        // Update the thread's modified timestamp
        try
        {
            var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);
            if (threadDoc != null)
            {
                var updatedThreadDoc = threadDoc with { ModifiedTimestamp = DateTime.UtcNow };
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
            var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);
            if (threadDoc != null)
            {
                var updatedThreadDoc = threadDoc with { LastMessageId = message.Id.ToString() };
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

    public async Task<Message?> UpdateMessageAsync(Guid threadId, Message message)
    {
        if (message.Id == Guid.Empty)
        {
            _logger.LogInternalError("Cannot update message: Message ID is empty");
            throw new ArgumentException("Message ID cannot be empty for update operation", nameof(message));
        }

        var threadIdStr = threadId.ToString();
        var messageIdStr = message.Id.ToString();

        try
        {
            // Check if the message exists
            var existingMessage = await GetDocumentAsync<MessageDocument>(messageIdStr, threadIdStr);
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

            // Update the thread's modified timestamp so that the incident cleanup timer doesn't close the issue when its taking longer to investigate
            try
            {
                var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);
                if (threadDoc != null)
                {
                    var updatedThreadDoc = threadDoc with { ModifiedTimestamp = DateTime.UtcNow };
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

    public async Task<Message?> UpdateMessageAsync(Guid threadId, Guid messageId, string newText, AgentTaskInfo? agentTaskInfo = null)
    {
        var threadIdStr = threadId.ToString();
        var messageIdStr = messageId.ToString();

        try
        {
            // Get the existing message
            var existingMessage = await GetDocumentAsync<MessageDocument>(messageIdStr, threadIdStr);
            if (existingMessage == null)
            {
                _logger.LogInternalWarning("Cannot update message: Message {MessageId} not found in thread {ThreadId}",
                    messageIdStr, threadIdStr);
                return null;
            }

            // Create updated message with new text and agent task info
            var updatedMessage = existingMessage.ToDomainModel() with
            {
                AgentTaskInfo = agentTaskInfo,
                Text = newText
            };

            // Convert back to document and update in database
            MessageDocument updatedMessageDoc = MessageDocument.FromDomainModel(updatedMessage, threadIdStr);

            var container = _client.GetContainer<MessageDocument>(_databaseName);
            await container.ReplaceItemAsync(
                updatedMessageDoc,
                messageIdStr,
                new PartitionKey(threadIdStr)
            );

            _logger.LogInternalInformation("Successfully updated message {MessageId} in thread {ThreadId} with new text and agent task info",
                messageIdStr, threadIdStr);

            return updatedMessage;
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
        var threadIdStr = threadId.ToString();
        var messageIdStr = messageId.ToString();

        try
        {
            // Check if this is a start message
            var threadDoc = await GetDocumentAsync<ThreadDocument>(threadIdStr, threadIdStr);
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
    public async Task<ThreadContext?> GetThreadContextAsync(Guid threadId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var threadContextDocId = ThreadContextDocument.GetId(threadIdStr);

            var threadContextDoc = await GetDocumentAsync<ThreadContextDocument>(threadContextDocId, threadContextDocId);

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

    public async Task<ThreadContext?> AddThreadContextAsync(ThreadContext threadContext)
    {
        // Ensure IDs are set
        if (threadContext.ThreadId == Guid.Empty)
        {
            threadContext = new ThreadContext(Guid.NewGuid(), threadContext.AgentTypeEnum);
        }

        // Then create the thread
        ThreadContextDocument threadContextDoc = ThreadContextDocument.FromDomainModel(threadContext);
        await _client.GetContainer<ThreadContextDocument>(_databaseName).CreateItemAsync(threadContextDoc, new PartitionKey(threadContextDoc.PartitionKey));

        return threadContext;
    }

    public async Task<ThreadContext?> UpdateThreadContextAsync(ThreadContext threadContext)
    {
        // Ensure IDs are set
        if (threadContext.ThreadId == Guid.Empty)
        {
            return null;
        }

        // Then create the thread
        ThreadContextDocument threadContextDoc = ThreadContextDocument.FromDomainModel(threadContext);
        await _client.GetContainer<ThreadContextDocument>(_databaseName).UpsertItemAsync(threadContextDoc, new PartitionKey(threadContextDoc.PartitionKey));

        return threadContext;
    }

    public async Task<bool> DeleteThreadContextAsync(Guid threadId)
    {
        var threadIdStr = threadId.ToString();
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
        var threadIdStr = threadId.ToString();

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

    public async Task<Action?> AddOrUpdateActionAsync(Guid threadId, Action action)
    {
        // Ensure ID is set
        if (action.Id == Guid.Empty)
        {
            action = action with { Id = Guid.NewGuid() };
        }

        var threadIdStr = threadId.ToString();

        // Create the action document
        ActionDocument actionDoc = ActionDocument.FromDomainModel(action, threadIdStr);
        await _client.GetContainer<ActionDocument>(_databaseName).UpsertItemAsync(actionDoc, new PartitionKey(actionDoc.PartitionKey));

        return action;
    }
    public async Task<Action?> GetActionAsync(Guid threadId, Guid actionId)
    {
        var threadIdStr = threadId.ToString();
        var actionIdStr = actionId.ToString();

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

    private async Task<T?> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            var response = await _client.GetContainer<T>(_databaseName).ReadItemAsync<T>(
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

    private async Task<(T? document, string? etag)> GetDocumentWithEtagAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            var response = await _client.GetContainer<T>(_databaseName).ReadItemAsync<T>(
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
        Status? status = null;

        // update Actions Status Properties for each thread
        var threadIdsWithCriticalActions = await GetThreadIdsWithActionSeverityAsync(ActionSeverity.Critical);
        var threadIdsWithWarningActions = await GetThreadIdsWithActionSeverityAsync(ActionSeverity.Warning);

        // Check if the thread has critical or warning actions
        var hasCriticalActions = threadIdsWithCriticalActions.Contains(thread.Id.ToString());
        var hasWarningActions = threadIdsWithWarningActions.Contains(thread.Id.ToString());

        status = new Status
        {
            ActionsStatus = new ActionsStatus
            {
                HasCriticalActions = hasCriticalActions,
                HasWarningActions = hasWarningActions
            },
            IncidentStatus = thread?.Status?.IncidentStatus
        };

        // add incident status
        if (thread?.Source == ThreadSource.Incident)
        {
            if (thread.Status != null && !string.IsNullOrEmpty(thread.IncidentSource?.IncidentId))
            {
                // check for incident in cosmos and apply status
                // check pager duty first
                var pagerDutyIncident = await GetDocumentAsync<PagerDutyIncidentDocument>(thread?.IncidentSource?.IncidentId ?? string.Empty, thread?.IncidentSource?.IncidentId ?? string.Empty);

                if (pagerDutyIncident != null)
                {
                    status.IncidentStatus = new IncidentStatus
                    {
                        IncidentId = thread?.IncidentSource?.IncidentId,
                        Status = pagerDutyIncident.Status
                    };
                }
                else
                {
                    // check azmon incident
                    var azMonIncident = await GetDocumentAsync<AzMonitorAlertDocument>(thread?.Status?.IncidentStatus?.IncidentId ?? string.Empty, thread?.Status?.IncidentStatus?.IncidentId ?? string.Empty);
                    if (azMonIncident != null)
                    {
                        status.IncidentStatus = new IncidentStatus
                        {
                            IncidentId = thread?.Status?.IncidentStatus?.IncidentId,
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

    public async Task<MessageFeedback?> GetMessageFeedbackAsync(Guid threadId, Guid messageFeedbackId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var messageFeedbackIdStr = messageFeedbackId.ToString();

            var messageFeedbackDoc = await GetDocumentAsync<MessageFeedbackDocument>(messageFeedbackIdStr, threadIdStr);

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
        var threadIdStr = threadId.ToString();

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

    public async Task<MessageFeedback?> GetMessageFeedbackNeedingRCAAsync()
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

    public async Task<MessageFeedback?> AddOrUpdateMessageFeedbackAsync(Guid threadId, MessageFeedback messageFeedback)
    {
        // Ensure ID is set
        if (messageFeedback.Id == Guid.Empty)
        {
            messageFeedback = messageFeedback with { Id = Guid.NewGuid() };
        }

        var threadIdStr = threadId.ToString();

        // Create the message document
        MessageFeedbackDocument messageFeedbackDoc = MessageFeedbackDocument.FromDomainModel(messageFeedback, threadIdStr);
        await _client.GetContainer<MessageFeedbackDocument>(_databaseName).UpsertItemAsync(messageFeedbackDoc, new PartitionKey(messageFeedbackDoc.PartitionKey));

        return messageFeedback;
    }

    public async Task<bool> DeleteMessageFeedbackAsync(Guid threadId, Guid messageFeedbackId)
    {
        var threadIdStr = threadId.ToString();
        var messageFeedbackIdStr = messageFeedbackId.ToString();

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
    public async Task<AgentContext> GetAgentContextAsync(Guid agentContextId, Guid threadId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var agentContextIdStr = agentContextId.ToString();

            var agentContextDocument = await GetDocumentAsync<AgentContextDocument>(agentContextIdStr, threadIdStr);

            var agentContext = agentContextDocument?.ToDomainModel();
            if (agentContext == null)
            {
                _logger.LogInternalError($"Error in getting AgentContext. Agent context is null, agentContextId: {agentContextId}, thread: {threadId}");
                throw new InvalidOperationException($"Error in getting AgentContext. Agent context is null, agentContextId: {agentContextId}, thread: {threadId}");
            }
            return agentContext;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalError($"Error in getting AgentContext. Agent context not found, agentContextId: {agentContextId}, thread: {threadId}");
            throw new InvalidOperationException($"Error in getting AgentContext. Agent context not found, agentContextId: {agentContextId}, thread: {threadId}");
        }
    }

    public async Task<IEnumerable<AgentContext>> GetAgentContextsForThreadAsync(Guid threadId)
    {
        var agentContexts = new List<AgentContext>();
        var threadIdStr = threadId.ToString();
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
            _logger.LogInternalError("Agent context has empty thread Id, error creating agent context.");
            throw new InvalidOperationException("Agent context has empty thread Id, error creating agent context.");
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
            _logger.LogInternalError("Agent context has empty thread Id, error updating agent context.");
            throw new InvalidOperationException("Agent context has empty thread Id, error updating agent context.");
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
            var batch = _client.GetContainer<AgentContextDocument>(_databaseName)
                .CreateTransactionalBatch(new PartitionKey(threadId.ToString()));

            batch.PatchItem(agentContextId.ToString(), [
                PatchOperation.Set(AgentContextDocument.AssignedInstancePatchPath, assignedInstanceId),
                PatchOperation.Set(AgentContextDocument.AssignmentExpiresPatchPath, expiration)
            ]);

            var response = await batch.ExecuteAsync();

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
        var threadIdStr = threadId.ToString();
        var agentContextIdStr = agentContextId.ToString();

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
    public async Task<ReasoningMessage?> GetReasoningMessageAsync(Guid reasoningMessageId, Guid agentContextId)
    {
        try
        {
            var agentContextIdStr = agentContextId.ToString();
            var reasoningMessageIdStr = reasoningMessageId.ToString();

            var reasoningMessageDocument = await GetDocumentAsync<ReasoningMessageDocument>(reasoningMessageIdStr, agentContextIdStr);

            return reasoningMessageDocument?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ReasoningMessage?> CreateReasoningMessageAsync(ReasoningMessage reasoningMessage)
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
        var agentContextIdStr = agentContextId.ToString();
        var reasoningMessageIdStr = reasoningMessageId.ToString();

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
    public async Task<AgentChatHistory?> GetAgentChatHistoryAsync(Guid agentContextId)
    {
        try
        {
            var agentContextIdStr = agentContextId.ToString();

            var agentChatHistoryDocument = await GetDocumentAsync<AgentChatHistoryDocument>(AgentChatHistoryDocument.GetDocumentId(agentContextIdStr), agentContextIdStr);

            return agentChatHistoryDocument?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<AgentChatHistory?> CreateAgentChatHistoryAsync(AgentChatHistory agentChatHistory)
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

    public async Task<AgentChatHistory?> UpdateAgentChatHistoryAsync(AgentChatHistory agentChatHistory)
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

    public async Task<AgentChatHistory?> AddReasoningMessagesToChatHistoryAsync(AgentChatHistory agentChatHistory, params IEnumerable<ReasoningMessage> reasoningMessages)
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
        for (var i = 0; i < retryLimit; i++)
        {
            try
            {
                foreach (var message in reasoningMessages)
                {
                    existingDocument?.ReasoningMessageIds.Add(message.Id.ToString());

                    if (message?.Role == ReasoningMessageRoleEnum.User && existingDocument != null)
                    {
                        existingDocument.LatestUserMessageId = message.Id.ToString();
                    }
                }

                var updateResponse = await _client.GetContainer<AgentChatHistoryDocument>(_databaseName).ReplaceItemAsync(
                    existingDocument,
                    existingDocument?.Id,
                    new PartitionKey(existingDocument?.PartitionKey),
                    new ItemRequestOptions { IfMatchEtag = etag }
                );

                return updateResponse?.Resource?.ToDomainModel();
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
        var agentContextIdStr = agentContextId.ToString();

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
    public async Task<ApprovalV2?> GetApprovalV2Async(Guid approvalIdV2, Guid agentContextId)
    {
        try
        {
            var approvalIdStr = approvalIdV2.ToString();
            var agentContextIdStr = agentContextId.ToString();

            var approvalV2Document = await GetDocumentAsync<ApprovalV2Document>(approvalIdStr, agentContextIdStr);

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

    public async Task<ApprovalV2?> CreateApprovalV2Async(ApprovalV2 approvalV2)
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

    public async Task<ApprovalV2?> UpdateApprovalV2Async(ApprovalV2 approvalV2)
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

    public async Task<bool> DeleteApprovalV2Async(Guid approvalIdV2, Guid agentContextId)
    {
        try
        {
            var id = approvalIdV2.ToString();
            var partitionKey = agentContextId.ToString();

            await _client.GetContainer<ApprovalV2Document>(_databaseName).DeleteItemAsync<ApprovalV2Document>(
                id,
                new PartitionKey(partitionKey)
            );

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<Approval?> CreateApprovalAsync(Approval approval)
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

    public async Task<Approval?> GetApprovalAsync(Guid threadId, Guid approvalId)
    {
        try
        {
            var id = approvalId.ToString();
            var partitionKey = threadId.ToString();

            var approvalDocument = await GetDocumentAsync<ApprovalDocument>(id, partitionKey);

            return approvalDocument?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // there might be multiple approvals with same title due to oboToken expiration
    // always get the approval with latest created timestamp
    public async Task<Approval?> GetApprovalAsync(Guid threadId, string title)
    {
        try
        {
            var partitionKey = threadId.ToString();

            var query = _client.GetContainer<ApprovalDocument>(_databaseName).GetItemLinqQueryable<ApprovalDocument>()
                .Where(d => d.Title == title && d.ThreadId == partitionKey)
                .OrderByDescending(d => d.CreatedTimestamp)
                .Take(1);

            using var iterator = query.ToFeedIterator();
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
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Approval?> UpdateApprovalAsync(Approval approval)
    {
        var approvalDocument = ApprovalDocument.FromDomainModel(approval);
        await _client.GetContainer<ApprovalDocument>(_databaseName).UpsertItemAsync(approvalDocument, new PartitionKey(approvalDocument.PartitionKey));

        return approval;
    }

    public async Task<IList<Approval>> GetApprovalsAsync(Guid threadId)
    {
        try
        {
            var partitionKey = threadId.ToString();

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
            return new List<Approval>();
        }
    }

    public async Task<bool> DeleteApprovalAsync(Guid threadId, Guid approvalId)
    {
        try
        {
            var id = approvalId.ToString();
            var partitionKey = threadId.ToString();

            await _client.GetContainer<ApprovalDocument>(_databaseName).DeleteItemAsync<ApprovalDocument>(
                id,
                new PartitionKey(partitionKey)
            );

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    #endregion

    #region GitHubAccessToken Operations
    public async Task<GitHubAccessToken?> GetGitHubAccessTokenAsync()
    {
        try
        {
            var gitHubAccessTokenDocument = await GetDocumentAsync<GitHubAccessTokenDocument>("GitHubAccessToken", "GitHubAccessToken");
            return gitHubAccessTokenDocument?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<GitHubAccessToken?> CreateOrUpdateGitHubAccessTokenAsync(GitHubAccessToken gitHubAccessToken)
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

    #region AzureDevOps

    public async Task<AzureDevOpsAccessToken?> GetAzureDevOpsAccessTokenAsync(string resourceId)
    {
        try
        {
            var azureDevOpsAccessTokenDocument = await GetDocumentAsync<AzureDevOpsAccessTokenDocument>($"{AzureDevOpsAccessTokenDocument.KeyName}_{resourceId}_{AzureDevOpsAccessTokenDocument._sessionGuid}", AzureDevOpsAccessTokenDocument.KeyName);
            return azureDevOpsAccessTokenDocument?.ToDomainModel();
        }

        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<AzureDevOpsAccessToken?> CreateOrUpdateAzureDevOpsAccessTokenAsync(AzureDevOpsAccessToken azureDevOpsAccessToken, string resourceId)
    {
        // Create the Azure DevOps access token document
        AzureDevOpsAccessTokenDocument azureDevOpsAccessDoc = AzureDevOpsAccessTokenDocument.FromDomainModel(azureDevOpsAccessToken, resourceId);
        await _client.GetContainer<AzureDevOpsAccessTokenDocument>(_databaseName).UpsertItemAsync(azureDevOpsAccessDoc, new PartitionKey(azureDevOpsAccessDoc.PartitionKey));
        return azureDevOpsAccessToken;
    }

    public async Task<bool> DeleteAzureDevOpsAccessTokenAsync(string resourceId)
    {
        try
        {
            // Delete the Azure DevOps access token
            await _client.GetContainer<AzureDevOpsAccessTokenDocument>(_databaseName).DeleteItemAsync<AzureDevOpsAccessTokenDocument>($"{AzureDevOpsAccessTokenDocument.KeyName}_{resourceId}_{AzureDevOpsAccessTokenDocument._sessionGuid}", new PartitionKey(AzureDevOpsAccessTokenDocument.KeyName));
            return true;
        }

        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    #endregion

    #region AzCliExecution Operations

    public async Task<AzCliExecution?> GetAzCliExecutionAsync(Guid threadId, Guid executionId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var executionIdStr = executionId.ToString();

            var executionDoc = await GetDocumentAsync<CliExecutionDocument>(executionIdStr, threadIdStr);

            return executionDoc?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<AzCliExecution?> CreateAzCliExecutionAsync(Guid threadId, AzCliExecution execution)
    {
        // Ensure ID is set
        if (execution.Id == Guid.Empty)
        {
            execution = execution with { Id = Guid.NewGuid() };
        }

        var threadIdStr = threadId.ToString();

        // Create the execution document
        CliExecutionDocument executionDoc = CliExecutionDocument.FromDomainModel(execution, threadIdStr);
        await _client.GetContainer<CliExecutionDocument>(_databaseName).CreateItemAsync(executionDoc, new PartitionKey(executionDoc.PartitionKey));

        return execution;
    }

    public async Task<AzCliExecution?> UpdateAzCliExecutionAsync(Guid threadId, AzCliExecution execution)
    {
        var threadIdStr = threadId.ToString();

        var executionDoc = CliExecutionDocument.FromDomainModel(execution, threadIdStr);
        await _client.GetContainer<CliExecutionDocument>(_databaseName).UpsertItemAsync(executionDoc, new PartitionKey(executionDoc.PartitionKey));

        return execution;
    }

    public async Task<AzCliExecution?> UpdateAzCliExecutionOutputAsync(Guid threadId, Guid executionId, string output, string? error = null)
    {
        var threadIdStr = threadId.ToString();
        var executionIdStr = executionId.ToString();

        try
        {
            var executionDoc = await GetDocumentAsync<CliExecutionDocument>(executionIdStr, threadIdStr);
            if (executionDoc == null)
            {
                return null;
            }

            var updatedDoc = executionDoc with
            {
                Output = output,
                Error = error,
                Status = error != null ? AzCliExecutionStatus.Failed : executionDoc.Status
            };

            await _client.GetContainer<CliExecutionDocument>(_databaseName).ReplaceItemAsync(
                updatedDoc,
                updatedDoc.Id,
                new PartitionKey(updatedDoc.PartitionKey)
            );

            return updatedDoc.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<AzCliExecution?> ListPendingAzCliExecutionAsync(Guid threadId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var pendingExecutions = new List<AzCliExecution>();

            var query = _client.GetContainer<CliExecutionDocument>(_databaseName).GetItemLinqQueryable<CliExecutionDocument>()
                .Where(m => m.DocumentType == "CliExecution" && m.ThreadId == threadIdStr && (m.Status == AzCliExecutionStatus.Pending || m.Status == AzCliExecutionStatus.PendingAuthorization));
            using var iterator = query.ToFeedIterator();

            while (iterator.HasMoreResults)
            {
                foreach (var executionDocument in await iterator.ReadNextAsync())
                {
                    pendingExecutions.Add(executionDocument.ToDomainModel());
                }
            }

            return pendingExecutions.FirstOrDefault();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    #endregion

    #region KubectlExecution Operations

    public async Task<KubectlExecution?> GetKubectlExecutionAsync(Guid threadId, Guid executionId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var executionIdStr = executionId.ToString();

            var executionDoc = await GetDocumentAsync<KubectlExecutionDocument>(executionIdStr, threadIdStr);

            return executionDoc?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<KubectlExecution?> CreateKubectlExecutionAsync(Guid threadId, KubectlExecution execution)
    {
        // Ensure ID is set
        if (execution.Id == Guid.Empty)
        {
            execution = execution with { Id = Guid.NewGuid() };
        }

        var threadIdStr = threadId.ToString();

        // Create the execution document
        KubectlExecutionDocument executionDoc = KubectlExecutionDocument.FromDomainModel(execution, threadIdStr);
        await _client.GetContainer<KubectlExecutionDocument>(_databaseName).CreateItemAsync(executionDoc, new PartitionKey(executionDoc.PartitionKey));

        return execution;
    }

    public async Task<KubectlExecution?> UpdateKubectlExecutionAsync(Guid threadId, KubectlExecution execution)
    {
        var threadIdStr = threadId.ToString();

        var executionDoc = KubectlExecutionDocument.FromDomainModel(execution, threadIdStr);
        await _client.GetContainer<KubectlExecutionDocument>(_databaseName).UpsertItemAsync(executionDoc, new PartitionKey(executionDoc.PartitionKey));

        return execution;
    }

    public async Task<KubectlExecution?> UpdateKubectlExecutionOutputAsync(Guid threadId, Guid executionId, string output, string? error = null)
    {
        var threadIdStr = threadId.ToString();
        var executionIdStr = executionId.ToString();

        try
        {
            var executionDoc = await GetDocumentAsync<KubectlExecutionDocument>(executionIdStr, threadIdStr);
            if (executionDoc == null)
            {
                return null;
            }

            var updatedDoc = executionDoc with
            {
                Output = output,
                Error = error,
                Status = error != null ? KubectlExecutionStatus.Failed : executionDoc.Status
            };

            await _client.GetContainer<KubectlExecutionDocument>(_databaseName).ReplaceItemAsync(
                updatedDoc,
                updatedDoc.Id,
                new PartitionKey(updatedDoc.PartitionKey)
            );

            return updatedDoc.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<KubectlExecution?> ListPendingKubectlExecutionAsync(Guid threadId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var pendingExecutions = new List<KubectlExecution>();

            var query = _client.GetContainer<KubectlExecutionDocument>(_databaseName).GetItemLinqQueryable<KubectlExecutionDocument>()
                .Where(m => m.DocumentType == "KubectlExecution" && m.ThreadId == threadIdStr && (m.Status == KubectlExecutionStatus.Pending || m.Status == KubectlExecutionStatus.PendingAuthorization));
            using var iterator = query.ToFeedIterator();

            while (iterator.HasMoreResults)
            {
                foreach (var executionDocument in await iterator.ReadNextAsync())
                {
                    pendingExecutions.Add(executionDocument.ToDomainModel());
                }
            }

            return pendingExecutions.FirstOrDefault();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    #endregion

    #region PsqlExecution Operations

    public async Task<PsqlExecution?> GetPsqlExecutionAsync(Guid threadId, Guid executionId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var executionIdStr = executionId.ToString();

            var executionDoc = await GetDocumentAsync<CliExecutionDocument>(executionIdStr, threadIdStr);

            return executionDoc?.ToPsqlDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<PsqlExecution?> CreatePsqlExecutionAsync(Guid threadId, PsqlExecution execution)
    {
        // Ensure ID is set
        if (execution.Id == Guid.Empty)
        {
            execution = execution with { Id = Guid.NewGuid() };
        }

        var threadIdStr = threadId.ToString();
        // Create the execution document
        CliExecutionDocument executionDoc = CliExecutionDocument.FromDomainModel(execution, threadIdStr);
        await _client.GetContainer<CliExecutionDocument>(_databaseName).CreateItemAsync(executionDoc, new PartitionKey(executionDoc.PartitionKey));

        return execution;
    }

    public async Task<PsqlExecution?> UpdatePsqlExecutionAsync(Guid threadId, PsqlExecution execution)
    {
        var threadIdStr = threadId.ToString();

        var executionDoc = CliExecutionDocument.FromDomainModel(execution, threadIdStr);
        await _client.GetContainer<CliExecutionDocument>(_databaseName).UpsertItemAsync(executionDoc, new PartitionKey(executionDoc.PartitionKey));

        return execution;
    }

    public async Task<PsqlExecution?> UpdatePsqlExecutionOutputAsync(Guid threadId, Guid executionId, string output, string? error = null)
    {
        var threadIdStr = threadId.ToString();
        var executionIdStr = executionId.ToString();

        try
        {
            var executionDoc = await GetDocumentAsync<CliExecutionDocument>(executionIdStr, threadIdStr);
            if (executionDoc == null)
            {
                return null;
            }

            var updatedDoc = executionDoc with
            {
                Output = output,
                Error = error,
                Status = error != null ? AzCliExecutionStatus.Failed : executionDoc.Status
            };

            await _client.GetContainer<CliExecutionDocument>(_databaseName).ReplaceItemAsync(
                updatedDoc,
                updatedDoc.Id,
                new PartitionKey(updatedDoc.PartitionKey)
            );

            return updatedDoc.ToPsqlDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<PsqlExecution?> ListPendingPsqlExecutionAsync(Guid threadId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var pendingExecutions = new List<PsqlExecution>();

            var query = _client.GetContainer<CliExecutionDocument>(_databaseName).GetItemLinqQueryable<CliExecutionDocument>()
                .Where(m => m.DocumentType == "CliExecution" && m.ThreadId == threadIdStr && (m.Status == AzCliExecutionStatus.Pending || m.Status == AzCliExecutionStatus.PendingAuthorization));
            using var iterator = query.ToFeedIterator();

            while (iterator.HasMoreResults)
            {
                foreach (var executionDocument in await iterator.ReadNextAsync())
                {
                    pendingExecutions.Add(executionDocument.ToPsqlDomainModel());
                }
            }

            return pendingExecutions.FirstOrDefault();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<Message>> GetMessagesWithPsqlAsync(Guid threadId)
    {
        var messages = new List<Message>();
        var threadIdStr = threadId.ToString();

        var query = _client.GetContainer<MessageDocument>(_databaseName).GetItemLinqQueryable<MessageDocument>()
            .Where(m => m.DocumentType == "Message" && m.ThreadId == threadIdStr && m.AzCliExecution != null)
            .OrderBy(m => m.TimeStamp);

        using var iterator = query.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            foreach (var messageDoc in await iterator.ReadNextAsync())
            {
                var messageDocWithApproval = messageDoc;

                if (messageDoc.PsqlExecution != null)
                {
                    var executionQuery = _client.GetContainer<CliExecutionDocument>(_databaseName)
                        .GetItemLinqQueryable<CliExecutionDocument>()
                        .Where(e => e.Id == messageDoc.PsqlExecution.Id.ToString());

                    using var executionIterator = executionQuery.ToFeedIterator();
                    CliExecutionDocument? executionDoc = null;
                    if (executionIterator.HasMoreResults)
                    {
                        var executionResults = await executionIterator.ReadNextAsync();
                        executionDoc = executionResults.FirstOrDefault();
                    }

                    if (executionDoc != null)
                    {
                        messageDocWithApproval = messageDoc with
                        {
                            AzCliExecution = null,
                            KubectlExecution = null,
                            PsqlExecution = executionDoc.ToPsqlDomainModel()
                        };
                    }
                }

                messages.Add(messageDocWithApproval.ToDomainModel(isDailyReport: messageDoc.IsDailyReport));
            }
        }

        return messages;
    }

    #endregion

    #region ThreadEvaluateResult Operations

    public async Task<ThreadEvaluateResult?> GetThreadEvaluateResultAsync(Guid evaluationId)
    {
        _logger.LogInternalInformation("Trying to get thread evaluation: {Id}", evaluationId);
        try
        {
            var evaluationIdStr = evaluationId.ToString();

            // Since we don't know the thread ID (partition key) from just the evaluation ID,
            // we need to query across all partitions using a cross-partition query
            var query = _client.GetContainer<ThreadEvaluateResultDocument>(_databaseName)
                .GetItemLinqQueryable<ThreadEvaluateResultDocument>(allowSynchronousQueryExecution: true)
                .Where(e => e.DocumentType == "ThreadEvaluationResult" && e.Id == evaluationIdStr);

            using var iterator = query.ToFeedIterator();
            var response = await iterator.ReadNextAsync();

            return response.FirstOrDefault()?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ThreadEvaluateResult?> GetThreadEvaluateResultByThreadIdAsync(Guid threadId)
    {
        _logger.LogInternalInformation("Trying to get thread evaluation by thread ID: {ThreadId}", threadId);
        try
        {
            var threadIdStr = threadId.ToString();

            // Query for evaluation by thread ID
            var query = _client.GetContainer<ThreadEvaluateResultDocument>(_databaseName)
                .GetItemLinqQueryable<ThreadEvaluateResultDocument>()
                .Where(e => e.DocumentType == "ThreadEvaluationResult" && e.ThreadId == threadIdStr)
                .OrderByDescending(e => e.EvaluatedTimestamp)
                .Take(1);

            using var iterator = query.ToFeedIterator();
            var response = await iterator.ReadNextAsync();

            return response.FirstOrDefault()?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<ThreadEvaluateResult>> GetThreadEvaluateResultsAsync(ODataQueryOptions? queryOptions = null)
    {
        _logger.LogInternalInformation("Getting all thread evaluations");

        var results = new List<ThreadEvaluateResult>();

        // Query for thread evaluation documents
        var query = _client.GetContainer<ThreadEvaluateResultDocument>(_databaseName)
            .GetItemLinqQueryable<ThreadEvaluateResultDocument>()
            .Where(e => e.DocumentType == "ThreadEvaluationResult")
            .OrderBy(e => e.EvaluatedTimestamp);

        var finalQuery = queryOptions?.ApplyTo(query, oDataQuerySettings) as IOrderedQueryable<ThreadEvaluateResultDocument> ?? query;

        using var iterator = finalQuery.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var evaluationDoc in await iterator.ReadNextAsync())
            {
                results.Add(evaluationDoc.ToDomainModel());
            }
        }

        return results;
    }

    public async Task<ThreadEvaluateResult?> CreateThreadEvaluateResultAsync(ThreadEvaluateResult evaluateResult)
    {
        _logger.LogInternalInformation("Creating thread evaluation: {Id}", evaluateResult.Id);

        // Ensure ID is set
        var resultToStore = evaluateResult;
        if (evaluateResult.Id == Guid.Empty)
        {
            resultToStore = evaluateResult with { Id = Guid.NewGuid() };
        }

        var document = ThreadEvaluateResultDocument.FromDomainModel(resultToStore);

        await _client.GetContainer<ThreadEvaluateResultDocument>(_databaseName).UpsertItemAsync(document, new PartitionKey(document.PartitionKey));

        return resultToStore;
    }

    public async Task<ThreadEvaluateResult?> UpdateThreadEvaluateResultAsync(ThreadEvaluateResult evaluateResult)
    {
        _logger.LogInternalInformation("Updating thread evaluation: {Id}", evaluateResult.Id);

        try
        {
            // Check if the document exists first
            var evaluationIdStr = evaluateResult.Id.ToString();
            var existingDoc = await GetDocumentAsync<ThreadEvaluateResultDocument>(evaluationIdStr, evaluateResult.ThreadId.ToString());

            if (existingDoc == null)
            {
                _logger.LogInternalWarning("Cannot update thread evaluation: {Id} not found", evaluateResult.Id);
                return null;
            }

            var document = ThreadEvaluateResultDocument.FromDomainModel(evaluateResult);
            await _client.GetContainer<ThreadEvaluateResultDocument>(_databaseName).UpsertItemAsync(document, new PartitionKey(document.PartitionKey));

            return evaluateResult;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Cannot update thread evaluation: {Id} not found", evaluateResult.Id);
            return null;
        }
    }

    public async Task<bool> DeleteThreadEvaluateResultAsync(Guid evaluationId)
    {
        _logger.LogInternalInformation("Deleting thread evaluation: {Id}", evaluationId);

        try
        {
            var evaluationIdStr = evaluationId.ToString();

            // First, find the document using cross-partition query to get the correct partition key (ThreadId)
            var query = _client.GetContainer<ThreadEvaluateResultDocument>(_databaseName)
                .GetItemLinqQueryable<ThreadEvaluateResultDocument>(allowSynchronousQueryExecution: true)
                .Where(e => e.DocumentType == "ThreadEvaluationResult" && e.Id == evaluationIdStr);

            using var iterator = query.ToFeedIterator();
            var response = await iterator.ReadNextAsync();
            var evaluationDoc = response.FirstOrDefault();

            if (evaluationDoc == null)
            {
                _logger.LogInternalWarning("Thread evaluation not found: {Id}", evaluationId);
                return false;
            }

            // Now delete using the correct partition key
            await _client.GetContainer<ThreadEvaluateResultDocument>(_databaseName)
                .DeleteItemAsync<ThreadEvaluateResultDocument>(evaluationIdStr, new PartitionKey(evaluationDoc.ThreadId));

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Thread evaluation not found: {Id}", evaluationId);
            return false;
        }
    }

    #endregion

    #region Agent Task Operations
    public async Task<bool> UpdateTaskOnThreadAsync(Guid threadId, AgentTaskShort task)
    {
        var threadIdStr = threadId.ToString();
        const int retryLimit = 3;
        for (var i = 0; i < retryLimit; i++)
        {
            var (threadDoc, etag) = await GetDocumentWithEtagAsync<ThreadDocument>(threadIdStr, threadIdStr);
            if (threadDoc == null)
            {
                _logger.LogInternalWarning("Cannot update tasks: Thread {ThreadId} not found", threadId);
                return false;
            }

            var agentTasks = (threadDoc.AgentTasks ?? []).ToList();
            var existingIndex = agentTasks.FindIndex(t => t.Id == task.Id);
            if (existingIndex >= 0)
            {
                agentTasks[existingIndex] = task;
            }
            else
            {
                agentTasks.Add(task);
            }

            var updatedThreadDoc = threadDoc with
            {
                AgentTasks = agentTasks,
                ModifiedTimestamp = DateTime.UtcNow
            };

            try
            {
                await _client.GetContainer<ThreadDocument>(_databaseName).ReplaceItemAsync(
                    updatedThreadDoc,
                    updatedThreadDoc.Id,
                    new PartitionKey(updatedThreadDoc.PartitionKey),
                    new ItemRequestOptions { IfMatchEtag = etag }
                );
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                if (i == retryLimit - 1)
                {
                    _logger.LogInternalError(ex, "Failed to update tasks for thread {ThreadId} after {RetryCount} attempts", threadId, retryLimit);
                    return false;
                }
                continue;
            }
        }
        return false;
    }

    #region Bulk Deletion Methods for Thread Cleanup

    public async Task<bool> DeleteAllCliExecutionsAsync(Guid threadId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var container = _client.GetContainer<CliExecutionDocument>(_databaseName);

            var query = container.GetItemLinqQueryable<CliExecutionDocument>()
                .Where(d => d.DocumentType == "CliExecution" && d.ThreadId == threadIdStr);

            using var iterator = query.ToFeedIterator();

            while (iterator.HasMoreResults)
            {
                foreach (var execution in await iterator.ReadNextAsync())
                {
                    await container.DeleteItemAsync<CliExecutionDocument>(
                        execution.Id,
                        new PartitionKey(execution.PartitionKey)
                    );
                }
            }

            return true;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to delete CLI executions for thread {ThreadId}", threadId);
            return false;
        }
    }

    public async Task<bool> DeleteAllKubectlExecutionsAsync(Guid threadId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var container = _client.GetContainer<KubectlExecutionDocument>(_databaseName);

            var query = container.GetItemLinqQueryable<KubectlExecutionDocument>()
                .Where(d => d.DocumentType == "KubectlExecution" && d.ThreadId == threadIdStr);

            using var iterator = query.ToFeedIterator();

            while (iterator.HasMoreResults)
            {
                foreach (var execution in await iterator.ReadNextAsync())
                {
                    await container.DeleteItemAsync<KubectlExecutionDocument>(
                        execution.Id,
                        new PartitionKey(execution.PartitionKey)
                    );
                }
            }

            return true;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to delete Kubectl executions for thread {ThreadId}", threadId);
            return false;
        }
    }

    public async Task<bool> DeleteAllPsqlExecutionsAsync(Guid threadId)
    {
        // PSQL executions are stored in the same CliExecutionDocument as AzCli executions
        // Since there's no way to distinguish them, we'll handle both in DeleteAllCliExecutionsAsync
        // This method is kept for interface compatibility but delegates to CLI deletion
        return await DeleteAllCliExecutionsAsync(threadId);
    }

    public async Task<bool> DeleteAllScheduledTasksAsync(Guid threadId)
    {
        try
        {
            var threadIdStr = threadId.ToString();
            var container = _client.GetContainer<ScheduledTaskDocument>(_databaseName);

            var query = container.GetItemLinqQueryable<ScheduledTaskDocument>()
                .Where(d => d.DocumentType == "ScheduledTask" && d.ThreadId == threadIdStr);

            using var iterator = query.ToFeedIterator();

            while (iterator.HasMoreResults)
            {
                foreach (var task in await iterator.ReadNextAsync())
                {
                    await container.DeleteItemAsync<ScheduledTaskDocument>(
                        task.Id,
                        new PartitionKey(task.PartitionKey)
                    );
                }
            }

            return true;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to delete scheduled tasks for thread {ThreadId}", threadId);
            return false;
        }
    }

    #endregion

    #endregion

    #region Todo Plan Operations

    public async Task<IReadOnlyList<TodoPlan>> GetTodoPlansAsync(Guid threadId)
    {
        _logger.LogInternalInformation("Getting todo plans for thread: {ThreadId}", threadId);

        var results = new List<TodoPlan>();
        var threadIdStr = threadId.ToString();

        var query = _client.GetContainer<TodoPlanDocument>(_databaseName)
            .GetItemLinqQueryable<TodoPlanDocument>()
            .Where(p => p.DocumentType == "TodoPlan" && p.ThreadId == threadIdStr)
            .OrderByDescending(p => p.CreatedAt);

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var planDoc in await iterator.ReadNextAsync())
            {
                results.Add(planDoc.ToDomainModel());
            }
        }

        return results;
    }

    public async Task<TodoPlan?> GetTodoPlanAsync(Guid threadId, Guid planId)
    {
        _logger.LogInternalInformation("Getting todo plan {PlanId} for thread: {ThreadId}", planId, threadId);
        try
        {
            var threadIdStr = threadId.ToString();
            var planIdStr = planId.ToString();

            var planDoc = await GetDocumentAsync<TodoPlanDocument>(planIdStr, threadIdStr);
            return planDoc?.ToDomainModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TodoPlan> CreateTodoPlanAsync(TodoPlan plan)
    {
        _logger.LogInternalInformation("Creating todo plan: {PlanId} for thread: {ThreadId}", plan.Id, plan.ThreadId);

        var planToStore = plan;
        if (plan.Id == Guid.Empty)
        {
            planToStore = plan with { Id = Guid.NewGuid() };
        }

        if (planToStore.CreatedAt == default)
        {
            planToStore = planToStore with { CreatedAt = DateTime.UtcNow };
        }

        var document = TodoPlanDocument.FromDomainModel(planToStore);
        await _client.GetContainer<TodoPlanDocument>(_databaseName).CreateItemAsync(document, new PartitionKey(document.PartitionKey));

        return planToStore;
    }

    public async Task<TodoPlan> UpdateTodoPlanAsync(TodoPlan plan)
    {
        _logger.LogInternalInformation("Updating todo plan: {PlanId} for thread: {ThreadId}", plan.Id, plan.ThreadId);

        var planToStore = plan with { LastUpdated = DateTime.UtcNow };

        var document = TodoPlanDocument.FromDomainModel(planToStore);
        await _client.GetContainer<TodoPlanDocument>(_databaseName).UpsertItemAsync(document, new PartitionKey(document.PartitionKey));

        return planToStore;
    }

    #endregion
}

