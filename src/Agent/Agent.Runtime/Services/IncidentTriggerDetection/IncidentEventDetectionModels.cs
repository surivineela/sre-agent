// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.Services.IncidentTriggerDetection;

/// <summary>
/// Represents a snapshot of incident state for event detection comparisons.
/// </summary>
public record IncidentStateSnapshot(
    string IncidentId,
    string State,
    DateTimeOffset CreatedDate,
    DateTimeOffset LastModifiedDate,
    long? HitCount = null
);

/// <summary>
/// Information about a hit count change for correlation detection.
/// </summary>
public record HitCountChangeInfo(
    long PreviousHitCount,
    long CurrentHitCount
);

/// <summary>
/// Information about a discussion entry that triggered agent processing.
/// </summary>
public record DiscussionEntryInfo(
    string EntryId,
    string ChangedBy,
    DateTime Date,
    string Text
);

/// <summary>
/// Result of incident event detection containing detected events and related info.
/// </summary>
public record IncidentEventDetectionResult(
    HashSet<IcmIncidentTriggerEvent> DetectedEvents,
    List<DiscussionEntryInfo> TriggeredDiscussionEntries,
    HitCountChangeInfo? HitCountChange = null
)
{
    /// <summary>
    /// Returns true if any events were detected.
    /// </summary>
    public bool HasEvents => DetectedEvents.Count > 0;

    /// <summary>
    /// Returns the first detected event, or null if none.
    /// Used for backward compatibility when only one event is expected.
    /// </summary>
    public IcmIncidentTriggerEvent? FirstEvent => DetectedEvents.Count > 0 ? DetectedEvents.First() : null;
}
