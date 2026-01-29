// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;

namespace Agent.Runtime.Services.IncidentTriggerDetection;

/// <summary>
/// Interface for looking up existing threads for an incident.
/// Used to determine if a thread already exists for a given incident + agent combination.
/// </summary>
public interface IIncidentThreadLookupService
{
    /// <summary>
    /// Finds an existing thread for the specified incident.
    /// </summary>
    /// <param name="incidentId">The incident ID to look up.</param>
    /// <returns>The thread document if found, null otherwise.</returns>
    Task<ThreadDocument?> FindThreadByIncidentIdAsync(string incidentId);

    /// <summary>
    /// Finds an existing thread for the specified incident and handler combination (Phase 2).
    /// Thread key is (incidentId, handlerId) for multi-agent support.
    /// </summary>
    /// <param name="incidentId">The incident ID.</param>
    /// <param name="handlerId">The handler agent ID.</param>
    /// <returns>The thread document if found, null otherwise.</returns>
    Task<ThreadDocument?> FindThreadByIncidentAndHandlerAsync(string incidentId, string handlerId);

    /// <summary>
    /// Gets all threads for a given incident (Phase 2).
    /// Used for multi-agent scenarios where multiple threads may exist.
    /// </summary>
    /// <param name="incidentId">The incident ID.</param>
    /// <returns>List of all thread documents for this incident.</returns>
    Task<List<ThreadDocument>> FindAllThreadsForIncidentAsync(string incidentId);

    /// <summary>
    /// Checks if an existing thread should be reused for the given incident and handler.
    /// </summary>
    /// <param name="incidentId">The incident ID.</param>
    /// <param name="handlerId">The handler agent ID.</param>
    /// <returns>True if an existing thread exists and should be reused.</returns>
    Task<bool> ShouldReuseThreadAsync(string incidentId, string handlerId);
}
