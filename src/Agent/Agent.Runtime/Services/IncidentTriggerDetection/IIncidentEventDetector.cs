// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.AzureAd.Icm.Types;

namespace Agent.Runtime.Services.IncidentTriggerDetection;

/// <summary>
/// Interface for detecting incident trigger events.
/// Analyzes incident state transitions and discussion entries to determine
/// which trigger events have occurred.
/// </summary>
public interface IIncidentEventDetector
{
    /// <summary>
    /// Detects which trigger events have occurred for an incident.
    /// </summary>
    /// <param name="currentState">Current state snapshot of the incident.</param>
    /// <param name="previousState">Previous state snapshot, or null for first-time processing.</param>
    /// <param name="discussionEntries">Recent discussion entries to check for @mentions.</param>
    /// <param name="processedEntryIds">IDs of entries already processed (for deduplication).</param>
    /// <param name="onCallAliases">List of current on-call aliases for the team (required for DiscussionEntry trigger).</param>
    /// <returns>Detection result containing all detected events and triggered discussion entries.</returns>
    IncidentEventDetectionResult DetectOccurredEvents(
        IncidentStateSnapshot currentState,
        IncidentStateSnapshot? previousState,
        List<DescriptionEntry> discussionEntries,
        HashSet<string>? processedEntryIds = null,
        List<string>? onCallAliases = null);
}
