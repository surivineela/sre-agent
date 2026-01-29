// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.Services;

/// <summary>
/// Service for ingesting thread evaluation snapshots for incident threads.
/// Called by ThreadEvaluator after evaluating incident-related threads.
/// </summary>
public interface IThreadEvaluationSnapshotService
{
    /// <summary>
    /// Ingests an IntentMet score snapshot for an incident thread.
    /// Retrieves the incident document and creates an updated snapshot with the evaluation scores.
    /// </summary>
    /// <param name="threadId">The ID of the evaluated thread</param>
    /// <param name="incidentId">The ID of the incident associated with the thread</param>
    /// <param name="intentMetScore">The intent met score from thread evaluation</param>
    /// <param name="evaluationSummary">The evaluation summary from thread evaluation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task IngestIntentMetSnapshotAsync(
        Guid threadId,
        string incidentId,
        int intentMetScore,
        string evaluationSummary,
        CancellationToken cancellationToken = default);
}
