// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Runtime.Services.IncidentTriggerDetection;
using Microsoft.Extensions.Logging;
using Microsoft.SREAgent.Incidents.IcM.Model;

namespace Agent.Runtime.SubAgents.Scanner;

/// <summary>
/// Context for an incident collected during the scan phase.
/// Captures the before state (from Cosmos) and current state (from ICM API)
/// along with all filters that matched this incident.
/// </summary>
public record IncidentScanContext
{
    public required string IncidentId { get; init; }

    /// <summary>
    /// The incident document from Cosmos DB at the start of the scan (null if new incident).
    /// This represents the "before" state for event detection.
    /// </summary>
    public IcmIncidentDocument? BeforeState { get; init; }

    /// <summary>
    /// The current incident data from the ICM API.
    /// </summary>
    public required ICMIncident CurrentState { get; init; }

    /// <summary>
    /// All filters that matched this incident during collection.
    /// </summary>
    public List<IcmIncidentFilterDocument> MatchingFilters { get; init; } = new();

    /// <summary>
    /// Discussion entries for this incident (populated during detection phase).
    /// </summary>
    public List<DiscussionEntryInfo> DiscussionEntries { get; set; } = new();

    /// <summary>
    /// On-call aliases for the owning team (for discussion entry validation).
    /// </summary>
    public List<string>? OnCallAliases { get; set; }
}

/// <summary>
/// Maps a detected event to the filter/handler that should process it.
/// </summary>
public record DetectedEventMapping
{
    public required string IncidentId { get; init; }
    public required IcmIncidentTriggerEvent Event { get; init; }
    public required IcmIncidentFilterDocument SelectedFilter { get; init; }

    /// <summary>
    /// The scan context containing before/current state information.
    /// </summary>
    public required IncidentScanContext ScanContext { get; init; }

    /// <summary>
    /// Discussion entries that triggered this event (for DiscussionEntry events).
    /// </summary>
    public List<DiscussionEntryInfo> TriggeredDiscussionEntries { get; init; } = new();
}

/// <summary>
/// Metrics for a single scan cycle.
/// </summary>
public record ScanCycleMetrics
{
    public int FiltersProcessed { get; set; }
    public int FiltersFailed { get; set; }
    public int IncidentsCollected { get; set; }
    public int IncidentCollectionsFailed { get; set; }
    public int EventsDetected { get; set; }
    public int DetectionsFailed { get; set; }
    public int PersistenceSucceeded { get; set; }
    public int PersistenceFailed { get; set; }
    /// <summary>
    /// Number of threads successfully synced with incident status changes.
    /// </summary>
    public int ThreadStatusSyncSucceeded { get; set; }
    /// <summary>
    /// Number of thread status sync operations that failed.
    /// </summary>
    public int ThreadStatusSyncFailed { get; set; }
    public int EventsProcessed { get; set; }
    public int ProcessingFailed { get; set; }
}

/// <summary>
/// Helper class for incident-centric scanning that coordinates event detection
/// and filter matching across multiple filters.
/// </summary>
public class IncidentCentricScannerHelper
{
    private readonly IIncidentEventDetector _eventDetector;
    private readonly ILogger _logger;

    public IncidentCentricScannerHelper(
        IIncidentEventDetector eventDetector,
        ILogger logger)
    {
        _eventDetector = eventDetector;
        _logger = logger;
    }

    /// <summary>
    /// Creates a state snapshot from an incident document (from Cosmos).
    /// </summary>
    public static IncidentStateSnapshot CreateStateSnapshot(IcmIncidentDocument document)
    {
        return new IncidentStateSnapshot(
            document.Id,
            document.State.ToString(),  // Ensure State is converted to string for consistent comparison
            document.CreatedDate,
            document.LastModifiedDate);
    }

    /// <summary>
    /// Creates a state snapshot from an ICM API incident.
    /// </summary>
    public static IncidentStateSnapshot CreateStateSnapshotFromApi(ICMIncident incident)
    {
        return new IncidentStateSnapshot(
            incident.Id.ToString(),
            incident.State.ToString(),
            incident.CreatedDate,
            incident.LastModifiedDate);
    }

    /// <summary>
    /// Detects all events for an incident and returns mappings to eligible filters.
    /// Each event is matched to the filter that has it enabled.
    /// </summary>
    /// <param name="context">The incident scan context with before/current state</param>
    /// <returns>List of event-to-filter mappings for this incident</returns>
    public List<DetectedEventMapping> DetectAllEventsAndAssignFilters(IncidentScanContext context)
    {
        var mappings = new List<DetectedEventMapping>();

        // Create state snapshots
        var currentState = CreateStateSnapshotFromApi(context.CurrentState);
        var previousState = context.BeforeState != null
            ? CreateStateSnapshot(context.BeforeState)
            : null;

        // Get processed entry IDs for deduplication
        var processedEntryIds = context.BeforeState?.ProcessedDiscussionEntryIds;

        // Detect all events
        var detectionResult = _eventDetector.DetectOccurredEvents(
            currentState,
            previousState,
            context.DiscussionEntries.Select(d => new Microsoft.AzureAd.Icm.Types.DescriptionEntry
            {
                DescriptionEntryId = long.TryParse(d.EntryId, out var id) ? id : 0,
                Text = d.Text,
                ChangedBy = d.ChangedBy,
                Date = d.Date
            }).ToList(),
            processedEntryIds,
            context.OnCallAliases);

        if (!detectionResult.HasEvents)
        {
            _logger.LogInternalDebug(
                "[IncidentCentricScanner] No events detected for incident {incidentId}",
                context.IncidentId);
            return mappings;
        }

        _logger.LogInternalInformation(
            "[IncidentCentricScanner] Detected {eventCount} events for incident {incidentId}: {events}",
            detectionResult.DetectedEvents.Count,
            context.IncidentId,
            string.Join(", ", detectionResult.DetectedEvents));

        // For each detected event, find the best matching filter that has this trigger enabled
        foreach (var detectedEvent in detectionResult.DetectedEvents)
        {
            // Find filters that have this trigger enabled
            var eligibleFilters = context.MatchingFilters
                .Where(f => f.IsTriggerEnabled(detectedEvent))
                .ToList();

            if (eligibleFilters.Count == 0)
            {
                _logger.LogInternalDebug(
                    "[IncidentCentricScanner] No eligible filters for event {eventType} on incident {incidentId}",
                    detectedEvent,
                    context.IncidentId);
                continue;
            }

            // Select the most specific filter (most matching criteria)
            // For now, just pick the first one - can add specificity calculation later
            var bestFilter = eligibleFilters
                .OrderByDescending(f => CalculateFilterSpecificity(f))
                .ThenBy(f => f.CreatedAt) // Tie-breaker: oldest filter wins
                .First();

            _logger.LogInternalInformation(
                "[IncidentCentricScanner] Event {eventType} on incident {incidentId} -> Filter {filterId}",
                detectedEvent,
                context.IncidentId,
                bestFilter.Id);

            mappings.Add(new DetectedEventMapping
            {
                IncidentId = context.IncidentId,
                Event = detectedEvent,
                SelectedFilter = bestFilter,
                ScanContext = context,
                TriggeredDiscussionEntries = detectedEvent == IcmIncidentTriggerEvent.DiscussionEntry
                    ? detectionResult.TriggeredDiscussionEntries
                    : new List<DiscussionEntryInfo>()
            });
        }

        return mappings;
    }

    /// <summary>
    /// Calculates a specificity score for a filter based on how many criteria it specifies.
    /// Higher score = more specific filter.
    /// </summary>
    private static int CalculateFilterSpecificity(IcmIncidentFilterDocument filter)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(filter.TitleContains)) score += 10;
#pragma warning disable CS0618
        if (!string.IsNullOrWhiteSpace(filter.Priority) || (filter.Priorities != null && filter.Priorities.Count > 0)) score += 5;
#pragma warning restore CS0618
        if (!string.IsNullOrWhiteSpace(filter.OwningTeamId)) score += 5;
        if (!string.IsNullOrWhiteSpace(filter.IncidentType)) score += 3;
        if (!string.IsNullOrWhiteSpace(filter.MonitorId)) score += 3;
        if (!string.IsNullOrWhiteSpace(filter.CreatedBy)) score += 2;
        if (!string.IsNullOrWhiteSpace(filter.ImpactedService)) score += 2;
        return score;
    }
}
