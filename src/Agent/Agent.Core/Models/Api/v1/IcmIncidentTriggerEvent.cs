// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Defines the events that can trigger agent processing for an incident.
/// ICM provider only.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IcmIncidentTriggerEvent
{
    /// <summary>
    /// Default trigger. Fires when incident created OR transferred to team.
    /// </summary>
    IncidentCreatedOrTransferred,

    /// <summary>
    /// Fires when discussion entry added with @sreagent mention by current on-call.
    /// STRICT: Will not fire if on-call is not configured.
    /// </summary>
    DiscussionEntry,

    /// <summary>
    /// Fires when incident state changes to Mitigated.
    /// </summary>
    IncidentMitigated,

    /// <summary>
    /// Fires when incident state changes from Mitigated/Resolved to Active.
    /// </summary>
    IncidentReactivated,

    /// <summary>
    /// Fires when incident state changes to Resolved.
    /// </summary>
    IncidentResolved,

    /// <summary>
    /// Fires when incident hit count increases (correlation detected).
    /// Only triggers when HitCount > 1 and has increased from previous value.
    /// </summary>
    HitCountIncreased
}
