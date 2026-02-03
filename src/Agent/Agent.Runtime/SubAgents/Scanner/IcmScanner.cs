// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Logging;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services;
using Agent.Runtime.Services.IncidentTriggerDetection;
using Agent.Runtime.SubAgents.IcmScanner;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.SREAgent.Incidents.IcM.Model;
using IncidentStatus = Microsoft.AzureAd.Icm.Types.IncidentStatus;

namespace Agent.Runtime.SubAgents.Scanner;

/// <summary>
/// Represents the information logged when an incident is resolved or mitigated
/// </summary>
public record IncidentResolveActionData
{
    public string IncidentSource { get; init; } = string.Empty;
    public string IncidentId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ResolvedBy { get; init; } = string.Empty;
    public string AgentMode { get; init; } = string.Empty;
}

#pragma warning disable CS9113 // Parameter is unread - kept for future use when NotifyUserAsync is restored
public class IcmScanner(ILogger<IcmScanner> logger,
    IICMAPIClient icmApiClient,
    CosmosClient cosmosClient,
    CosmosDBSettings cosmosDbSettings,
    IIncidentHandlingService<IcmIncidentFilterDocumentPayload> incidentHandlingService,
    IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload> incidentManagementService,
    IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload> incidentFilterManagementService,
    IAgentInboundCommunicationService agentInboundCommunicationService,
    IncidentManagementSettings incidentManagementSettings,
    IIncidentAnalysisService<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload, ICMIncident> incidentAnalysisService,
    IIncidentEventDetector incidentEventDetector,
    IIncidentThreadLookupService incidentThreadLookupService,
    IThreadRepository threadRepository) : IIncidentScanner
#pragma warning restore CS9113
{

    private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    private readonly IncidentCentricScannerHelper _scannerHelper = new(incidentEventDetector, logger);
    private const uint PageSize = 50;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    private bool isScanSucceeded = true;
    private DateTime lastScanTime;
    private DateTime? latestModifiedDateInScan = null;
    //After offset > 5000, ICM endpoint will returning 400 bad request
    //Updating offset to 200, since now it will apply on every existing incident Filter
    private static readonly int maxOffset = 200;

    // Automated RCA configuration
    private bool IsAutomatedRCAEnabled => incidentManagementSettings.AutomatedRCA.Enabled;
    private string WebBaseUrl => incidentManagementSettings.AutomatedRCA.WebBaseUrl;
    private bool IsICMAPIReadOnly => incidentManagementSettings.ICMAPI.ReadOnly;

    // Track processed incidents to avoid duplicate processing
    private readonly HashSet<string> _processedIncidents = new();
    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        var lastScanTimeKey = LastScanTimeDoc.GetLastScanTimeKey(IncidentManagementType.Icm);
        var lastScanTimeDoc = await GetDocumentAsync<LastScanTimeDoc>(lastScanTimeKey, lastScanTimeKey);
        lastScanTime = DateTime.UtcNow.AddDays(-30); // Default to 30 days ago

        if (lastScanTimeDoc != null)
        {
            if (lastScanTimeDoc.LastScanTime <= DateTime.UtcNow.AddDays(-30))
            {
                logger.LogInternalWarning("[IcmScanner] Last scan time document is older than 30 days, ignoring and using default 30 days ago.");
            }
            else
            {
                lastScanTime = lastScanTimeDoc.LastScanTime;
                logger.LogInternalInformation("[IcmScanner] Retrieved last scan time from document: {lastScanTime}", lastScanTime);
            }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            // Drain test queue (local dev) before normal scanning if enabled.
            if (IcmScannerTestQueueHelper.IsEnabled())
            {
                try
                {
                    await ProcessTestQueueAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, "[IcmScanner] Error processing test queue batch");
                }
            }
            var filters = await incidentFilterManagementService.ListIncidentFilters(false);
            var scanStartTime = DateTime.UtcNow;
            if (filters is null || filters.Count == 0)
            {
                logger.LogInternalInformation("[IcmScanner] No incident filters found, skipping IcM scanner.");
            }
            else
            {
                logger.LogInternalInformation("[IcmScanner] Found {filterCount} incident filters, starting IcM scanner.", filters.Count);
                latestModifiedDateInScan = lastScanTime; // Reset for each scan cycle
                await ScanAllIncidentsAsync(filters, cancellationToken);
                if (isScanSucceeded)
                {
                    if (latestModifiedDateInScan.HasValue)
                    {
                        // Use the latest ModifiedDate from incidents fetched in this scan as the new checkpoint
                        // This ensures we don't miss incidents due to API lag or timing issues
                        lastScanTime = await UpdateLastScanTimeDocAsync(latestModifiedDateInScan.Value, IncidentManagementType.Icm);
                        logger.LogInternalInformation("[IcmScanner] Updated checkpoint to latest incident ModifiedDate: {lastScanTime}", lastScanTime);

                        if (lastScanTime == DateTime.MinValue)
                        {
                            logger.LogInternalWarning($"[IcmScanner] Last scan time updated to MinValue, resetting to {latestModifiedDateInScan.Value}");
                            lastScanTime = latestModifiedDateInScan.Value;
                        }
                    }
                    else
                    {
                        logger.LogInternalInformation("[IcmScanner] No incidents found in this scan, lastScanTime remains at: {lastScanTime}", lastScanTime);
                    }
                }
                else
                {
                    logger.LogInternalWarning("[IcmScanner] IcM scanner failed to scan incidents, last scan time will not be updated.");
                }
            }
            await Task.Delay(ScanInterval, cancellationToken);
        }
    }

    private async Task ScanAllIncidentsAsync(List<IcmIncidentFilterDocument> filters, CancellationToken cancellationToken)
    {
        // Use incident-centric 4-phase architecture for proper state change detection
        await ScanAllIncidentsIncidentCentricAsync(filters, cancellationToken);
    }

    /// <summary>
    /// Incident-centric scanning with 4-phase architecture:
    /// Phase 1: Collection - Gather incidents across all filters with state snapshot
    /// Phase 2: Detection - Detect all events and assign filters
    /// Phase 3: Persistence - Update Cosmos with current state and checkpoints
    /// Phase 3.5: Status Sync - Sync thread status on ANY status change (decoupled from triggers)
    /// Phase 4: Processing - Execute handlers for persisted events
    /// </summary>
    private async Task ScanAllIncidentsIncidentCentricAsync(List<IcmIncidentFilterDocument> filters, CancellationToken cancellationToken)
    {
        var metrics = new ScanCycleMetrics();
        logger.LogInternalInformation("[IcmScanner] Starting incident-centric scan with {filterCount} filters", filters.Count);

        // ════════════════════════════════════════════════════════════════════════
        // PHASE 1: COLLECTION - Gather incidents with state snapshots
        // ════════════════════════════════════════════════════════════════════════
        var collectedIncidents = await Phase1_CollectIncidentsAsync(filters, metrics, cancellationToken);

        if (collectedIncidents.Count == 0)
        {
            logger.LogInternalInformation("[IcmScanner] No incidents collected, scan complete");
            LogScanMetrics(metrics);
            return;
        }

        // ════════════════════════════════════════════════════════════════════════
        // PHASE 2: DETECTION - Detect events and assign filters
        // ════════════════════════════════════════════════════════════════════════
        var eventMappings = await Phase2_DetectEventsAsync(collectedIncidents, metrics, cancellationToken);

        // ════════════════════════════════════════════════════════════════════════
        // PHASE 3: PERSISTENCE - Update state for ALL collected incidents
        // This runs even if no events were detected to capture state changes
        // ════════════════════════════════════════════════════════════════════════
        var persistedIncidents = await Phase3_PersistStateAsync(eventMappings, collectedIncidents, metrics, cancellationToken);

        // ════════════════════════════════════════════════════════════════════════
        // PHASE 3.5: STATUS SYNC - Sync thread status on ANY status change
        // CRITICAL: This is DECOUPLED from triggers - runs even without events
        // ════════════════════════════════════════════════════════════════════════
        await SyncAllThreadStatusesAsync(collectedIncidents, persistedIncidents, metrics, cancellationToken);

        // ════════════════════════════════════════════════════════════════════════
        // PHASE 4: PROCESSING - Execute handlers for persisted events
        // ════════════════════════════════════════════════════════════════════════
        if (eventMappings.Count > 0)
        {
            await Phase4_ProcessEventsAsync(eventMappings, persistedIncidents, metrics, cancellationToken);
        }
        else
        {
            logger.LogInternalInformation("[IcmScanner] No events to process, skipping Phase 4");
        }

        LogScanMetrics(metrics);
    }

    /// <summary>
    /// Phase 1: Collect incidents across all filters with fault tolerance.
    /// Each filter and incident is processed independently - failures don't affect others.
    /// CRITICAL: BeforeState is captured ONCE here and never modified until Phase 3.
    /// </summary>
    private async Task<Dictionary<string, IncidentScanContext>> Phase1_CollectIncidentsAsync(
        List<IcmIncidentFilterDocument> filters,
        ScanCycleMetrics metrics,
        CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("[IcmScanner] Phase 1: Collecting incidents across {filterCount} filters", filters.Count);
        var collectedIncidents = new Dictionary<string, IncidentScanContext>();

        foreach (var filter in filters)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("[IcmScanner] Phase 1: Cancellation requested");
                break;
            }

            if (filter is not IcmIncidentFilterDocument filterDocument || filterDocument.DocumentType != "IncidentFilterIcm")
            {
                continue;
            }

            try
            {
                await CollectIncidentsForFilterIntoContextAsync(filterDocument, collectedIncidents, cancellationToken);
                metrics.FiltersProcessed++;
            }
            catch (Exception ex)
            {
                metrics.FiltersFailed++;
                logger.LogInternalError(ex, "[IcmScanner] Phase 1: Filter {filterId} failed, continuing with other filters", filterDocument.Id);
            }
        }

        metrics.IncidentsCollected = collectedIncidents.Count;
        logger.LogInternalInformation(
            "[IcmScanner] Phase 1 complete: {incidentCount} incidents collected from {filterCount} filters ({failedCount} filters failed)",
            collectedIncidents.Count,
            metrics.FiltersProcessed,
            metrics.FiltersFailed);

        return collectedIncidents;
    }

    /// <summary>
    /// Collects incidents for a single filter into the shared context dictionary.
    /// Does NOT update Cosmos - just captures the before state snapshot.
    /// </summary>
    private async Task CollectIncidentsForFilterIntoContextAsync(
        IcmIncidentFilterDocument filterDocument,
        Dictionary<string, IncidentScanContext> collectedIncidents,
        CancellationToken cancellationToken)
    {
        uint page = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var offset = page * PageSize;
            if (offset > maxOffset)
            {
                break;
            }

            var incidents = await icmApiClient.GetIncidentsAsync(
                PageSize,
                offset,
                lastScanTime,
                null,
                filterDocument.TitleContains,
                string.IsNullOrWhiteSpace(filterDocument.OwningTeamId) ? null : filterDocument.OwningTeamId,
                string.IsNullOrWhiteSpace(filterDocument.IncidentType) ? null : filterDocument.IncidentType,
                string.IsNullOrWhiteSpace(filterDocument.CreatedBy) ? null : filterDocument.CreatedBy,
                string.IsNullOrWhiteSpace(filterDocument.MonitorId) ? null : filterDocument.MonitorId,
                filterDocument.Priorities
            );

            if (incidents is null || incidents.Count == 0)
            {
                break;
            }

            foreach (var incident in incidents)
            {
                try
                {
                    // Track the latest ModifiedDate for checkpoint
                    var incidentModifiedDate = incident.LastModifiedDate.UtcDateTime;
                    if (!latestModifiedDateInScan.HasValue || incidentModifiedDate > latestModifiedDateInScan.Value)
                    {
                        latestModifiedDateInScan = incidentModifiedDate;
                    }

                    var incidentId = incident.Id.ToString();

                    if (collectedIncidents.TryGetValue(incidentId, out var existingContext))
                    {
                        // Incident already collected - just add this filter
                        if (!existingContext.MatchingFilters.Any(f => f.Id == filterDocument.Id))
                        {
                            existingContext.MatchingFilters.Add(filterDocument);
                        }
                    }
                    else
                    {
                        // New incident - load before state from Cosmos (can be null for new incidents)
                        var beforeState = await GetDocumentAsync<IcmIncidentDocument>(incidentId, incidentId);

                        collectedIncidents[incidentId] = new IncidentScanContext
                        {
                            IncidentId = incidentId,
                            BeforeState = beforeState,
                            CurrentState = incident,
                            MatchingFilters = new List<IcmIncidentFilterDocument> { filterDocument }
                        };
                    }
                }
                catch (Exception ex)
                {
                    logger.LogInternalWarning(ex, "[IcmScanner] Phase 1: Failed to collect incident {incidentId}, skipping", incident.Id);
                }
            }

            page++;
        }
    }

    /// <summary>
    /// Phase 2: Detect all events with fault tolerance.
    /// BeforeState is compared to CurrentState to detect state transitions.
    /// </summary>
    private async Task<List<DetectedEventMapping>> Phase2_DetectEventsAsync(
        Dictionary<string, IncidentScanContext> collectedIncidents,
        ScanCycleMetrics metrics,
        CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("[IcmScanner] Phase 2: Detecting events for {incidentCount} incidents", collectedIncidents.Count);
        var allMappings = new List<DetectedEventMapping>();

        foreach (var (incidentId, context) in collectedIncidents)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("[IcmScanner] Phase 2: Cancellation requested");
                break;
            }

            try
            {
                // Fetch discussion entries for event detection
                try
                {
                    var discussionEntries = await icmApiClient.GetIncidentDiscussionEntriesAsync(incidentId);
                    context.DiscussionEntries = discussionEntries?
                        .Select(e => new DiscussionEntryInfo(
                            e.DescriptionEntryId.ToString(),
                            e.ChangedBy,
                            e.Date,
                            e.Text))
                        .ToList() ?? new List<DiscussionEntryInfo>();
                }
                catch (Exception ex)
                {
                    logger.LogInternalWarning(ex, "[IcmScanner] Phase 2: Failed to get discussion entries for {incidentId}, detecting without them", incidentId);
                }

                // Lazy fetch on-call aliases: only if we have @sreagent mentions and don't have aliases yet
                if (context.OnCallAliases == null && context.DiscussionEntries.Any(d => d.Text?.Contains("@sreagent", StringComparison.OrdinalIgnoreCase) == true))
                {
                    var owningTeamId = context.MatchingFilters
                        .Where(f => !string.IsNullOrWhiteSpace(f.OwningTeamId))
                        .Select(f => f.OwningTeamId)
                        .FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(owningTeamId))
                    {
                        try
                        {
                            context.OnCallAliases = await icmApiClient.GetCurrentOnCallAliasesAsync(owningTeamId);
                            logger.LogInternalInformation("[IcmScanner] Phase 2: Fetched {count} on-call aliases for incident {incidentId} (lazy fetch due to @sreagent mention)",
                                context.OnCallAliases?.Count ?? 0, incidentId);
                        }
                        catch (Exception ex)
                        {
                            logger.LogInternalWarning(ex, "[IcmScanner] Phase 2: Failed to fetch on-call aliases for incident {incidentId}", incidentId);
                        }
                    }
                }

                // Detect all events and assign to filters
                var mappings = _scannerHelper.DetectAllEventsAndAssignFilters(context);
                allMappings.AddRange(mappings);
                metrics.EventsDetected += mappings.Count;
            }
            catch (Exception ex)
            {
                metrics.DetectionsFailed++;
                logger.LogInternalError(ex, "[IcmScanner] Phase 2: Detection failed for incident {incidentId}, skipping", incidentId);
            }
        }

        logger.LogInternalInformation(
            "[IcmScanner] Phase 2 complete: {eventCount} events detected ({failedCount} incidents failed)",
            allMappings.Count,
            metrics.DetectionsFailed);

        return allMappings;
    }

    /// <summary>
    /// Phase 3: Persist state updates and event checkpoints with fault tolerance.
    /// Persists ALL collected incidents (to capture state changes) and sets checkpoints for detected events.
    /// Returns the set of incident IDs that were successfully persisted.
    /// </summary>
    private async Task<HashSet<string>> Phase3_PersistStateAsync(
        List<DetectedEventMapping> eventMappings,
        Dictionary<string, IncidentScanContext> collectedIncidents,
        ScanCycleMetrics metrics,
        CancellationToken cancellationToken)
    {
        // Group events by incident for checkpoint setting
        var eventsByIncident = eventMappings.GroupBy(e => e.IncidentId).ToDictionary(g => g.Key, g => g.ToList());
        var persistedIncidents = new HashSet<string>();

        logger.LogInternalInformation(
            "[IcmScanner] Phase 3: Persisting state for {totalCount} incidents ({withEventsCount} with events)",
            collectedIncidents.Count,
            eventsByIncident.Count);

        // Persist ALL collected incidents (not just ones with events)
        // This ensures state changes are captured even when no trigger events fire
        foreach (var (incidentId, context) in collectedIncidents)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("[IcmScanner] Phase 3: Cancellation requested");
                break;
            }

            try
            {
                // Create or update the incident document with current state
                var updatedDoc = await CreateUpdatedIncidentDocumentAsync(context);

                // Set checkpoints for detected events (if any)
                if (eventsByIncident.TryGetValue(incidentId, out var incidentEvents))
                {
                    updatedDoc.ProcessedDiscussionEntryIds ??= new HashSet<string>();

                    foreach (var eventMapping in incidentEvents)
                    {
                        // Track processed discussion entry IDs for deduplication
                        if (eventMapping.Event == IcmIncidentTriggerEvent.DiscussionEntry)
                        {
                            foreach (var entry in eventMapping.TriggeredDiscussionEntries)
                            {
                                updatedDoc.ProcessedDiscussionEntryIds.Add(entry.EntryId);
                            }
                        }
                    }

                    // Cleanup old entries (older than 30 days) to prevent unbounded growth
                    // Note: ProcessedDiscussionEntryIds doesn't have timestamps, so we can't do time-based cleanup
                    // Just limit the size
                    if (updatedDoc.ProcessedDiscussionEntryIds.Count > 100)
                    {
                        var toRemove = updatedDoc.ProcessedDiscussionEntryIds.Take(updatedDoc.ProcessedDiscussionEntryIds.Count - 100).ToList();
                        foreach (var entryId in toRemove)
                        {
                            updatedDoc.ProcessedDiscussionEntryIds.Remove(entryId);
                        }
                    }
                }

                // Persist to Cosmos
                await container.UpsertItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);

                persistedIncidents.Add(incidentId);
                metrics.PersistenceSucceeded++;

                var eventCount = eventsByIncident.TryGetValue(incidentId, out var events) ? events.Count : 0;
                logger.LogInternalInformation(
                    "[IcmScanner] Phase 3: Persisted incident {incidentId} with {eventCount} event checkpoints",
                    incidentId,
                    eventCount);
            }
            catch (Exception ex)
            {
                metrics.PersistenceFailed++;
                logger.LogInternalError(ex,
                    "[IcmScanner] Phase 3: Persistence failed for incident {incidentId}, events will NOT be processed",
                    incidentId);
                // Do NOT add to persistedIncidents - events won't be processed
            }
        }

        logger.LogInternalInformation(
            "[IcmScanner] Phase 3 complete: {successCount} incidents persisted ({failedCount} failed)",
            metrics.PersistenceSucceeded,
            metrics.PersistenceFailed);

        return persistedIncidents;
    }

    /// <summary>
    /// Creates an updated incident document from the scan context.
    /// </summary>
    private async Task<IcmIncidentDocument> CreateUpdatedIncidentDocumentAsync(IncidentScanContext context)
    {
        var incident = context.CurrentState;
        var existingDoc = context.BeforeState;

        if (existingDoc == null)
        {
            // New incident
            return new IcmIncidentDocument(incident);
        }

        // Update existing document with current state while preserving important fields
        var updatedDoc = new IcmIncidentDocument(incident)
        {
            AIRootCause = existingDoc.AIRootCause,
            RootCauseDescription = existingDoc.RootCauseDescription,
            GeneralSummary = existingDoc.GeneralSummary,
            IsAssistedByAgent = existingDoc.IsAssistedByAgent,
            DiscussionEntries = existingDoc.DiscussionEntries,
            ProcessedDiscussionEntryIds = existingDoc.ProcessedDiscussionEntryIds,
            PreviousState = existingDoc.PreviousState
        };

        // Update discussion entries from API
        try
        {
            var latestDiscussionEntries = await icmApiClient.GetIncidentDiscussionEntriesAsync(incident.Id.ToString());
            updatedDoc.DiscussionEntries = latestDiscussionEntries;
        }
        catch (Exception ex)
        {
            logger.LogInternalWarning(ex, "[IcmScanner] Failed to update discussion entries for incident {incidentId}", incident.Id);
        }

        // Do AI analysis if incident is mitigated/resolved
        var incidentStatus = incident.State.ToString();
        if ((string.Equals(incidentStatus, IncidentStatus.Mitigated.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(incidentStatus, IncidentStatus.Resolved.ToString(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(updatedDoc.AIRootCause) || string.IsNullOrWhiteSpace(updatedDoc.RootCauseDescription) || string.IsNullOrWhiteSpace(updatedDoc.GeneralSummary)))
        {
            try
            {
                // Find a matching filter for analysis
                var filterDocument = context.MatchingFilters.FirstOrDefault();
                updatedDoc = await incidentAnalysisService.AnalyzeIncident(updatedDoc, incident, filterDocument);
            }
            catch (Exception ex)
            {
                logger.LogInternalError($"[IcmScanner] Error generating AI-generated insights for incident; {ex.Message}");
            }
        }

        return updatedDoc;
    }

    /// <summary>
    /// Phase 4: Process events for incidents that were successfully persisted.
    /// </summary>
    private async Task Phase4_ProcessEventsAsync(
        List<DetectedEventMapping> eventMappings,
        HashSet<string> persistedIncidents,
        ScanCycleMetrics metrics,
        CancellationToken cancellationToken)
    {
        // Only process events for incidents that were successfully persisted
        var eventsToProcess = eventMappings
            .Where(e => persistedIncidents.Contains(e.IncidentId))
            .ToList();

        logger.LogInternalInformation("[IcmScanner] Phase 4: Processing {eventCount} events", eventsToProcess.Count);

        foreach (var eventMapping in eventsToProcess)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("[IcmScanner] Phase 4: Cancellation requested");
                break;
            }

            try
            {
                await ProcessSingleEventAsync(eventMapping, cancellationToken);
                metrics.EventsProcessed++;
            }
            catch (Exception ex)
            {
                metrics.ProcessingFailed++;
                logger.LogInternalError(ex,
                    "[IcmScanner] Phase 4: Processing failed for event {eventType} on incident {incidentId}. Event was checkpointed and will NOT retry.",
                    eventMapping.Event,
                    eventMapping.IncidentId);
            }
        }

        logger.LogInternalInformation(
            "[IcmScanner] Phase 4 complete: {successCount} events processed ({failedCount} failed)",
            metrics.EventsProcessed,
            metrics.ProcessingFailed);
    }

    /// <summary>
    /// Processes a single event mapping. Handles thread creation and event dispatching
    /// for all trigger types: IncidentCreatedOrTransferred, DiscussionEntry, IncidentMitigated,
    /// IncidentResolved, IncidentReactivated, and HitCountIncreased.
    /// </summary>
    private async Task ProcessSingleEventAsync(DetectedEventMapping eventMapping, CancellationToken cancellationToken)
    {
        var incidentId = eventMapping.IncidentId;
        var filter = eventMapping.SelectedFilter;
        var triggerEvent = eventMapping.Event;
        var triggeredDiscussionEntries = eventMapping.TriggeredDiscussionEntries;
        var hitCountChange = eventMapping.HitCountChange;

        logger.LogInternalInformation(
            "[IcmScanner] Processing event {eventType} for incident {incidentId} with filter {filterId}",
            triggerEvent,
            incidentId,
            filter.Id);

        // Load the updated incident document (now has current state after Phase 3)
        var incidentDocument = await GetDocumentAsync<IcmIncidentDocument>(incidentId, incidentId);
        if (incidentDocument == null)
        {
            throw new InvalidOperationException($"Incident document {incidentId} not found after persistence");
        }

        // Verify trigger is still enabled (defense in depth - Phase 2 already checks this)
        if (!filter.IsTriggerEnabled(triggerEvent))
        {
            logger.LogInternalWarning(
                "[IcmScanner] Trigger {triggerEvent} is not enabled for filter {filterId}, skipping",
                triggerEvent, filter.Id);
            return;
        }

        // Get all handling agents for this filter
        var handlingAgents = filter.GetEffectiveHandlingAgents();
        if (handlingAgents.Count == 0)
        {
            // Backward compatibility: If no agents configured, use empty string (meta_agent fallback)
            handlingAgents = new List<string> { string.Empty };
        }

        // Find all existing threads for this incident
        var existingThreads = await incidentThreadLookupService.FindAllThreadsForIncidentAsync(incidentId);
        var existingHandlerIds = existingThreads
            .Where(t => t.IncidentDetails?.HandlerId != null)
            .Select(t => t.IncidentDetails!.HandlerId!)
            .ToHashSet();

        // Create threads for handling agents that don't have threads yet
        // Note: When handlingAgents contains empty string (""), check by FilterId since
        // HandlerId may be either filter.Id (MetaAgent path) or incidentHandler.Id (Handler path)
        var agentsNeedingThreads = handlingAgents
            .Where(agent =>
            {
                if (string.IsNullOrEmpty(agent))
                {
                    // When HandlingAgent is empty, check if any thread exists for this filter
                    // (HandlerId may be filter.Id or incidentHandler.Id depending on path)
                    var hasThreadForFilter = existingThreads.Any(t =>
                        t.IncidentDetails?.FilterId == filter.Id);
                    return !hasThreadForFilter;
                }
                return !existingHandlerIds.Contains(agent);
            })
            .ToList();

        if (agentsNeedingThreads.Count > 0)
        {
            logger.LogInternalInformation(
                "[IcmScanner] Creating threads for {count} handling agents for incident {incidentId}",
                agentsNeedingThreads.Count, incidentId);

            foreach (var handlingAgent in agentsNeedingThreads)
            {
                await CreateThreadForHandlerAsync(incidentDocument, filter, handlingAgent, triggerEvent);
            }

            // Refresh existing threads list after creation
            existingThreads = await incidentThreadLookupService.FindAllThreadsForIncidentAsync(incidentId);
        }

        // Filter threads to only those whose handler is in the current trigger's handling agents list
        // This ensures we only send events to threads that belong to handlers for this trigger
        // Note: When handlingAgents contains empty string, match by FilterId since HandlerId
        // may be either filter.Id (MetaAgent path) or incidentHandler.Id (Handler path)
        var threadsToProcess = existingThreads
            .Where(t =>
            {
                var threadHandlerId = t.IncidentDetails?.HandlerId;

                // Include threads with null/empty HandlerId for backward compatibility
                if (string.IsNullOrEmpty(threadHandlerId))
                    return true;

                // Direct match: thread's handler is in the handling agents list
                if (handlingAgents.Contains(threadHandlerId))
                    return true;

                // Indirect match: If handlingAgents has empty string (meta_agent fallback),
                // match threads belonging to this filter (by FilterId, not HandlerId)
                if (handlingAgents.Contains(string.Empty) &&
                    t.IncidentDetails?.FilterId == filter.Id)
                    return true;

                return false;
            })
            .ToList();

        logger.LogInternalInformation(
            "[IcmScanner] Processing {processCount} threads for incident {incidentId} (handlers: {agents})",
            threadsToProcess.Count, incidentId, string.Join(", ", handlingAgents));

        // Process event for each applicable thread
        foreach (var thread in threadsToProcess)
        {
            var threadHandlerId = thread.IncidentDetails?.HandlerId ?? filter.HandlingAgent;
            await ProcessEventForThreadAsync(thread, incidentDocument, triggerEvent, triggeredDiscussionEntries, hitCountChange, threadHandlerId);
        }
    }

    /// <summary>
    /// Creates a thread for a specific handling agent.
    /// </summary>
    private async Task CreateThreadForHandlerAsync(
        IcmIncidentDocument incidentDocument,
        IcmIncidentFilterDocument filter,
        string handlingAgent,
        IcmIncidentTriggerEvent triggerEvent)
    {
        logger.LogInternalInformation(
            "[IcmScanner] Creating thread for incident {incidentId} with handling agent {handlingAgent}",
            incidentDocument.Id, handlingAgent);

        // Temporarily set the single HandlingAgent for thread creation
        var originalHandlingAgent = filter.HandlingAgent;
        filter.HandlingAgent = handlingAgent;

        try
        {
            var response = await incidentHandlingService.HandleIncidentAsync(
                new IncidentHandlingRequestModelWithFilterOnly<IcmIncidentFilterDocumentPayload>()
                {
                    IncidentId = incidentDocument.Id.ToString(),
                    Title = incidentDocument.Title,
                    Description = incidentDocument.Description,
                    Severity = incidentDocument.Priority,
                    CreatedTime = incidentDocument.CreatedAt,
                    ImpactedService = incidentDocument.ImpactedServiceName,
                    IncidentFilter = filter,
                    TriggerEvent = triggerEvent
                });

            logger.LogInternalInformation(
                "[IcmScanner] Created thread {threadId} for incident {incidentId} with handling agent {handlingAgent}",
                response.ThreadId, incidentDocument.Id, handlingAgent);
        }
        finally
        {
            // Restore original HandlingAgent
            filter.HandlingAgent = originalHandlingAgent;
        }
    }

    /// <summary>
    /// Processes a specific event type for a thread. Routes to appropriate handler method
    /// based on trigger type.
    /// </summary>
    private async Task ProcessEventForThreadAsync(
        ThreadDocument thread,
        IcmIncidentDocument incidentDocument,
        IcmIncidentTriggerEvent triggerEvent,
        List<DiscussionEntryInfo>? triggeredDiscussionEntries,
        HitCountChangeInfo? hitCountChange,
        string? handlerId)
    {
        switch (triggerEvent)
        {
            case IcmIncidentTriggerEvent.IncidentCreatedOrTransferred:
                // Thread was just created, no additional action needed
                // The thread creation already triggers agent processing
                logger.LogInternalInformation(
                    "[IcmScanner] IncidentCreatedOrTransferred: Thread {threadId} created for incident {incidentId}",
                    thread.Id, incidentDocument.Id);
                break;

            case IcmIncidentTriggerEvent.DiscussionEntry:
                if (triggeredDiscussionEntries?.Count > 0)
                {
                    var notes = triggeredDiscussionEntries
                        .Select(e => new IncidentDiscussion(
                            incidentDocument.Id,
                            e.Text,
                            e.ChangedBy,
                            e.ChangedBy,
                            e.Date))
                        .ToList();

                    logger.LogInternalInformation(
                        "[IcmScanner] DiscussionEntry: Processing {count} entries for thread {threadId}",
                        notes.Count, thread.Id);
                    await AddAndProcessDiscussionsInternalAsync(Guid.Parse(thread.Id), notes, handlerId);
                }
                break;

            case IcmIncidentTriggerEvent.IncidentMitigated:
                logger.LogInternalInformation(
                    "[IcmScanner] IncidentMitigated: Triggering event for thread {threadId}",
                    thread.Id);
                await TriggerIncidentEventInternalAsync(
                    Guid.Parse(thread.Id),
                    "The incident has been mitigated.",
                    handlerId);
                break;

            case IcmIncidentTriggerEvent.IncidentResolved:
                logger.LogInternalInformation(
                    "[IcmScanner] IncidentResolved: Triggering event for thread {threadId}",
                    thread.Id);
                await TriggerIncidentEventInternalAsync(
                    Guid.Parse(thread.Id),
                    "The incident has been resolved.",
                    handlerId);
                break;

            case IcmIncidentTriggerEvent.IncidentReactivated:
                logger.LogInternalInformation(
                    "[IcmScanner] IncidentReactivated: Triggering event for thread {threadId}",
                    thread.Id);
                await TriggerIncidentEventInternalAsync(
                    Guid.Parse(thread.Id),
                    "The incident has been reactivated.",
                    handlerId);
                break;

            case IcmIncidentTriggerEvent.HitCountIncreased:
                if (hitCountChange != null)
                {
                    logger.LogInternalInformation(
                        "[IcmScanner] HitCountIncreased: Triggering event for thread {threadId} (HitCount: {previousHitCount} -> {currentHitCount})",
                        thread.Id, hitCountChange.PreviousHitCount, hitCountChange.CurrentHitCount);
                    await TriggerIncidentEventInternalAsync(
                        Guid.Parse(thread.Id),
                        $"Incident Correlated. Hit Count increased from {hitCountChange.PreviousHitCount} to {hitCountChange.CurrentHitCount}.",
                        handlerId);
                }
                break;

            default:
                logger.LogInternalWarning(
                    "[IcmScanner] Unknown trigger event {triggerEvent} for thread {threadId}",
                    triggerEvent, thread.Id);
                break;
        }
    }

    /// <summary>
    /// Logs the scan cycle metrics.
    /// </summary>
    private void LogScanMetrics(ScanCycleMetrics metrics)
    {
        logger.LogInternalInformation(
            "[IcmScanner] Scan cycle complete - Filters: {processed}/{failed}, Incidents: {collected}, Events: {detected}/{detectionFailed}, Persisted: {persisted}/{persistFailed}, StatusSync: {syncSuccess}/{syncFailed}, Processed: {processedEvents}/{processFailed}",
            metrics.FiltersProcessed, metrics.FiltersFailed,
            metrics.IncidentsCollected,
            metrics.EventsDetected, metrics.DetectionsFailed,
            metrics.PersistenceSucceeded, metrics.PersistenceFailed,
            metrics.ThreadStatusSyncSucceeded, metrics.ThreadStatusSyncFailed,
            metrics.EventsProcessed, metrics.ProcessingFailed);
    }

    // ════════════════════════════════════════════════════════════════════════════════════
    // Phase 3.5: Status Sync Methods - Sync thread status on ANY status change
    // ════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Syncs thread statuses for all incidents that had status changes.
    /// CRITICAL: This is DECOUPLED from triggers - runs for ANY status change regardless of trigger configuration.
    /// Must be called AFTER Phase 3 (persistence) but BEFORE Phase 4 (event processing).
    /// </summary>
    private async Task SyncAllThreadStatusesAsync(
        Dictionary<string, IncidentScanContext> collectedIncidents,
        HashSet<string> persistedIncidents,
        ScanCycleMetrics metrics,
        CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("[IcmScanner] Phase 3.5: Syncing thread statuses for incidents with status changes");

        foreach (var (incidentId, context) in collectedIncidents)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("[IcmScanner] Phase 3.5: Cancellation requested");
                break;
            }

            // Only process incidents that were successfully persisted
            if (!persistedIncidents.Contains(incidentId))
            {
                continue;
            }

            // DETECT STATUS CHANGE: Compare previous state to current state
            var previousStatus = context.BeforeState?.State?.ToString();
            var currentStatus = context.CurrentState.State.ToString();

            // Skip if no status change OR if this is a new incident (no previous state)
            if (previousStatus == null ||
                string.Equals(previousStatus, currentStatus, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            logger.LogInternalInformation(
                "[IcmScanner] SyncAllThreadStatuses: Status changed for incident {incidentId}: {previous} → {current}",
                incidentId, previousStatus, currentStatus);

            // Get ALL threads for this incident (multi-agent support)
            var allThreads = await incidentThreadLookupService.FindAllThreadsForIncidentAsync(incidentId);

            if (allThreads.Count == 0)
            {
                logger.LogInternalInformation(
                    "[IcmScanner] SyncAllThreadStatuses: No threads found for incident {incidentId}, skipping status sync",
                    incidentId);
                continue;
            }

            // Load incident document for additional details
            var incidentDocument = await GetDocumentAsync<IcmIncidentDocument>(incidentId, incidentId);
            if (incidentDocument == null)
            {
                logger.LogInternalWarning(
                    "[IcmScanner] SyncAllThreadStatuses: Incident document {incidentId} not found, skipping",
                    incidentId);
                continue;
            }

            // Sync status to ALL threads
            foreach (var thread in allThreads)
            {
                try
                {
                    await SyncThreadStatusFromIncidentAsync(thread, incidentDocument, previousStatus, currentStatus);
                    metrics.ThreadStatusSyncSucceeded++;
                }
                catch (Exception ex)
                {
                    metrics.ThreadStatusSyncFailed++;
                    logger.LogInternalError(ex,
                        "[IcmScanner] SyncAllThreadStatuses: Failed to sync thread {threadId} for incident {incidentId}",
                        thread.Id, incidentId);
                }
            }
        }

        logger.LogInternalInformation(
            "[IcmScanner] Phase 3.5 complete: {succeeded} threads synced ({failed} failed)",
            metrics.ThreadStatusSyncSucceeded,
            metrics.ThreadStatusSyncFailed);
    }

    /// <summary>
    /// Syncs thread status and details from incident state.
    /// Called for ANY status change (Active→Mitigated, Mitigated→Resolved, Resolved→Active, etc.)
    /// </summary>
    private async Task SyncThreadStatusFromIncidentAsync(
        ThreadDocument thread,
        IcmIncidentDocument incident,
        string previousStatus,
        string currentStatus)
    {
        var needsUpsert = false;

        // 1. Sync IncidentStatus field
        var normalizedStatus = NormalizeIncidentStatus(currentStatus);
        if (thread.IncidentStatus != normalizedStatus)
        {
            logger.LogInternalInformation(
                "[IcmScanner] Syncing thread {threadId} status: {old} → {new}",
                thread.Id, thread.IncidentStatus ?? "null", normalizedStatus);

            thread.IncidentStatus = normalizedStatus;
            needsUpsert = true;

            // Log ResolveIncident action only for transitions TO mitigated/resolved
            if (normalizedStatus == "mitigated" || normalizedStatus == "resolved")
            {
                await LogResolveIncidentActionAsync(thread, incident, normalizedStatus);
            }
        }

        // 2. Sync IncidentDetails (title, priority, incident status for display)
        // InvestigationStatus is primarily controlled by ReasoningLoop, but we mark it Complete
        // when incident transitions to mitigated/resolved and the investigation is still InProgress.
        // This handles the case where an incident gets resolved externally while the agent is idle.
        if (thread.IncidentDetails != null)
        {
            var detailsChanged = false;
            var investigationStatus = thread.IncidentDetails.InvestigationStatus;

            if (thread.IncidentDetails.IncidentTitle != incident.Title)
            {
                detailsChanged = true;
            }
            if (thread.IncidentDetails.IncidentPriority != incident.Priority)
            {
                detailsChanged = true;
            }

            // Check if incident status display value changed (case-insensitive)
            var incidentStatusChanged = !string.Equals(thread.IncidentDetails.IncidentStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase);
            if (incidentStatusChanged)
            {
                detailsChanged = true;

                // When incident transitions to mitigated/resolved and investigation is not yet Complete,
                // mark the investigation as Complete since there's no more active incident to investigate.
                // This covers both InProgress and PendingUserInput states.
                if ((normalizedStatus == "mitigated" || normalizedStatus == "resolved") &&
                    investigationStatus != InvestigationStatus.Complete)
                {
                    logger.LogInternalInformation(
                        "[IcmScanner] Marking investigation Complete for thread {threadId} as incident transitioned to {status} (was {previousInvestigationStatus})",
                        thread.Id, normalizedStatus, investigationStatus);
                    investigationStatus = InvestigationStatus.Complete;
                }
            }

            if (detailsChanged)
            {
                thread.IncidentDetails = new IncidentDetails(
                    incident.Title,
                    thread.IncidentDetails.IncidentCreatedTime,
                    incident.Priority,
                    thread.IncidentDetails.ImpactedService,
                    thread.IncidentDetails.FilterId,
                    thread.IncidentDetails.HandlerId,
                    investigationStatus,
                    thread.IncidentDetails.TriggerEvent,
                    normalizedStatus);  // Update incident status for UI display
                needsUpsert = true;
            }
        }

        // 3. Persist if changed
        if (needsUpsert)
        {
            await container.UpsertItemAsync(thread, new PartitionKey(thread.Id));
            logger.LogInternalInformation(
                "[IcmScanner] Updated thread {threadId} for incident {incidentId}: status={status}",
                thread.Id, incident.Id, normalizedStatus);
        }

        // 4. Sync discussion entries to thread messages (for record-keeping, no agent processing)
        // DISABLED: This logic has issues:
        // - Phase 3 already updates DiscussionEntries, so delta is always empty
        // - Risk of duplicates with @mention entries processed in Phase 4
        // - First-time incidents would dump entire history
        // NOTE: Previous implementation for syncing discussion entries to thread messages was removed
        // because it produced duplicate notes and incorrect deltas. Reintroduce only with a correct
        // BeforeState comparison strategy and explicit @mention filtering.
    }

    /// <summary>
    /// Normalizes ICM status string to lowercase thread status.
    /// </summary>
    private static string NormalizeIncidentStatus(string status)
    {
        if (string.Equals(status, "Mitigated", StringComparison.OrdinalIgnoreCase))
            return "mitigated";
        if (string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase))
            return "resolved";
        return "active"; // Default for Active, Investigating, etc.
    }

    /// <summary>
    /// Logs the ResolveIncident action for telemetry when an incident is mitigated/resolved.
    /// </summary>
    private async Task LogResolveIncidentActionAsync(
        ThreadDocument thread,
        IcmIncidentDocument incident,
        string status)
    {
        try
        {
            var resolvedBy = "User"; // Default
            if (incident.Tags?.Any(tag =>
                tag.Equals("SREAgent_Processed", StringComparison.OrdinalIgnoreCase) ||
                tag.Equals("SREAgent_Mitigated", StringComparison.OrdinalIgnoreCase)) == true)
            {
                resolvedBy = "Agent";
            }

            var resolveActionData = new IncidentResolveActionData
            {
                IncidentSource = "Icm",
                IncidentId = incident.Id,
                Status = status,
                ResolvedBy = resolvedBy
            };

            logger.LogAgentAction(
                action: AgentActionEvents.ResolveIncident,
                parameter: JsonSerializer.Serialize(resolveActionData),
                status: AgentActionStatus.Success,
                duration: 0,
                threadId: thread.Id);
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex,
                "[IcmScanner] Failed to log ResolveIncident action for thread {threadId}",
                thread.Id);
        }

        await Task.CompletedTask; // Async placeholder for future telemetry calls
    }

    // ════════════════════════════════════════════════════════════════════════════════════
    // Internal Helper Methods for ICM-specific event processing
    // ════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Triggers agent processing for an incident event.
    /// This is ICM-specific logic, moved from InboundCommunicationService.
    /// </summary>
    private async Task TriggerIncidentEventInternalAsync(
        Guid threadId,
        string eventMessage,
        string? agentName)
    {
        logger.LogInternalInformation(
            "[IcmScanner] TriggerIncidentEventInternalAsync: Triggering event for thread {ThreadId}: {EventMessage} (AgentName: {AgentName})",
            threadId, eventMessage, agentName ?? "none");

        // 1. Get thread
        var thread = await threadRepository.GetThreadAsync(threadId);
        if (thread == null)
        {
            logger.LogInternalWarning("[IcmScanner] Thread {threadId} not found, cannot trigger event", threadId);
            return;
        }

        // 2. Get agent context
        var agentContexts = await threadRepository.GetAgentContextsForThreadAsync(threadId);
        var agentContext = agentContexts.FirstOrDefault(c => c.HandoffFromAgentContextId == null);
        if (agentContext == null)
        {
            logger.LogInternalWarning("[IcmScanner] No agent context for thread {threadId}", threadId);
            return;
        }

        // 3. Create and add event message
        var messageId = Guid.NewGuid();
        var eventMessageRecord = new Message(
            Id: messageId,
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.User, "system", "System Event"),
            Text: eventMessage);

        await threadRepository.AddMessageAsync(threadId, eventMessageRecord);

        // 4. Create ThreadMessage and trigger processing
        var threadMessage = new ThreadMessage(
            ThreadId: threadId,
            AgentContextId: agentContext.Id,
            MessageId: messageId,
            Message: eventMessage,
            UserId: "system",
            DisplayName: "System Event",
            Timestamp: DateTime.UtcNow,
            Posted: new Posted(true),
            AgentName: agentName);

        await agentInboundCommunicationService.ProcessIncidentMessageAsync(threadMessage, defaultHandler: true);

        logger.LogInternalInformation(
            "[IcmScanner] TriggerIncidentEventInternalAsync: Successfully processed event message {MessageId} for thread {ThreadId}",
            messageId, threadId);
    }

    /// <summary>
    /// Adds discussion entries and triggers agent processing.
    /// This is ICM-specific logic, moved from InboundCommunicationService.
    /// </summary>
    private async Task AddAndProcessDiscussionsInternalAsync(
        Guid threadId,
        List<IncidentDiscussion> discussions,
        string? agentName)
    {
        if (discussions == null || discussions.Count == 0)
        {
            logger.LogInternalInformation("[IcmScanner] No discussions to add to thread {ThreadId}", threadId);
            return;
        }

        // 1. Get thread
        var thread = await threadRepository.GetThreadAsync(threadId);
        if (thread == null)
        {
            logger.LogInternalWarning("[IcmScanner] Thread {threadId} not found", threadId);
            return;
        }

        // 2. Get agent context
        var agentContexts = await threadRepository.GetAgentContextsForThreadAsync(threadId);
        var agentContext = agentContexts.FirstOrDefault();
        if (agentContext == null)
        {
            logger.LogInternalWarning("[IcmScanner] No agent context for thread {threadId}", threadId);
            return;
        }

        // 3. Add discussion messages
        Guid lastMessageId = Guid.Empty;
        foreach (var discussion in discussions)
        {
            var message = new Message(
                Id: Guid.NewGuid(),
                TimeStamp: DateTime.UtcNow,
                Author: new Author(Role.User, discussion.UserId, discussion.UserDisplayName),
                Text: $"{discussion.UserDisplayName} commented at {discussion.CreatedTimestamp}: {discussion.Message}",
                IncidentDiscussionId: discussion.Id);
            await threadRepository.AddMessageAsync(threadId, message);
            lastMessageId = message.Id;
        }

        // 4. Build combined message and trigger processing
        var combinedMessage = string.Join("\n\n",
            discussions.Select(d => $"{d.UserDisplayName} mentioned @sreagent: {d.Message}"));

        logger.LogInternalInformation(
            "[IcmScanner] Processing {Count} discussion entries with @sreagent mentions for thread {ThreadId} (AgentName: {AgentName})",
            discussions.Count, threadId, agentName ?? "none");

        var threadMessage = new ThreadMessage(
            ThreadId: threadId,
            AgentContextId: agentContext.Id,
            MessageId: lastMessageId,
            Message: combinedMessage,
            UserId: discussions.First().UserId,
            DisplayName: discussions.First().UserDisplayName,
            Timestamp: DateTime.UtcNow,
            Posted: new Posted(true),
            AgentName: agentName);

        await agentInboundCommunicationService.ProcessIncidentMessageAsync(threadMessage, defaultHandler: true);
    }

    /// <summary>
    /// Monitors RCA completion and posts results back to ICM
    /// </summary>
    /// <param name="incidentId">The incident ID</param>
    /// <param name="threadId">The thread ID where RCA is running</param>
    /// <param name="threadUrl">The URL to the RCA thread</param>
    private async Task MonitorRCACompletionAsync(string incidentId, Guid threadId, string threadUrl, string owningTeamId)
    {
        try
        {
            // Monitor for up to 24 hours, checking every 30 minutes
            var maxAttempts = 48;
            var checkInterval = TimeSpan.FromMinutes(30);

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                await Task.Delay(checkInterval);

                try
                {
                    // Prefer tag-based completion check
                    var incident = await icmApiClient.GetIncidentAsync(incidentId);
                    var teamCompletedTag = !string.IsNullOrWhiteSpace(owningTeamId) ? $"{owningTeamId}:Completed" : null;
                    if (!string.IsNullOrWhiteSpace(teamCompletedTag) &&
                        incident?.Tags?.Any(t => string.Equals(t, teamCompletedTag, StringComparison.OrdinalIgnoreCase)) == true)
                    {
                        logger.LogInternalInformation("[IcmScanner] RCA completed for incident {incidentId} (found tag {tag})", incidentId, teamCompletedTag);
                        _processedIncidents.Remove(incidentId);
                        return;
                    }

                    // Legacy: context-state based (may never reach Completed; keep as secondary)
                    var agentContexts = await GetAgentContextsForThread(threadId);
                    var activeContext = agentContexts?.FirstOrDefault();
                    if (activeContext == null)
                    {
                        logger.LogInternalWarning("[IcmScanner] No agent context found for thread {threadId}, attempt {attempt}", threadId, attempt + 1);
                        continue;
                    }

                    if (activeContext.ContextState == ContextStateEnum.Failed)
                    {
                        logger.LogInternalWarning("[IcmScanner] RCA encountered error for incident {incidentId}", incidentId);
                        _processedIncidents.Remove(incidentId);
                        return;
                    }

                    logger.LogInternalInformation("[IcmScanner] RCA still in progress for incident {incidentId}, state: {state}, attempt {attempt}",
                        incidentId, activeContext.ContextState, attempt + 1);
                }
                catch (Exception ex)
                {
                    logger.LogInternalWarning(ex, "[IcmScanner] Error checking RCA completion status for incident {incidentId}, attempt {attempt}",
                        incidentId, attempt + 1);
                    _processedIncidents.Remove(incidentId);
                }
            }

            // Timeout
            logger.LogInternalWarning("[IcmScanner] RCA monitoring timed out for incident {incidentId}", incidentId);
            _processedIncidents.Remove(incidentId);
            var timeoutMessage = $"⏰ **RCA Analysis Status**: The automated analysis is taking longer than expected. Please check the [analysis thread]({threadUrl}) for the latest progress.";
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error monitoring RCA completion for incident {incidentId}", incidentId);
        }
    }

    private async Task<T?> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            var response = await container.ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
        catch (JsonException ex) when (ex.Message.StartsWith("The JSON value could not be converted to") && typeof(T) == typeof(IcmIncidentDocument))
        {
            logger.LogInternalWarning("[IcmScanner] JSON deserialization error for document Id: {id} PartitionKey: {partitionKey}, Prepare to delete Document.Message: {message}", id, partitionKey, ex.Message);
            await container.DeleteItemAsync<T>(
                id,
                new PartitionKey(partitionKey)
            );
            return default;
        }
    }

    private async Task<IcmIncidentDocument> UpsertIncidentDocumentIfNeededAsync(IcmIncidentDocument? incidentDocument, ICMIncident incident, IcmIncidentFilterDocument? filterDocument, CancellationToken cancellationToken = default)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalInformation("[IcmScanner] Creating new incident document for incident id {incidentId}", incident.Id);

                incidentDocument = new IcmIncidentDocument(incident);

                await container.CreateItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey), cancellationToken: cancellationToken);
                logger.LogInternalInformation("[IcmScanner] Created new incident document for IcM incident {incidentId}", incident.Id);
            }
            else if (incidentDocument.Id == incident.Id.ToString())
            {
                logger.LogInternalInformation("[IcmScanner] Incident document already exists for incident id {incidentId}", incident.Id);
                //For now use UpsertItemAsync for updating IcmIncidentDocument with latest non-critical fields, later can switch to PatchItemAsync if needed.
                var updatedDoc = new IcmIncidentDocument(incident)
                {
                    AIRootCause = incidentDocument.AIRootCause,
                    RootCauseDescription = incidentDocument.RootCauseDescription,
                    GeneralSummary = incidentDocument.GeneralSummary,
                    IsAssistedByAgent = incidentDocument.IsAssistedByAgent,
                    DiscussionEntries = incidentDocument.DiscussionEntries,
                    // CRITICAL: Preserve these fields for event detection and deduplication
                    ProcessedDiscussionEntryIds = incidentDocument.ProcessedDiscussionEntryIds,
                    PreviousState = incidentDocument.PreviousState
                };

                // Once incident is mitigated or resolved, do AI analysis
                var incidentStatus = incident.State.ToString();
                if ((string.Equals(incidentStatus, IncidentStatus.Mitigated.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(incidentStatus, IncidentStatus.Resolved.ToString(), StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(updatedDoc.AIRootCause) || string.IsNullOrWhiteSpace(updatedDoc.RootCauseDescription) || string.IsNullOrWhiteSpace(updatedDoc.GeneralSummary)))
                {
                    try
                    {
                        updatedDoc = await incidentAnalysisService.AnalyzeIncident(updatedDoc, incident, filterDocument);
                    }
                    catch (Exception ex)
                    {
                        logger.LogInternalError($"[IcmScanner] Error generating AI-generated insights for incident; {ex.Message}");
                    }
                }

                var latestDiscussionEntries = await icmApiClient.GetIncidentDiscussionEntriesAsync(incident.Id.ToString());
                updatedDoc.DiscussionEntries = latestDiscussionEntries;
                var newNotes = latestDiscussionEntries?
                        .Where(entry => entry.Date > lastScanTime.AddMinutes(-5)) // Add 5 minutes buffer to avoid missing notes due to time skew
                        .Select(entry => new IncidentDiscussion($"{entry.DescriptionEntryId}-{entry.Date}", entry.Text, entry.ChangedBy, entry.ChangedBy, entry.Date))
                        .ToList() ?? new List<IncidentDiscussion>();

                if (newNotes.Count > 0)
                {
                    // Check if agent assisted with incident based on new notes
                    if (!updatedDoc.IsAssistedByAgent)
                    {
                        var agentAssisted = await incidentAnalysisService.DetermineAgentAssistanceFromNotes(updatedDoc, newNotes);
                        if (agentAssisted)
                        {
                            logger.LogInternalInformation("[IcmScanner] Detected agent assistance for incident {incidentId}, updating document", updatedDoc.Id);
                            updatedDoc.IsAssistedByAgent = true;
                        }
                    }
                }

                logger.LogInternalInformation("[IcmScanner] Upserting existing incident document for IcM incident {incidentId}", incident.Id);
                var response = await container.UpsertItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
                incidentDocument = response.Resource;
            }

            if (incidentDocument == null)
            {
                throw new Exception($"Failed to create or update incident document for IcM incident {incident?.Id}. The incident document is null.");
            }

            return incidentDocument;

        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            logger.LogInternalWarning("[IcmScanner] Original IcM {incidentId} is too large, truncate incident details", incident.Id);
            incidentDocument = IcmIncidentDocument.TruncateIcmIncidentDocument(incident);
            await container.UpsertItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey), cancellationToken: cancellationToken);
            return incidentDocument;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error upserting incident document for IcM incident {incidentId}", incident.Id);
            throw;
        }
    }

    /// <summary>
    /// Team-specific incident processing (AutomatedRCA only)
    /// </summary>
    /// <param name="incidentDocument">Incident document</param>
    /// <param name="filterDocument">Filter document</param>
    private async Task ProcessTeamSpecificIncident(IcmIncidentDocument incidentDocument, IcmIncidentFilterDocumentPayload filterDocument, bool forceRun = false)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalWarning("[IcmScanner] Incident document is null, skipping team-specific processing.");
                return;
            }

            // by-pass the checkinf for testing.
            if (forceRun)
            {
                logger.LogInternalWarning($"[IcmScanner] Rceive testing queue for IncidentId: {incidentDocument.Id} forceRun it.");
                await ExecuteAutomatedRCAAsync(incidentDocument, filterDocument.OwningTeamId);
                return;
            }

            // Skip resolved/mitigated incidents
            if (incidentDocument.State.ToString().Equals("resolved", StringComparison.OrdinalIgnoreCase) ||
                incidentDocument.State.ToString().Equals("mitigated", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} is mitigated/resolved, skipping team-specific processing.", incidentDocument.Id);
                return;
            }

            // Check if already processed
            if (_processedIncidents.Contains(incidentDocument.Id.ToString()))
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} already processed, skipping.", incidentDocument.Id);
                return;
            }

            // Get detailed information from ICM API and check tags
            var incident = await icmApiClient.GetIncidentAsync(incidentDocument.Id.ToString());
            if (incident == null)
            {
                logger.LogInternalWarning("[IcmScanner] Could not retrieve incident details for {incidentId}", incidentDocument.Id);
                return;
            }
            var owningTeamId = filterDocument.OwningTeamId;
            // New: Skip if team-specific Completed tag exists
            var teamCompletedTag = !string.IsNullOrWhiteSpace(owningTeamId) ? $"{owningTeamId}:Completed" : null;
            if (!string.IsNullOrWhiteSpace(teamCompletedTag) && incident.Tags?.Any(t => string.Equals(t, teamCompletedTag, StringComparison.OrdinalIgnoreCase)) == true)
            {
                logger.LogInternalInformation("[IcmScanner] Incident {incidentId} already completed for owning team (tag: {tag})", incidentDocument.Id, teamCompletedTag);
                return;
            }

            logger.LogInternalInformation("[IcmScanner] Incident {incidentId} qualifies for automated RCA execution via team filter", incidentDocument.Id);

            // Execute Automated RCA
            await ExecuteAutomatedRCAAsync(incidentDocument, owningTeamId);
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error processing team-specific incident {incidentId}", incidentDocument.Id);
        }
    }

    /// <summary>
    /// Executes automated RCA for the given incident
    /// </summary>
    /// <param name="incidentDocument">The incident document to process</param>
    private async Task ExecuteAutomatedRCAAsync(IcmIncidentDocument incidentDocument, string owningTeamId)
    {
        try
        {
            logger.LogInternalInformation("[IcmScanner] Starting automated RCA execution for incident {incidentId}", incidentDocument.Id);

            // Mark incident as processed to avoid duplicate processing
            _processedIncidents.Add(incidentDocument.Id.ToString());

            // Begin scanner-origin scope so downstream components can gate ICM posting/tagging
            using (IncidentProcessingContext.BeginScannerOriginScope())
            {
                // Create thread that will trigger agent execution with the incident ID
                var (thread, agentContext) = await agentInboundCommunicationService.CreateAgentThread(
                    title: $"🤖 Automated RCA for ICM {incidentDocument.Id}: {incidentDocument.Title}",
                    message: $"Please analyze and route IncidentId {incidentDocument.Id} for RCA analysis.",
                    agentTypeEnum: AgentTypeEnum.Incident,
                    source: ThreadSource.Incident,
                    incidentId: incidentDocument.Id,
                    incidentSource: new IncidentSource(Agent.Core.Models.Api.v1.IncidentType.Icm, incidentDocument.Id.ToString())
                );

                // Start agent execution
                await agentInboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                    ThreadId: thread.Id,
                    AgentContextId: agentContext.Id,
                    MessageId: thread.StartMessage?.Id ?? new Guid(),
                    Message: $"Please analyze and route IncidentId {incidentDocument.Id} for RCA analysis.",
                    UserId: "icm-scanner",
                    DisplayName: "ICM Scanner",
                    Timestamp: DateTime.UtcNow
                ));

                var threadUrl = $"{WebBaseUrl}/static/#/views/thread/{thread.Id}";
                logger.LogInternalInformation($"[IcmScanner] Automated RCA thread created and started for incident {incidentDocument.Id}. Thread ID: {thread.Id}, URL: {threadUrl}",
                    incidentDocument.Id, thread.Id, threadUrl);

                // Start background monitoring for RCA completion
                _ = Task.Run(() => MonitorRCACompletionAsync(incidentDocument.Id.ToString(), thread.Id, threadUrl, owningTeamId));
            }
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error executing automated RCA for incident {incidentId}", incidentDocument.Id);

            // Remove from processed incidents on error
            _processedIncidents.Remove(incidentDocument.Id.ToString());
        }
    }

    /// <summary>
    /// Gets agent contexts for a specific thread
    /// </summary>
    /// <param name="threadId">The thread ID</param>
    /// <returns>List of agent contexts</returns>
    private async Task<List<AgentContext>?> GetAgentContextsForThread(Guid threadId)
    {
        try
        {
            var query = container.GetItemLinqQueryable<AgentContextDocument>()
                .Where(doc => doc.DocumentType == "AgentContext" && doc.ThreadId == threadId.ToString())
                .ToFeedIterator();

            var results = new List<AgentContext>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                foreach (var doc in response)
                {
                    results.Add(new AgentContext(
                        Id: Guid.Parse(doc.Id),
                        ThreadId: Guid.Parse(doc.ThreadId),
                        AgentType: doc.AgentType,
                        ContextState: doc.ContextState,
                        WaitInformation: doc.WaitInformation,
                        ApprovalInformation: doc.ApprovalInformation,
                        CurrentAgent: doc.CurrentAgent,
                        AllowedTools: doc.AllowedTools
                    ));
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error getting agent contexts for thread {threadId}", threadId);
            return null;
        }
    }

    // ------------------------------------------------------------
    // Local test queue processing
    // ------------------------------------------------------------
    private async Task ProcessTestQueueAsync(CancellationToken cancellationToken)
    {
        var batch = IcmScannerTestQueueHelper.Drain();
        if (batch.Count == 0)
        {
            return;
        }

        logger.LogInternalInformation("[IcmScanner] Processing {count} test queue incident(s).", batch.Count);
        foreach (var item in batch)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var icmIncident = await icmApiClient.GetIncidentAsync(item.IncidentId);
                if (icmIncident == null)
                {
                    logger.LogInternalWarning("[IcmScanner] Test queue incident {incidentId} not found via ICM API.", item.IncidentId);
                    continue;
                }
                var incidentDoc = await GetDocumentAsync<IcmIncidentDocument>(item.IncidentId, item.IncidentId);
                incidentDoc = await UpsertIncidentDocumentIfNeededAsync(incidentDoc, icmIncident, null, cancellationToken);

                // Use automated RCA path only when feature enabled; otherwise fall back to standard notification.
                if (IsAutomatedRCAEnabled && item.ForceTeamSpecific && !string.IsNullOrWhiteSpace(item.OwningTeamId))
                {
                    // Minimal synthetic filter payload for team-specific processing.
                    var payload = new IcmIncidentFilterDocumentPayload
                    {
                        OwningTeamId = item.OwningTeamId,
                        TitleContains = string.Empty,
                        IncidentType = string.Empty
                    };
                    await ProcessTeamSpecificIncident(incidentDoc, payload, forceRun: true);
                }
                else
                {
                    // Test queue without filter: Create thread using incident handling service directly
                    // with a minimal synthetic filter for IncidentCreatedOrTransferred trigger
                    var syntheticFilter = new IcmIncidentFilterDocument
                    {
                        Id = "TestQueue",
                        Name = "TestQueue",
                        AlertId = "TestQueue",
                        AgentMode = AgentModes.Autonomous.ToLowerInvariant(),
                        ImpactedService = incidentDoc.ImpactedServiceName ?? string.Empty,
                        Priorities = string.IsNullOrWhiteSpace(incidentDoc.Priority) ? new List<string>() : new List<string> { incidentDoc.Priority },
                        IncidentType = incidentDoc.IncidentType ?? string.Empty,
                        TitleContains = string.Empty,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        Triggers = new List<IcmIncidentTriggerEvent> { IcmIncidentTriggerEvent.IncidentCreatedOrTransferred }
                    };

                    await incidentHandlingService.HandleIncidentAsync(
                        new IncidentHandlingRequestModelWithFilterOnly<IcmIncidentFilterDocumentPayload>()
                        {
                            IncidentId = incidentDoc.Id.ToString(),
                            Title = incidentDoc.Title,
                            Description = incidentDoc.Description,
                            Severity = incidentDoc.Priority,
                            CreatedTime = incidentDoc.CreatedAt,
                            ImpactedService = incidentDoc.ImpactedServiceName,
                            IncidentFilter = syntheticFilter,
                            TriggerEvent = IcmIncidentTriggerEvent.IncidentCreatedOrTransferred
                        });

                    logger.LogInternalInformation("[IcmScanner] Test queue: Created thread for incident {incidentId}", item.IncidentId);
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "[IcmScanner] Error handling test queue incident {incidentId}", item.IncidentId);
            }
        }
    }

    private async Task<DateTime> UpdateLastScanTimeDocAsync(DateTime lastScanTime, IncidentManagementType type)
    {
        try
        {
            var patchOperationList = new List<PatchOperation>()
            {
                PatchOperation.Add($"/lastScanTime", lastScanTime)
            };

            var lastScanTimeKey = LastScanTimeDoc.GetLastScanTimeKey(type);
            var doc = await container.PatchItemAsync<LastScanTimeDoc>(
                lastScanTimeKey,
                new PartitionKey(lastScanTimeKey),
                patchOperationList
            );
            return doc.Resource.LastScanTime;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var lastScanTimeKey = LastScanTimeDoc.GetLastScanTimeKey(type);
            var lastScanTimeDoc = new LastScanTimeDoc
            {
                Id = lastScanTimeKey,
                DocumentType = lastScanTimeKey,
                PartitionKey = lastScanTimeKey,
                LastScanTime = lastScanTime
            };
            var doc = await container.CreateItemAsync(lastScanTimeDoc, new PartitionKey(lastScanTimeKey));
            return doc.Resource.LastScanTime;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "[IcmScanner] Error updating LastScanTime for {incidentType} scanner", type);
            return DateTime.UtcNow;
        }
    }
}

public class NullableIncidentScanner : IIncidentScanner
{
    public Task ScanAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }
}
