// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services.IncidentTriggerDetection;

/// <summary>
/// Service for looking up existing threads for incidents.
/// Wraps Cosmos DB queries to find threads by incident ID.
/// </summary>
public class IncidentThreadLookupService : IIncidentThreadLookupService
{
    private readonly Container _container;
    private readonly ILogger<IncidentThreadLookupService> _logger;

    public IncidentThreadLookupService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        ILogger<IncidentThreadLookupService> logger)
    {
        _container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ThreadDocument?> FindThreadByIncidentIdAsync(string incidentId)
    {
        _logger.LogInternalInformation("[ThreadLookup] Searching for thread with incident ID {IncidentId}", incidentId);

        try
        {
            var threads = _container.GetItemLinqQueryable<ThreadDocument>()
                .Where(doc => doc.DocumentType == "Thread" && doc.Source == ThreadSource.Incident)
                .Where(doc => (doc.IncidentSource != null &&
                               doc.IncidentSource.IncidentType == IncidentType.Icm &&
                               doc.IncidentSource.IncidentId == incidentId) ||
                              doc.IncidentId == incidentId)
                .OrderByDescending(doc => doc.CreatedTimestamp)
                .ToFeedIterator();

            if (threads.HasMoreResults)
            {
                var response = await threads.ReadNextAsync();
                var thread = response.FirstOrDefault();

                if (thread != null)
                {
                    _logger.LogInternalInformation("[ThreadLookup] Found thread {ThreadId} for incident {IncidentId}",
                        thread.Id, incidentId);

                    if (response.Count > 1)
                    {
                        _logger.LogInternalWarning(
                            "[ThreadLookup] Multiple threads found for incident {IncidentId}, using latest: {ThreadId}",
                            incidentId, thread.Id);
                    }
                }

                return thread;
            }

            _logger.LogInternalInformation("[ThreadLookup] No thread found for incident {IncidentId}", incidentId);
            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("[ThreadLookup] No thread found for incident {IncidentId}", incidentId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[ThreadLookup] Error looking up thread for incident {IncidentId}", incidentId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ShouldReuseThreadAsync(string incidentId, string handlerId)
    {
        // Phase 2: Thread is keyed by (incident, handler)
        // Use handler-based lookup for multi-agent support
        var existingThread = await FindThreadByIncidentAndHandlerAsync(incidentId, handlerId);

        if (existingThread == null)
        {
            return false;
        }

        _logger.LogInternalInformation(
            "[ThreadLookup] Thread {ThreadId} exists for incident {IncidentId} with handler {HandlerId}, will reuse",
            existingThread.Id, incidentId, handlerId);

        return true;
    }

    /// <inheritdoc />
    public async Task<ThreadDocument?> FindThreadByIncidentAndHandlerAsync(string incidentId, string handlerId)
    {
        _logger.LogInternalInformation("[ThreadLookup] Searching for thread with incident ID {IncidentId} and handler {HandlerId}",
            incidentId, handlerId);

        try
        {
            // Query threads where incident matches AND handler matches
            var threads = _container.GetItemLinqQueryable<ThreadDocument>()
                .Where(doc => doc.DocumentType == "Thread" && doc.Source == ThreadSource.Incident)
                .Where(doc => (doc.IncidentSource != null &&
                               doc.IncidentSource.IncidentType == IncidentType.Icm &&
                               doc.IncidentSource.IncidentId == incidentId) ||
                              doc.IncidentId == incidentId)
                .Where(doc => doc.IncidentDetails != null && doc.IncidentDetails.HandlerId == handlerId)
                .OrderByDescending(doc => doc.CreatedTimestamp)
                .ToFeedIterator();

            if (threads.HasMoreResults)
            {
                var response = await threads.ReadNextAsync();
                var thread = response.FirstOrDefault();

                if (thread != null)
                {
                    _logger.LogInternalInformation("[ThreadLookup] Found thread {ThreadId} for incident {IncidentId} with handler {HandlerId}",
                        thread.Id, incidentId, handlerId);
                }

                return thread;
            }

            _logger.LogInternalInformation("[ThreadLookup] No thread found for incident {IncidentId} with handler {HandlerId}",
                incidentId, handlerId);
            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("[ThreadLookup] No thread found for incident {IncidentId} with handler {HandlerId}",
                incidentId, handlerId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[ThreadLookup] Error looking up thread for incident {IncidentId} with handler {HandlerId}",
                incidentId, handlerId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ThreadDocument>> FindAllThreadsForIncidentAsync(string incidentId)
    {
        _logger.LogInternalInformation("[ThreadLookup] Searching for all threads with incident ID {IncidentId}", incidentId);
        var results = new List<ThreadDocument>();

        try
        {
            var threads = _container.GetItemLinqQueryable<ThreadDocument>()
                .Where(doc => doc.DocumentType == "Thread" && doc.Source == ThreadSource.Incident)
                .Where(doc => (doc.IncidentSource != null &&
                               doc.IncidentSource.IncidentType == IncidentType.Icm &&
                               doc.IncidentSource.IncidentId == incidentId) ||
                              doc.IncidentId == incidentId)
                .OrderByDescending(doc => doc.CreatedTimestamp)
                .ToFeedIterator();

            while (threads.HasMoreResults)
            {
                var response = await threads.ReadNextAsync();
                results.AddRange(response);
            }

            _logger.LogInternalInformation("[ThreadLookup] Found {Count} thread(s) for incident {IncidentId}",
                results.Count, incidentId);

            return results;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("[ThreadLookup] No threads found for incident {IncidentId}", incidentId);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[ThreadLookup] Error looking up threads for incident {IncidentId}", incidentId);
            throw;
        }
    }
}
